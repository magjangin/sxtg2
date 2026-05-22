using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using sxtg2.Loaders;
using sxtg2.Helpers.Track;

namespace sxtg2.Helpers.Screen
{
    public static class ResultScreenHelper
    {
        /// <summary>
        /// 결과 화면의 Starting Point 자켓 이미지만 설정합니다.
        /// 캐싱된 트랙 정보를 사용합니다.
        /// </summary>
        public static void SetResultScreenJacketImage()
        {
            try
            {
                MelonLogger.Msg("[ResultScreenHelper] 결과 화면 자켓 이미지 설정 시작");

                // 캐싱된 트랙 정보 확인
                if (!CustomTrackHelper.HasSelectedTrack())
                {
                    MelonLogger.Msg("[ResultScreenHelper] 캐싱된 커스텀 트랙 정보가 없습니다. (기본 트랙일 수 있음)");
                    return;
                }

                var (trackId, displayName, albumFolder) = CustomTrackHelper.GetSelectedTrack();
                MelonLogger.Msg($"[ResultScreenHelper] 캐싱된 트랙 정보 사용: ID={trackId}, DisplayName={displayName}");

                // 자켓 이미지 찾기
                var jacketImages = FindResultJacketImages();
                
                if (jacketImages.Count == 0)
                {
                    MelonLogger.Warning("[ResultScreenHelper] 자켓 이미지를 찾을 수 없습니다.");
                    return;
                }

                MelonLogger.Msg($"[ResultScreenHelper] {jacketImages.Count}개의 자켓 이미지 발견");

                // 썸네일 로드
                Sprite sprite = LoadThumbnailSprite(trackId, albumFolder);

                if (sprite != null)
                {
                    foreach (var jacketImage in jacketImages)
                    {
                        try
                        {
                            if (jacketImage != null)
                            {
                                jacketImage.sprite = sprite;
                                MelonLogger.Msg($"[ResultScreenHelper] 자켓 이미지 설정 완료: {jacketImage.gameObject.name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[ResultScreenHelper] 자켓 이미지 설정 중 오류: {ex.Message}");
                        }
                    }
                }
                else
                {
                    MelonLogger.Msg("[ResultScreenHelper] 썸네일을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultScreenHelper] 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Result 화면의 자켓 이미지를 찾습니다.
        /// Canvas/Result UI/Track Data Indicator/Jacket Mask/Jacket 만 대상
        /// </summary>
        private static List<Image> FindResultJacketImages()
        {
            var targetImages = new List<Image>();
            
            try
            {
                Image[] allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
                
                foreach (var image in allImages)
                {
                    try
                    {
                        if (image == null)
                            continue;
                        
                        string imageName = image.gameObject.name;
                        string gameObjectPath = GetGameObjectPath(image.gameObject);
                        
                        // "Jacket" 이름 + "Track Data Indicator" 경로에 있는 것만
                        if (imageName.Equals("Jacket", StringComparison.OrdinalIgnoreCase) &&
                            gameObjectPath.Contains("Track Data Indicator"))
                        {
                            targetImages.Add(image);
                            MelonLogger.Msg($"[ResultScreenHelper] 자켓 발견: {gameObjectPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ResultScreenHelper] 자켓 Image 순회 중 오류: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultScreenHelper] 이미지 검색 중 오류: {ex.Message}");
            }

            return targetImages;
        }

        /// <summary>
        /// 썸네일 스프라이트를 로드합니다.
        /// </summary>
        private static Sprite LoadThumbnailSprite(string trackId, string albumFolder)
        {
            Sprite sprite = null;

            // 앨범 폴더에서 먼저 검색
            if (!string.IsNullOrEmpty(albumFolder) && Directory.Exists(albumFolder))
            {
                sprite = ThumbnailLoader.LoadThumbnail(trackId, albumFolder);
            }

            // 앨범 폴더에서 못 찾으면 hwa 루트 폴더에서 검색
            if (sprite == null)
            {
                string hwaFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "hwa");
                if (Directory.Exists(hwaFolder))
                {
                    sprite = ThumbnailLoader.LoadThumbnail(trackId, hwaFolder);
                }
            }

            return sprite;
        }

        /// <summary>
        /// GameObject의 전체 경로를 가져옵니다.
        /// </summary>
        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return "";

            string path = obj.name;
            Transform current = obj.transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
    }
}

















