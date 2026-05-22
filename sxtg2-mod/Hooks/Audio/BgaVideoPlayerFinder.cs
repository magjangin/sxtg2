using MelonLoader;
using UnityEngine;
using UnityEngine.Video;

namespace sxtg2.Hooks.Audio
{
    internal static class BgaVideoPlayerFinder
    {
        public static VideoPlayer Find()
        {
            var videoPlayer = Object.FindObjectOfType<VideoPlayer>();
            if (videoPlayer != null)
            {
                return videoPlayer;
            }

            var allGameObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var go in allGameObjects)
            {
                if (go == null)
                {
                    continue;
                }

                var candidate = go.GetComponent<VideoPlayer>();
                if (candidate != null)
                {
                    MelonLogger.Msg($"[BGAPlayerHook] VideoPlayer 발견 (GameObject: {go.name})");
                    return candidate;
                }
            }

            return null;
        }
    }
}
