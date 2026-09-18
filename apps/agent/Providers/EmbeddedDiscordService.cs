using Discord.Audio;
using System.Security.Cryptography;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

// Owns Gateway and an optional presence-driven voice connection. Never sends or records audio/messages.
public sealed class EmbeddedDiscordService(ConfigStore config, ILogger<EmbeddedDiscordService> logger) : BackgroundService
{
    private DiscordSocketClient? client;
    private ulong guildId;
    private ulong channelId;
    private volatile bool ready;
    private string detail = "Connessione a Discord in preparazione.";
    private string status = "starting";
    private IAudioClient? voice;
    private ulong voiceChannelId;
    private CancellationTokenSource? voiceLifetime;
    private readonly VoiceActivity speaking = new();
    private volatile bool encryptionReady;
    private volatile string? voiceFault;
    private string? voiceDetail;
    private DateTimeOffset voiceConnectedAt;
    private void NativeVoiceLog(Discord.LibDave.Binding.LoggingSeverity severity, string file, int line, string message)
    {
        // Never write native payloads, key packages or participant IDs to application logs.
        if (message.Contains("Successfully welcomed to MLS Group", StringComparison.Ordinal)
            || message.Contains("Successfully processed MLS commit", StringComparison.Ordinal))
        { encryptionReady = true; voiceFault = null; }
        else if (message.Contains("MLS welcome lists unrecognized user ID", StringComparison.Ordinal)
            || message.Contains("Group received in MLS welcome is not valid", StringComparison.Ordinal))
        { encryptionReady = false; voiceFault = "voice-encryption-roster"; }
    }
    private string speakingStatus = "inactive";
    private DateTimeOffset voiceRetryAt;

    private static bool WantsVoice(DeckConfig settings, SocketVoiceChannel? channel) =>
        channel is not null && DiscordVoicePolicy.ShouldConnect(settings,
            channel.ConnectedUsers.Where(user => !user.IsBot).Select(user => user.Id.ToString()));

    private ulong SelectedChannel(DeckConfig settings) => DiscordVoicePolicy.SelectedChannel(settings, channelId);

    public DiscordOptions Options()
    {
        var guild = ready ? client?.GetGuild(guildId) : null;
        if (guild is null) return new("unavailable", null, SelectedChannel(config.Current).ToString(), [], []);
        var channels = guild.VoiceChannels.OrderBy(c => c.Position).Select(c => new DiscordChoice(c.Id.ToString(), c.Name)).Take(200).ToArray();
        var members = guild.Users.Where(u => !u.IsBot).OrderBy(u => u.DisplayName)
            .Select(u => new DiscordChoice(u.Id.ToString(), u.DisplayName == u.Username ? u.DisplayName : $"{u.DisplayName} · {u.Username}")).Take(500).ToArray();
        return new("connected", guild.Name, SelectedChannel(config.Current).ToString(), channels, members);
    }

