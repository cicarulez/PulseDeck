// Isolated world; only the watch page's main player, never sidebar previews.
(() => {
  function read() {
    const url = new URL(location.href);
    const id = url.searchParams.get('v');
    if (url.pathname !== '/watch' || !/^[\w-]{11}$/.test(id ?? '')) return null;
    const page = document.querySelector('ytd-watch-flexy');
    const player = page?.querySelector('#movie_player');
    const video = player?.querySelector('video.html5-main-video');
    // During SPA navigation the old DOM may still belong to the previous video.
    if (page?.getAttribute('video-id') !== id || !video || video.readyState < 1 ||
        player.classList.contains('ad-showing')) return null;
    const title = page.querySelector('h1.ytd-watch-metadata yt-formatted-string')?.textContent?.trim();
    if (!title) return null;
    const artist = page.querySelector('ytd-watch-metadata #owner #channel-name a')?.textContent?.trim() ?? '';
    return { videoId: id, title: title.slice(0, 300), artist: artist.slice(0, 160),
      playing: !video.paused && !video.ended, positionSeconds: Math.max(0, video.currentTime || 0),
      durationSeconds: Number.isFinite(video.duration) ? Math.max(0, video.duration) : 0,
      playbackRate: video.playbackRate };
  }
  chrome.runtime.onMessage.addListener((message, _sender, respond) => {
    if (message?.type === 'read-player') respond(read());
  });
})();
