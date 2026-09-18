using PulseDeck.Core;
using Xunit;

public class DisplayRecoveryTests
{
    private static byte[] Frame(byte value = 0)
    {
        var frame = new byte[1920 * 480 * 4];
        frame[0] = value;
        return frame;
    }

    [Fact]
    public void PartialRejectionUsesLatestFullFrameAndKeepsFullUpdates()
    {
        var rig = new Rig();
        rig.FullSuccess(Frame());
        rig.Replies.Enqueue("needReSend:1");
        rig.Delivery.Send(Frame(1));
        Assert.Equal("recovering", rig.Delivery.State);
        Assert.Equal(1, rig.Delivery.AcknowledgedFrames);
        rig.Writes.Clear();
        rig.Delivery.Send(Frame(2));
        Assert.Empty(rig.Writes);
        rig.Clock.Advance(2);
        rig.FullSuccess(Frame(3));
        Assert.Equal(TurzxProtocol.FullFrameCommand(), rig.Writes[2]);
        Assert.Equal(TurzxProtocol.FullFrame(Frame(3)), rig.Writes[3]);
        Assert.Equal(0, rig.Reopens);
        Assert.Equal(0, rig.Closes);
        Assert.Equal(1, rig.Delivery.Recoveries);
        Assert.Equal("connected", rig.Delivery.State);
        rig.Writes.Clear();
        rig.FullSuccess(Frame(4));
        Assert.True(rig.Delivery.FullFrameFallback);
        Assert.Equal(TurzxProtocol.FullFrameCommand(), rig.Writes[2]);
        Assert.Equal(new FrameRect(0, 0, 1920, 480), rig.Delivery.LastFrameRegion);
        Assert.Equal(rig.Writes.Sum(packet => packet.Length), rig.Delivery.LastTransferBytes);
        rig.Writes.Clear();
        rig.Delivery.Send(Frame(4));
        Assert.Empty(rig.Writes);
        Assert.Equal(3, rig.Delivery.AcknowledgedFrames);
        Assert.NotNull(rig.Delivery.LastAcknowledgedAt);
        Assert.Contains("needReSend:1", rig.Delivery.LastError);
    }

