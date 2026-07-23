using MelonLoader;
using UnityEngine;
using UnityEngine.Video;

namespace sxtg2.Hooks.Audio
{
    internal static class BgaVideoPlayerFinder
    {
        public static VideoPlayer Find()
        {
            return Object.FindObjectOfType<VideoPlayer>();
        }
    }
}
