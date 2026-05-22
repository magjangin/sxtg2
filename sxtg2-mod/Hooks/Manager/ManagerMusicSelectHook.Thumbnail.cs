using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        /// <summary>
        /// 커스텀 썸네일을 로드하고 적용합니다.
        /// </summary>
        private static void LoadCustomThumbnail(string trackId, string albumFolder = null)
        {
            try
            {
                if (string.IsNullOrEmpty(albumFolder))
                {
                    string gamePath = Path.GetDirectoryName(Application.dataPath);
                    albumFolder = Path.Combine(gamePath, "hwa");
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 썸네일 검색 시작: Track ID={trackId}, 앨범 폴더={Path.GetFileName(albumFolder)}");

                // ThumbnailLoader 사용 (앨범 폴더 우선)
                Sprite sprite = ThumbnailLoader.LoadThumbnail(trackId, albumFolder);
                
                if (sprite == null)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 썸네일을 찾을 수 없습니다.");
                    return;
                }
                
                MelonLogger.Msg($"[ManagerMusicSelectHook] 썸네일 로드 성공: {sprite.texture.width}x{sprite.texture.height}");

                // 풀사이즈 자켓 이미지 찾기
                var targetImages = FindFullsizeJacketImages();
                
                // 썸네일 적용
                if (targetImages.Count > 0)
                {
                    ApplyThumbnailToImages(targetImages, sprite);
                    MelonLogger.Msg($"[ManagerMusicSelectHook] {targetImages.Count}개의 풀사이즈 자켓 이미지에 썸네일 적용 완료");
                }
                else
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] 풀사이즈 자켓 이미지를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 썸네일 로드 실패: {ex.Message}");
            }
        }

        private static List<Image> FindFullsizeJacketImages()
        {
            var targetImages = new List<Image>();
            
            try
            {
                Image[] allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
                
                foreach (var image in allImages)
                {
                    try
                    {
                        if (image == null || !image.gameObject.activeInHierarchy || !image.enabled)
                            continue;
                        
                        // "Jacket Image"라는 이름을 가진 이미지 중에서 경로에 "Fullsize Jacket"가 포함된 경우만 선택
                        if (image.name.Equals("Jacket Image", StringComparison.OrdinalIgnoreCase))
                        {
                            string gameObjectPath = GetGameObjectPath(image.gameObject);
                            if (gameObjectPath.ToLower().Contains("fullsize jacket"))
                            {
                                targetImages.Add(image);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 개별 이미지 처리 중 오류는 무시하고 계속 진행
                        MelonLogger.Warning($"[ManagerMusicSelectHook] 이미지 처리 중 오류 (무시): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 이미지 검색 중 오류: {ex.Message}");
            }

            return targetImages;
        }

        private static void ApplyThumbnailToImages(List<Image> targetImages, Sprite sprite)
        {
            foreach (var targetImage in targetImages)
            {
                try
                {
                    if (targetImage != null)
                    {
                        targetImage.sprite = sprite;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 썸네일 적용 중 오류 (무시): {ex.Message}");
                }
            }
        }
    }
}









