    [Fact]
    public void FullRejectionInFallbackReinitializesWithinExistingBudgetAndKeepsFallback()
    {
        var rig = new Rig();
        rig.FullSuccess(Frame());
        rig.Replies.Enqueue("needReSend:0"); rig.Delivery.Send(Frame(1));
        rig.Replies.Enqueue("needReSend:1|renderCnt:0"); rig.Delivery.Send(Frame(2));
        Assert.Equal("partial", rig.Delivery.LastFrameKind);
        Assert.Equal((uint)1, rig.Delivery.LastFrameCounter);
        rig.Clock.Advance(2); rig.FullSuccess(Frame(3));
        Assert.Equal("full", rig.Delivery.LastFrameKind);
        Assert.Null(rig.Delivery.LastFrameCounter);
        Assert.Equal(0, rig.Reopens);
        rig.Replies.Enqueue("full_png_sucess");
        rig.Replies.Enqueue("needReSend:1|renderCnt:0"); rig.Delivery.Send(Frame(4));
        Assert.Equal(1, rig.Closes);
        Assert.Null(rig.Delivery.LastFrameCounter);
        rig.Clock.Advance(4); rig.Delivery.Send(Frame(5)); Assert.Equal(0, rig.Reopens);
        rig.Clock.Advance(1); rig.FullSuccess(Frame(5));
        Assert.Equal(1, rig.Reopens);
        Assert.Equal(2, rig.Delivery.Attempts);
        Assert.Equal(2, rig.Delivery.Recoveries);
        rig.Writes.Clear();
        rig.FullSuccess(Frame(6));
        Assert.Equal(TurzxProtocol.FullFrameCommand(), rig.Writes[2]);
        Assert.True(rig.Delivery.FullFrameFallback);
        rig.FullSuccess(Frame(7));
        Assert.Null(rig.Delivery.LastFrameCounter);
        Assert.Equal("connected", rig.Delivery.State);
        Assert.Equal(2, rig.Delivery.Attempts); // a retry success does not renew the budget
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationPreventsEscalatedResendReopen(bool shutdown)
    {
        var rig = new Rig(); rig.FullSuccess(Frame());
        rig.Replies.Enqueue("needReSend:1"); rig.Delivery.Send(Frame(1));
        rig.Clock.Advance(2); rig.FullSuccess(Frame(2));
        rig.Replies.Enqueue("full_png_sucess");
        rig.Replies.Enqueue("needReSend:1"); rig.Delivery.Send(Frame(3));
        if (shutdown) rig.Cancelled = true; else rig.Delivery.Cancel();
        rig.Clock.Advance(5); rig.Writes.Clear(); rig.Delivery.Send(Frame(4));
        Assert.Empty(rig.Writes); Assert.Equal(0, rig.Reopens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TimeoutReopensBeforeFullFrameAndResetsPartialCounter(bool writeTimeout)
    {
        var rig = new Rig();
        rig.FullSuccess(Frame());
        if (writeTimeout) rig.WriteFailure = new TimeoutException("write timeout");
        else rig.Replies.Enqueue(new TimeoutException("short response"));
        rig.Delivery.Send(Frame(1));
        Assert.Equal(1, rig.Closes);
        rig.Clock.Advance(2);
        rig.Writes.Clear();
        rig.FullSuccess(Frame(2));
        Assert.Equal(1, rig.Reopens);
        Assert.Equal(0xc8, rig.Writes[2][0]);
        rig.Writes.Clear();
        rig.Replies.Enqueue("needReSend:0");
        rig.Delivery.Send(Frame(3));
        Assert.Equal(new byte[4], rig.Writes[0][10..14]);
    }

    [Theory]
    [InlineData("needReSend:01")]
    [InlineData("unrecognized")]
    [InlineData("needReSend:10")]
    [InlineData("needReSend:0 needReSend:1")]
    public void InvalidOrNegativeStatusNeverAcknowledgesFrame(string reply)
    {
        var rig = new Rig();
        rig.Replies.Enqueue("full_png_sucess");
        rig.Replies.Enqueue(reply);
        rig.Delivery.Send(Frame());
        Assert.Equal(0, rig.Delivery.AcknowledgedFrames);
        Assert.Equal("recovering", rig.Delivery.State);
    }

    [Fact]
    public void MissingFullFrameAcknowledgementRequiresReopen()
    {
        var rig = new Rig();
        rig.Replies.Enqueue("needReSend:0");
        rig.Delivery.Send(Frame());
        Assert.Equal("recovering", rig.Delivery.State);
        Assert.Equal(1, rig.Closes);
        Assert.Equal(0, rig.Delivery.AcknowledgedFrames);
    }

    [Fact]
    public void RepeatedResendsExhaustTwoRetriesAndRequireExplicitStart()
    {
        var rig = new Rig();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            rig.Replies.Enqueue("full_png_sucess");
            rig.Replies.Enqueue("needReSend:1");
            rig.Delivery.Send(Frame());
            rig.Clock.Advance(attempt == 0 ? 2 : 5);
        }
        Assert.Equal("error", rig.Delivery.State);
        Assert.Equal(2, rig.Delivery.Attempts);
        Assert.Equal(2, rig.Closes);
        Assert.Equal(1, rig.Reopens);
        rig.Writes.Clear();
        for (var i = 0; i < 100; i++) { rig.Clock.Advance(60); rig.Delivery.Send(Frame()); }
        Assert.Empty(rig.Writes);
        rig.Delivery.Start(90);
        rig.FullSuccess(Frame());
        Assert.Equal("connected", rig.Delivery.State);
        Assert.Equal(0, rig.Delivery.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedReopenIsBoundedAndNeverSendsPixels(bool enumerationFailure)
    {
        var rig = new Rig();
        rig.WriteFailure = new TimeoutException();
        rig.Delivery.Send(Frame());
        rig.OpenFailure = enumerationFailure ? new Exception("device enumeration failed") : new IOException("identity rejected or port absent");
        rig.Writes.Clear();
        rig.Clock.Advance(2);
        rig.Delivery.Send(Frame());
        Assert.Equal(1, rig.Reopens);
        rig.Clock.Advance(4);
        rig.Delivery.Send(Frame());
        Assert.Equal(1, rig.Reopens);
        rig.Clock.Advance(1);
        rig.Delivery.Send(Frame());
        Assert.Equal(2, rig.Reopens);
        Assert.Equal("error", rig.Delivery.State);
        Assert.Empty(rig.Writes);
        rig.Clock.Advance(100);
        rig.Delivery.Send(Frame());
        Assert.Equal(2, rig.Reopens);
    }

    [Fact]
    public void IntermittentFailuresCannotRenewBudgetAfterOneSuccessfulFrame()
    {
        var rig = new Rig();
        rig.FullSuccess(Frame());
        for (byte i = 1; i <= 3; i++)
        {
            if (i > 1) rig.Replies.Enqueue("full_png_sucess");
            rig.Replies.Enqueue("needReSend:1");
            rig.Delivery.Send(Frame(i));
            if (i == 3) break;
            rig.Clock.Advance(5);
            rig.FullSuccess(Frame(i));
        }
        Assert.Equal("error", rig.Delivery.State);
        Assert.Equal(2, rig.Delivery.Recoveries);
    }

    [Fact]
    public void OnlySixtyAcknowledgedFramesRenewRetryBudget()
    {
        var rig = new Rig();
        rig.Replies.Enqueue(new TimeoutException());
        rig.Delivery.Send(Frame());
        rig.Clock.Advance(2);
        rig.FullSuccess(Frame());
        for (int i = 0; i < 100; i++) rig.Delivery.Send(Frame());
        Assert.Equal(1, rig.Delivery.Attempts);
        for (byte i = 1; i < 60; i++)
        {
            rig.Replies.Enqueue("needReSend:0");
            rig.Delivery.Send(Frame(i));
        }
        Assert.Equal(0, rig.Delivery.Attempts);
    }

    [Fact]
    public void HealthyFullFramesRenewBudgetButOnlyExplicitStartClearsFallback()
    {
        var rig = new Rig(); rig.FullSuccess(Frame());
        rig.Replies.Enqueue("needReSend:1"); rig.Delivery.Send(Frame(1));
        Assert.Equal(new FrameRect(0, 0, 1, 1), rig.Delivery.LastFrameRegion);
        Assert.Equal(750, rig.Delivery.LastTransferBytes);
        rig.Clock.Advance(2);
        for (byte i = 1; i <= 60; i++)
        {
            rig.Writes.Clear();
            rig.FullSuccess(Frame(i));
            Assert.Equal("full", rig.Delivery.LastFrameKind);
        }
        Assert.Equal(0, rig.Delivery.Attempts);
        Assert.True(rig.Delivery.FullFrameFallback);
        rig.Delivery.Start(90);
        Assert.False(rig.Delivery.FullFrameFallback);
        rig.FullSuccess(Frame());
        rig.Writes.Clear();
        rig.Replies.Enqueue("needReSend:0"); rig.Delivery.Send(Frame(1));
        Assert.Equal(0xcc, rig.Writes[0][0]);
        Assert.Equal((uint)0, rig.Delivery.LastFrameCounter);
    }

    [Fact]
    public void TransportTimeoutDuringFallbackPreservesFullUpdatesAndMeasuresDuration()
    {
        var rig = new Rig(); rig.FullSuccess(Frame());
        rig.Replies.Enqueue("needReSend:1"); rig.Delivery.Send(Frame(1));
        rig.Clock.Advance(2); rig.FullSuccess(Frame(2));
        rig.Replies.Enqueue(new TimeoutException()); rig.Delivery.Send(Frame(3));
        rig.Clock.Advance(5);
        rig.Writes.Clear();
        rig.OnWrite = () => rig.Clock.Advance(1);
        rig.FullSuccess(Frame(4));
        Assert.Equal(1, rig.Reopens);
        Assert.True(rig.Delivery.FullFrameFallback);
        Assert.Equal(5000, rig.Delivery.LastTransferMilliseconds);
        rig.Writes.Clear();
        rig.FullSuccess(Frame(5));
        Assert.Equal(TurzxProtocol.FullFrameCommand(), rig.Writes[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectOrShutdownCancelsPendingRecovery(bool shutdown)
    {
        var rig = new Rig();
        rig.Replies.Enqueue(new TimeoutException());
        rig.Delivery.Send(Frame());
        if (shutdown) rig.Cancelled = true;
        else rig.Delivery.Cancel();
        rig.Clock.Advance(100);
        rig.Writes.Clear();
        rig.Delivery.Send(Frame());
        Assert.Equal(0, rig.Reopens);
        Assert.Empty(rig.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringReopenOrTransferStopsFurtherWrites(bool duringReopen)
    {
        var rig = new Rig();
        if (duringReopen)
        {
            rig.Replies.Enqueue(new TimeoutException());
            rig.Delivery.Send(Frame());
            rig.Clock.Advance(2);
            rig.Writes.Clear();
            rig.OnOpen = () => rig.Cancelled = true;
        }
        else rig.OnWrite = () => rig.Cancelled = true;
        rig.Delivery.Send(Frame());
        Assert.Equal(duringReopen ? 0 : 1, rig.Writes.Count);
        Assert.Equal(0, rig.Delivery.AcknowledgedFrames);
        Assert.Equal("disconnected", rig.Delivery.State);
    }

    [Fact]
    public void InvalidFrameDoesNotTouchTransport()
    {
        var rig = new Rig();
        Assert.Throws<ArgumentException>(() => rig.Delivery.Send(new byte[10]));
        Assert.Empty(rig.Writes);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => seconds;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddSeconds(seconds);
        public void Advance(int value) => seconds += value;
    }

    private sealed class Rig
    {
        public ManualClock Clock { get; } = new();
        public List<byte[]> Writes { get; } = [];
        public Queue<object> Replies { get; } = new();
        public Exception? WriteFailure, OpenFailure;
        public Action? OnOpen, OnWrite;
        public bool Cancelled;
        public int Reopens, Closes;
        public TurzxFrameDelivery Delivery { get; }
        public Rig()
        {
            Delivery = new(packet =>
            {
                if (WriteFailure is { } failure) { WriteFailure = null; throw failure; }
                Writes.Add(packet);
                OnWrite?.Invoke();
            }, () =>
            {
                var reply = Replies.Dequeue();
                return reply is Exception failure ? throw failure : (string)reply;
            }, () =>
            {
                Reopens++;
                if (OpenFailure is not null) throw OpenFailure;
                OnOpen?.Invoke();
                return 90;
            }, () => Closes++, () => Cancelled, Clock);
            Delivery.Start(90);
        }
        public void FullSuccess(byte[] frame)
        {
            Replies.Enqueue("full_png_sucess");
            Replies.Enqueue("needReSend:0");
            Delivery.Send(frame);
        }
    }
}
