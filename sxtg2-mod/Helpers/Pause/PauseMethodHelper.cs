using MelonLoader;
using System.IO;
using System.Reflection;
using System;
using UnityEngine;
using sxtg2.Helpers.Track;
using sxtg2.Hooks.Manager;

namespace sxtg2.Helpers
{
    public static partial class PauseMethodHelper
    {
        public static void ApplyCustomPauseJacket()
        {
            SetPauseJacketImage();
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private const string LogPrefix = "[PauseMethodHelper]";

        private static object GetFieldOrPropertyValue(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(instance);
        }

        private static void SetPauseJacketImage()
        {
            try
            {
                var managerPlayType = TypeFinderHelper.FindType("ManagerPlay");
                var pauseType = TypeFinderHelper.FindType("RG_PS_Pause");
                if (managerPlayType == null || pauseType == null)
                    return;

                var managerPlayInstance = UnityEngine.Object.FindObjectOfType(managerPlayType);
                var pauseInstance = UnityEngine.Object.FindObjectOfType(pauseType);
                if (managerPlayInstance == null || pauseInstance == null)
                    return;

                if (!TryResolveTrackDataForPause(managerPlayInstance, out object trackData))
                    return;

                var trackId = GetFieldOrPropertyValue(trackData, "ID") as string;
                var displayName = GetFieldOrPropertyValue(trackData, "DisplayName") as string;
                if (string.IsNullOrEmpty(trackId))
                    return;

                var sprite = TryLoadPauseThumbnailSprite(trackId, displayName);
                if (sprite == null)
                    return;

                var jacketField = pauseType.GetField("jacketImage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var jacketImage = jacketField?.GetValue(pauseInstance) as UnityEngine.UI.Image;
                if (jacketImage == null)
                    return;

                jacketImage.sprite = sprite;
                MelonLogger.Msg($"{LogPrefix} 일시정지 자켓 교체 완료: {sprite.name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 일시정지 자켓 교체 중 오류: {ex.Message}");
            }
        }

        private static bool TryResolveTrackDataForPause(object managerPlayInstance, out object trackData)
        {
            trackData = null;
            var playTrack = GetFieldOrPropertyValue(managerPlayInstance, "playTrack");
            if (playTrack != null)
                trackData = GetFieldOrPropertyValue(playTrack, "trackData") ?? playTrack;

            if (trackData == null)
                trackData = GetFieldOrPropertyValue(managerPlayInstance, "trackData");

            if (trackData == null)
            {
                var bms = GetFieldOrPropertyValue(managerPlayInstance, "bms");
                if (bms != null)
                    trackData = GetFieldOrPropertyValue(bms, "trackData");
            }

            return trackData != null;
        }

        private static Sprite TryLoadPauseThumbnailSprite(string trackId, string displayName)
        {
            string albumFolder = null;
            if (!string.IsNullOrEmpty(displayName))
                albumFolder = ManagerMusicSelectHook.ResolveAlbumFolderForTrack(displayName, trackId);

            if (!string.IsNullOrEmpty(albumFolder) && Directory.Exists(albumFolder))
            {
                MelonLogger.Msg($"{LogPrefix} 앨범 폴더에서 썸네일 검색: {Path.GetFileName(albumFolder)}");
                var fromAlbum = ThumbnailLoader.LoadThumbnail(trackId, albumFolder);
                if (fromAlbum != null)
                    return fromAlbum;
            }

            string hwaFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "hwa");
            if (!Directory.Exists(hwaFolder))
                return null;

            MelonLogger.Msg($"{LogPrefix} hwa 루트 폴더에서 썸네일 검색");
            return ThumbnailLoader.LoadThumbnail(trackId, hwaFolder);
        }
    
    }
}
