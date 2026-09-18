using PulseDeck.Core;
using Xunit;
public class MusicTransitionTests
{
    private static MediaSnapshot Spotify => new(true,"Synthetic track","Artist","Spotify.exe",99,100,"connected");
    [Fact]
    public void TrackGapKeepsMusicButRealStopEventuallyExits()
    {
        var s=new ProfileSelector(); var c=new DeckConfig {ProfileDelaySeconds=0};var t=DateTimeOffset.UtcNow;
        Assert.Equal("music",s.Select(c,"explorer",true,t,false,Spotify));
        var gap=new MediaSnapshot(false,"","","",0,0,"idle");
        Assert.Equal("music",s.Select(c,"explorer",false,t.AddSeconds(1),false,gap));
        Assert.True(s.SpotifyTransition);
        Assert.Equal("music",s.Select(c,"explorer",false,t.AddSeconds(7),false,gap));
        Assert.Equal("music",s.Select(c,"explorer",true,t.AddSeconds(8),false,Spotify with {Title="Next",PositionSeconds=0}));
        Assert.False(s.SpotifyTransition);
        var paused=Spotify with {Playing=false,PositionSeconds=20};
        Assert.Equal("music",s.Select(c,"explorer",false,t.AddSeconds(9),false,paused));
        Assert.Equal("desktop",s.Select(c,"explorer",false,t.AddSeconds(17),false,paused));
    }
    [Fact]
    public void SpotifyGraceDoesNotDelayGamingOrOverrideManualSelection()
    {
        var s=new ProfileSelector(); var c=new DeckConfig {ProfileDelaySeconds=3};var t=DateTimeOffset.UtcNow;
        s.Select(c,"explorer",true,t,false,Spotify);s.Select(c,"explorer",true,t.AddSeconds(3),false,Spotify);
        s.Select(c,"explorer",false,t.AddSeconds(4),false,Spotify with {Playing=false});
        s.Select(c,"bf6",false,t.AddSeconds(5),true,Spotify with {Playing=false});
        Assert.Equal("gaming",s.Select(c,"bf6",false,t.AddSeconds(8),true,Spotify with {Playing=false}));
        Assert.False(s.SpotifyTransition);
        Assert.Equal("desktop",s.Select(c with {ProfileMode="desktop"},"bf6",true,t.AddSeconds(9),true,Spotify));
    }
    [Fact]
    public void OtherPlayersDoNotGetSpotifyHold()
    {
        var s=new ProfileSelector();var c=new DeckConfig {ProfileDelaySeconds=0};var t=DateTimeOffset.UtcNow;
        s.Select(c,"explorer",true,t,false,Spotify);
        var browser=Spotify with {App="chrome.exe",Playing=false};
        Assert.Equal("desktop",s.Select(c,"explorer",false,t.AddSeconds(1),false,browser));
        Assert.False(s.SpotifyTransition);
    }
    [Fact]
    public void PendingLyricsUseDedicatedLayoutWithoutAcceptingOldTrackText()
    {
        var media=Spotify with {Title="Next"};
        var state=new DeckState(DateTimeOffset.UtcNow,"music","",new([],"idle"),media,new([],null,"idle"),new(false,"",null,"idle"))
            {Lyrics=new("loading",SpotifyLyrics.Key(media))};
        Assert.True(SpotifyLyrics.Show(state,new()));
        Assert.False(SpotifyLyrics.Show(state with {Lyrics=new("synced",SpotifyLyrics.Key(Spotify),[new(0,"Old synthetic line")])},new()));
    }
}
