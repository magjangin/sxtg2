using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers.Track;

namespace sxtg2.Helpers
{
    public static partial class PauseMethodHelper
    {
        /// <summary>
        /// ESC 키 입력 시 게임 씬의 모든 이미지 관련 오브젝트를 출력합니다.
        /// </summary>
        private static void LogSXGTDataDetails()
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                MelonLogger.Msg($"{LogPrefix} 일시정지 메뉴 Show 메서드 호출됨 - 모든 이미지 열거");

                var imageType = typeof(UnityEngine.UI.Image);
                var images = UnityEngine.Object.FindObjectsOfType(imageType);
                MelonLogger.Msg($"{LogPrefix} 씬의 모든 Image 검색 중... (총 {images.Length}개)");
                for (int i = 0; i < images.Length; i++)
                {
                    try
                    {
                        var img = images[i] as UnityEngine.UI.Image;
                        if (img != null)
                        {
                            var sprite = img.sprite;
                            var spriteName = sprite != null ? sprite.name : "null";
                            var spriteSize = sprite != null ? $"{sprite.rect.width}x{sprite.rect.height}" : "";
                            var sizeStr = !string.IsNullOrEmpty(spriteSize) ? $", 크기={spriteSize}" : "";
                            MelonLogger.Msg($"{LogPrefix}   Image: {img.gameObject.name}, sprite={spriteName}{sizeStr}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"{LogPrefix}   Image: [읽기 실패: {ex.Message}]");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 이미지 오브젝트 출력 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }
        }

        /// <summary>
        /// 필드 또는 속성 값을 가져옵니다.
        /// </summary>
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

                if (!TryResolveTrackDataForEyecatch(managerPlayInstance, out object trackData))
                    return;

                var trackId = GetFieldOrPropertyValue(trackData, "ID") as string;
                var displayName = GetFieldOrPropertyValue(trackData, "DisplayName") as string;
                if (string.IsNullOrEmpty(trackId))
                    return;

                var sprite = TryLoadEyecatchThumbnailSprite(trackId, displayName);
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

        /// <summary>
        /// Eyecatch Image를 설정합니다. playTrack -> trackData -> bms 순서로 찾습니다.
        /// </summary>
        private static void SetEyecatchImage()
        {
            try
            {
                var managerPlayType = TypeFinderHelper.FindType("ManagerPlay");
                if (managerPlayType == null)
                    return;

                var managerPlayInstance = UnityEngine.Object.FindObjectOfType(managerPlayType);
                if (managerPlayInstance == null)
                    return;

                if (!TryResolveTrackDataForEyecatch(managerPlayInstance, out object trackData))
                    return;

                var trackId = GetFieldOrPropertyValue(trackData, "ID") as string;
                var displayName = GetFieldOrPropertyValue(trackData, "DisplayName") as string;
                if (string.IsNullOrEmpty(trackId))
                    return;

                var eyecatchImage = FindEyecatchUiImageOrNull();
                if (eyecatchImage == null)
                    return;

                var sprite = TryLoadEyecatchThumbnailSprite(trackId, displayName);
                if (sprite != null)
                {
                    eyecatchImage.sprite = sprite;
                    MelonLogger.Msg($"{LogPrefix} Eyecatch Image 설정 완료: {sprite.texture.width}x{sprite.texture.height}");
                }
                else
                {
                    MelonLogger.Msg($"{LogPrefix} 썸네일을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} Eyecatch Image 설정 중 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }
        }

        private static bool TryResolveTrackDataForEyecatch(object managerPlayInstance, out object trackData)
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

        private static UnityEngine.UI.Image FindEyecatchUiImageOrNull()
        {
            return UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>()
                .FirstOrDefault(img => img.gameObject.name.Contains("Eyecatch Image"));
        }

        private static Sprite TryLoadEyecatchThumbnailSprite(string trackId, string displayName)
        {
            string albumFolder = null;
            if (!string.IsNullOrEmpty(displayName))
                albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);

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