    private async Task UpdateVoice(CancellationToken token)
    {
        var selected = SelectedChannel(config.Current);
        var channel = client?.GetGuild(guildId)?.GetVoiceChannel(selected);
        if (voice is not null && voiceChannelId != selected)
        {
            await StopVoice(); voiceRetryAt = DateTimeOffset.MinValue;
        }
        if (!ready || !WantsVoice(config.Current, channel))
        {
            await StopVoice(); speakingStatus = "inactive"; voiceRetryAt = DateTimeOffset.MinValue; voiceDetail = null; return;
        }
        if (voice?.ConnectionState == ConnectionState.Connected)
        {
            if (voiceFault is null && (encryptionReady || DateTimeOffset.UtcNow - voiceConnectedAt < TimeSpan.FromSeconds(15))) return;
            voiceDetail = voiceFault ?? "voice-encryption-timeout";
            logger.LogWarning("Discord Voice reconnect requested: {Reason}", voiceDetail);
            await StopVoice();
            speakingStatus = "unavailable";
            voiceRetryAt = DateTimeOffset.UtcNow.AddSeconds(30);
            return;
        }
        if (DateTimeOffset.UtcNow < voiceRetryAt) return;
        await StopVoice();
        voiceRetryAt = DateTimeOffset.UtcNow.AddSeconds(30);
        if (channel is null) { speakingStatus = "unavailable"; return; }
        speakingStatus = "connecting"; voiceDetail = null; encryptionReady = false; voiceFault = null;
        try
        {
            // StopAsync in the catch also cancels an incomplete voice handshake.
            voice = await channel.ConnectAsync(selfDeaf: false, selfMute: true).WaitAsync(TimeSpan.FromSeconds(15), token);
            voiceChannelId = selected;
            voiceConnectedAt = DateTimeOffset.UtcNow;
            voiceLifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            var drainToken = voiceLifetime.Token;
            voice.StreamCreated += (id, stream) => { _ = Drain(stream, drainToken); return Task.CompletedTask; };
            foreach (var stream in voice.GetStreams().Values) _ = Drain(stream, drainToken);
            voice.SpeakingUpdated += (id, active) => { speaking.Update(id, active, DateTimeOffset.UtcNow); return Task.CompletedTask; };
            voice.ClientDisconnected += id => { speaking.Remove(id); return Task.CompletedTask; };
            voice.Disconnected += _ => { speaking.Clear(); speakingStatus = "unavailable"; return Task.CompletedTask; };
            speakingStatus = "connected";
        }
        catch (Exception error)
        {
            voiceDetail = error is TimeoutException ? "voice-connect-timeout" : "voice-connect-failed";
            logger.LogWarning("Discord Voice could not connect: {Reason}", voiceDetail);
            try { await channel.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
            await StopVoice(); speakingStatus = "unavailable";
        }
    }

    private static async Task Drain(AudioInStream stream, CancellationToken token)
    {
        try { await stream.CopyToAsync(Stream.Null, 4096, token); } catch { }
    }

    private async Task StopVoice()
    {
        voiceLifetime?.Cancel(); voiceLifetime?.Dispose(); voiceLifetime = null;
        var old = voice; voice = null; speaking.Clear(); encryptionReady = false; voiceFault = null;
        if (old is null) return;
        try { await old.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        old.Dispose();
    }

    public DiscordSnapshot Read(DeckConfig settings)
    {
        var socket = client;
        if (!ready || socket?.ConnectionState != ConnectionState.Connected)
            return new([], null, status, detail);
        var guild = socket.GetGuild(guildId);
        if (guild is null || !((IGuild)guild).Available)
            return new([], null, "unavailable", "Il server configurato non è disponibile per il bot.");
        var selected = SelectedChannel(settings);
        var channel = guild.GetVoiceChannel(selected);
        if (channel is null)
            return new([], null, "unavailable", "Il canale vocale configurato non è disponibile per il bot.");
        var wanted = WantsVoice(settings, channel);
        var voiceAvailable = wanted && voiceChannelId == selected && encryptionReady && voiceFault is null && voice?.ConnectionState == ConnectionState.Connected;
        var members = channel.ConnectedUsers.Where(u => u.Id != socket.CurrentUser.Id).Select(u => new VoiceMember(u.Id.ToString(), u.DisplayName,
            u.IsMuted || u.IsSelfMuted, u.IsDeafened || u.IsSelfDeafened)
            { Speaking = voiceAvailable ? speaking.IsSpeaking(u.Id, DateTimeOffset.UtcNow) : null, Streaming = u.IsStreaming })
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(members, members.FirstOrDefault(m => m.Id == settings.TrackedMemberId), "connected",
            $"{guild.Name} / {channel.Name} · Discord integrato" + (voiceAvailable ? " · bot nel canale vocale" : ""))
            { ServerName = guild.Name, ChannelName = channel.Name,
                SpeakingStatus = !wanted ? "inactive" : voiceAvailable ? "connected"
                : voice?.ConnectionState == ConnectionState.Connected && voiceFault is null ? "connecting" : speakingStatus,
                SpeakingDetail = voiceDetail };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The current-user DPAPI file is outside the repository and is never returned by HTTP.
        var path = Path.Combine(config.DirectoryPath, "discord.credentials");
        if (!File.Exists(path))
        {
            status = "not-configured";
            detail = "Importa la configurazione con Import-Discord.ps1 e riavvia PulseDeck.";
            return;
        }
        string token;
        try
        {
            var clear = ProtectedData.Unprotect(await File.ReadAllBytesAsync(path, stoppingToken), null, DataProtectionScope.CurrentUser);
            try
            {
                using var document = JsonDocument.Parse(clear);
                var root = document.RootElement;
                token = root.GetProperty("botToken").GetString() ?? "";
                guildId = ulong.Parse(root.GetProperty("guildId").GetString() ?? "");
                channelId = ulong.Parse(root.GetProperty("voiceChannelId").GetString() ?? "");
                if (string.IsNullOrWhiteSpace(token) || guildId == 0 || channelId == 0) throw new InvalidDataException();
            }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch
        {
            status = "error";
            detail = "Credenziali Discord non leggibili. Ripeti l’importazione con lo stesso utente Windows che avvia PulseDeck.";
            return;
        }

        try { Discord.LibDave.Dave.SetLogSink(NativeVoiceLog); }
        catch { voiceDetail = "voice-native-libraries-unavailable"; }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (config.Current.DiscordMode != "embedded")
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }
            using var socket = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                AlwaysDownloadUsers = false, MessageCacheSize = 0, LogLevel = LogSeverity.Error,
                EnableVoiceDaveEncryption = true
            });
            client = socket;
            socket.Ready += () => { ready = true; status = "connecting"; return Task.CompletedTask; };
            socket.Disconnected += _ =>
            {
                ready = false; status = "reconnecting"; detail = "Connessione Discord interrotta; riconnessione automatica in corso.";
                return Task.CompletedTask;
            };
            // A resumed session may not emit Ready again.
            socket.Connected += () => { if (socket.Guilds.Count > 0) ready = true; return Task.CompletedTask; };
            try
            {
                status = "connecting";
                detail = "Connessione del bot a Discord…";
                await socket.LoginAsync(TokenType.Bot, token).WaitAsync(stoppingToken);
                await socket.StartAsync().WaitAsync(stoppingToken);
                while (config.Current.DiscordMode == "embedded")
                {
                    await UpdateVoice(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                status = "error";
                detail = "Connessione Discord non riuscita. Verifica token, accesso del bot al server e rete. Nuovo tentativo tra 30 secondi.";
            }
            finally
            {
                ready = false;
                await StopVoice();
                client = null;
                try { await socket.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            }
            if (config.Current.DiscordMode == "embedded")
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

public sealed record DiscordChoice(string Id, string Name);
public sealed record DiscordOptions(string Status, string? ServerName, string ChannelId, DiscordChoice[] Channels, DiscordChoice[] Members);
