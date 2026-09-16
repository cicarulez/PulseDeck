using System.Security.Cryptography;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

// Owns the Gateway connection for the lifetime of PulseDeck. No audio connection or messages.
public sealed class EmbeddedDiscordService(ConfigStore config) : BackgroundService
{
    private DiscordSocketClient? client;
    private ulong guildId;
    private ulong channelId;
    private volatile bool ready;
    private string detail = "Connessione a Discord in preparazione.";
    private string status = "starting";

    public DiscordSnapshot Read(DeckConfig settings)
    {
        var socket = client;
        if (!ready || socket?.ConnectionState != ConnectionState.Connected)
            return new([], null, status, detail);
        var guild = socket.GetGuild(guildId);
        if (guild is null || !((IGuild)guild).Available)
            return new([], null, "unavailable", "Il server configurato non è disponibile per il bot.");
        var channel = guild.GetVoiceChannel(channelId);
        if (channel is null)
            return new([], null, "unavailable", "Il canale vocale configurato non è disponibile per il bot.");
        var members = channel.ConnectedUsers.Select(u => new VoiceMember(u.Id.ToString(), u.DisplayName,
            u.IsMuted || u.IsSelfMuted, u.IsDeafened || u.IsSelfDeafened))
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(members, members.FirstOrDefault(m => m.Id == settings.TrackedMemberId), "connected",
            $"{guild.Name} / {channel.Name} · Discord integrato");
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
                AlwaysDownloadUsers = false, MessageCacheSize = 0, LogLevel = LogSeverity.Error
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
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
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
                client = null;
                try { await socket.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            }
            if (config.Current.DiscordMode == "embedded")
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
