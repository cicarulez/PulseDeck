using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PulseDeck.Core;

public sealed record GmailUpdate(int UnreadCount, bool NewMail);
public sealed class GmailMailbox(HttpClient client)
{
    private string? historyId;
    public void Reset() => historyId = null;
    public async Task<GmailUpdate> Read(string accessToken, CancellationToken token)
    {
        async Task<JsonDocument> Get(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/" + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        }
        var nextId = historyId;
        var added = new HashSet<string>(StringComparer.Ordinal);
        if (historyId is not null)
        {
            try
            {
                string? page = null;
                var pages = 0;
                do
                {
                    if (++pages > 100) throw new InvalidDataException("Gmail history exceeds refresh budget.");
                    using var doc = await Get("history?historyTypes=messageAdded&maxResults=500&startHistoryId=" + Uri.EscapeDataString(historyId)
                        + (page is null ? "" : "&pageToken=" + Uri.EscapeDataString(page)));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("history", out var history))
                        foreach (var record in history.EnumerateArray())
                            if (record.TryGetProperty("messagesAdded", out var messages))
                                foreach (var entry in messages.EnumerateArray())
                                {
                                    var message = entry.GetProperty("message");
                                    var id = message.GetProperty("id").GetString()!;
                                    string?[] labels;
                                    if (message.TryGetProperty("labelIds", out var ids)) labels = ids.EnumerateArray().Select(x => x.GetString()).ToArray();
                                    else
                                    {
                                        // History may return only id/threadId. Request labels only, never headers/body.
                                        try
                                        {
                                            using var metadata = await Get("messages/" + Uri.EscapeDataString(id) + "?format=metadata&fields=labelIds");
                                            labels = metadata.RootElement.TryGetProperty("labelIds", out ids) ? ids.EnumerateArray().Select(x => x.GetString()).ToArray() : [];
                                        }
                                        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound) { continue; }
                                    }
                                    if (labels.Contains("INBOX") && labels.Contains("UNREAD") && !labels.Contains("SPAM") && !labels.Contains("TRASH") && !labels.Contains("DRAFT") && !labels.Contains("SENT"))
                                        added.Add(id);
                                }
                    nextId = root.GetProperty("historyId").GetString();
                    page = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
                } while (page is not null);
            }
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound) { nextId = null; added.Clear(); }
        }
        if (nextId is null)
        {
            using var profile = await Get("profile?fields=historyId");
            nextId = profile.RootElement.GetProperty("historyId").GetString();
        }
        // Gmail's authoritative message count, never a threads count or resultSizeEstimate.
        using var unread = await Get("labels/INBOX?fields=messagesUnread");
        var count = unread.RootElement.GetProperty("messagesUnread").GetInt32();
        if (count < 0) throw new InvalidDataException("Invalid unread count.");
        historyId = nextId; // Commit only after every page and the count succeeded.
        return new(count, added.Count > 0);
    }
}
