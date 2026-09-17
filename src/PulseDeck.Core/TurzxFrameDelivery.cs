namespace PulseDeck.Core;

/// <summary>Serial operations are supplied by the identified Windows adapter.
/// The caller serializes access; retries run on later render ticks, without a queue.</summary>
public sealed class TurzxFrameDelivery(Action<byte[]> write, Func<string> readStatus,
    Func<int> reopen, Action close, Func<bool> cancelled, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private byte[]? previous;
    private uint counter;
    private int rom;
    private bool active, pending, mustReopen, failed;
    private long failedAt;
    private int healthyFrames;
    public int Attempts { get; private set; }
    public int Recoveries { get; private set; }
    public long AcknowledgedFrames { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LastAcknowledgedAt { get; private set; }
    public string? LastFrameKind { get; private set; }
    public uint? LastFrameCounter { get; private set; }
    public string State => !active ? (failed ? "error" : "disconnected") : pending ? "recovering" : "connected";

    public void Start(int firmware)
    {
        Cancel();
        rom = firmware;
        active = true;
        Attempts = Recoveries = 0;
        AcknowledgedFrames = 0;
        LastAcknowledgedAt = null;
        LastError = null;
        LastFrameKind = null;
        LastFrameCounter = null;
    }

    public void Cancel()
    {
        active = pending = mustReopen = failed = false;
        previous = null;
        counter = 0;
        healthyFrames = 0;
    }

    public void Send(byte[] pixels)
    {
        if (!active || cancelled()) return;
        if (pending && time.GetElapsedTime(failedAt) < TimeSpan.FromSeconds(Attempts == 0 ? 2 : 5)) return;
        // Validate before performing any serial operation, including a recovery reopen.
        if (pixels.Length != TurzxProtocol.Width * TurzxProtocol.Height * 4)
            throw new ArgumentException("Expected a 1920x480 BGRA frame.", nameof(pixels));
        try
        {
            if (pending)
            {
                Attempts++;
                if (mustReopen)
                {
                    CheckCancellation();
                    rom = reopen();
                    counter = 0;
                }
            }
            if (previous is null)
            {
                LastFrameKind = "full";
                LastFrameCounter = null;
                Write(TurzxProtocol.Packet(Convert.FromHexString("86EF6900000001")));
                Write(TurzxProtocol.Packet([0x2c], 0x2c));
                Write(TurzxProtocol.FullFrameCommand());
                Write(TurzxProtocol.FullFrame(pixels));
                var response = ReadStatus();
                if (!response.Contains("full_png_sucess", StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Frame was not acknowledged: {response}");
            }
            else if (TurzxProtocol.ChangedRegion(previous, pixels) is { } rect)
            {
                LastFrameKind = "partial";
                LastFrameCounter = counter;
                var (header, payload) = TurzxProtocol.PartialFrame(pixels, rect, rom, counter++);
                Write(header);
                Write(payload);
            }
            else return;

            Write(TurzxProtocol.Packet(Convert.FromHexString("CFEF6900000001")));
            var state = ReadStatus();
            // Match the complete numeric field, not a prefix such as needReSend:01.
            if (System.Text.RegularExpressions.Regex.IsMatch(state, @"\bneedReSend:1\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new ResendRequestedException($"Display requested a resend: {state}");
            if (!System.Text.RegularExpressions.Regex.IsMatch(state, @"\bneedReSend:0\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new IOException($"Invalid display status: {state}");

            CheckCancellation();
            previous = pixels;
            AcknowledgedFrames++;
            LastAcknowledgedAt = time.GetUtcNow();
            if (pending) Recoveries++;
            pending = mustReopen = false;
            // A single successful retry must not renew an otherwise endless retry loop.
            if (++healthyFrames >= 60) Attempts = 0;
        }
        catch (OperationCanceledException) when (cancelled()) { Cancel(); }
        // Adapter failures (including Windows device enumeration) must consume the
        // same bounded budget, rather than escaping and retrying on every render tick.
        catch (Exception e)
        {
            LastError = e.Message;
            previous = null; // Never diff against an unacknowledged frame.
            healthyFrames = 0;
            // A full resend can be acknowledged while subsequent partial frames
            // still fail. Escalate within the existing two-attempt budget to the
            // same identity-checked reinitialization used after a transport error.
            mustReopen = e is not ResendRequestedException || Attempts > 0;
            if (mustReopen || Attempts >= 2 || cancelled()) close();
            active = !cancelled() && Attempts < 2;
            pending = active;
            failed = !active && !cancelled();
            failedAt = time.GetTimestamp();
        }
    }

    private void CheckCancellation()
    {
        if (cancelled()) throw new OperationCanceledException();
    }
    private void Write(byte[] packet) { CheckCancellation(); write(packet); }
    private string ReadStatus() { CheckCancellation(); return readStatus(); }
    private sealed class ResendRequestedException(string message) : IOException(message);
}
