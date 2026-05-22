using System;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Helpers.Track
{
    public static class ThumbnailLoader
    {
        /// <summary>
        /// hwa 폴더에서 썸네일 이미지를 로드합니다.
        /// </summary>
        /// <param name="trackId">트랙 ID</param>
        /// <param name="hwaFolder">hwa 폴더 경로 (또는 앨범 폴더 경로)</param>
        /// <returns>로드된 Sprite, 없으면 null</returns>
        public static Sprite LoadThumbnail(string trackId, string hwaFolder)
        {
            if (string.IsNullOrEmpty(hwaFolder) || !Directory.Exists(hwaFolder))
            {
                return null;
            }

            try
            {
                // 썸네일 파일 패턴 (trackId 기반 + 일반 파일명 모두 검색)
                var thumbnailPatterns = new System.Collections.Generic.List<string>();
                
                // 일반 파일명 패턴 (항상 검색)
                thumbnailPatterns.AddRange(new[] {
                    "thumb.png",
                    "thumbnail.png",
                    "jacket.png",
                    "cover.png",
                    "image.png"
                });
                
                // trackId 기반 패턴 (trackId가 있는 경우)
                if (!string.IsNullOrEmpty(trackId))
                {
                    thumbnailPatterns.AddRange(new[] {
                        $"{trackId}_thumb.png",
                        $"{trackId}_thumbnail.png",
                        $"{trackId}_jacket.png",
                        $"{trackId}.png"
                    });
                }

                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaRootFolder = Path.Combine(gamePath, "hwa");
                bool isAlbumFolder = !hwaFolder.Equals(hwaRootFolder, StringComparison.OrdinalIgnoreCase);
                
                string thumbnailFile = null;
                
                // 1. 전달된 폴더(앨범 폴더)에서 우선 검색
                if (isAlbumFolder)
                {
                    MelonLogger.Msg($"[ThumbnailLoader] 앨범 폴더에서 썸네일 검색: {Path.GetFileName(hwaFolder)}");
                    
                    foreach (var pattern in thumbnailPatterns)
                    {
                        string file = Path.Combine(hwaFolder, pattern);
                        if (File.Exists(file))
                        {
                            thumbnailFile = file;
                            MelonLogger.Msg($"[ThumbnailLoader] 앨범 폴더에서 썸네일 발견: {Path.GetFileName(file)}");
                            break;
                        }
                    }
                }
                else
                {
                    // 2. hwa 루트 폴더에서 검색
                    MelonLogger.Msg($"[ThumbnailLoader] hwa 루트 폴더에서 썸네일 검색");
                    foreach (var pattern in thumbnailPatterns)
                    {
                        string file = Path.Combine(hwaFolder, pattern);
                        if (File.Exists(file))
                        {
                            thumbnailFile = file;
                            MelonLogger.Msg($"[ThumbnailLoader] hwa 루트 폴더에서 썸네일 발견: {Path.GetFileName(file)}");
                            break;
                        }
                    }
                }

                // 3. 앨범 폴더 내부에서도 검색 (hwa 루트인 경우)
                if (thumbnailFile == null && !isAlbumFolder)
                {
                    var albumFolders = Directory.GetDirectories(hwaFolder);
                    MelonLogger.Msg($"[ThumbnailLoader] {albumFolders.Length}개의 앨범 폴더 발견");
                    
                    foreach (var albumFolder in albumFolders)
                    {
                        foreach (var pattern in thumbnailPatterns)
                        {
                            string file = Path.Combine(albumFolder, pattern);
                            if (File.Exists(file))
                            {
                                thumbnailFile = file;
                                MelonLogger.Msg($"[ThumbnailLoader] 앨범 폴더 '{Path.GetFileName(albumFolder)}'에서 썸네일 발견: {Path.GetFileName(file)}");
                                break;
                            }
                        }
                        if (thumbnailFile != null) break;
                    }
                }

                // 썸네일을 찾지 못한 경우 기본 처리
                if (thumbnailFile == null)
                {
                    MelonLogger.Msg("[ThumbnailLoader] 썸네일 파일을 찾을 수 없습니다.");
                }

                if (thumbnailFile == null)
                {
                    MelonLogger.Msg("[ThumbnailLoader] 썸네일 파일을 찾을 수 없습니다.");
                    return null;
                }

                // 일반 파일 로드
                return LoadThumbnailFromFile(thumbnailFile);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ThumbnailLoader] 썸네일 로드 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 일반 파일에서 썸네일을 로드합니다.
        /// </summary>
        private static Sprite LoadThumbnailFromFile(string thumbnailFile)
        {
            try
            {
                byte[] imageData = File.ReadAllBytes(thumbnailFile);
                Texture2D texture = new Texture2D(2, 2);
                
                if (texture.LoadImage(imageData))
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    MelonLogger.Msg($"[ThumbnailLoader] 썸네일 로드 완료: {texture.width}x{texture.height}");
                    return sprite;
                }
                else
                {
                    MelonLogger.Warning($"[ThumbnailLoader] 이미지 로드 실패: {Path.GetFileName(thumbnailFile)}");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ThumbnailLoader] 파일 썸네일 로드 실패: {ex.Message}");
                return null;
            }
        }

    }
}

















