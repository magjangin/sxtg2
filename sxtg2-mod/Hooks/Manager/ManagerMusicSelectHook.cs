using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            MelonLogger.Msg("[ManagerMusicSelectHook] Initialize() 호출됨");
            
            if (_isInitialized)
            {
                MelonLogger.Warning("[ManagerMusicSelectHook] 이미 초기화되었습니다.");
                return;
            }

            try
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] 초기화 시작...");
                
                var harmony = new HarmonyLib.Harmony("sxtg2.ManagerMusicSelectHook");

                // ManagerMusicSelect 타입 찾기
                var managerType = ManagerMusicSelectBridge.TryGetManagerMusicSelectType();
                if (managerType == null)
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] ManagerMusicSelect 타입을 찾을 수 없습니다.");
                    return;
                }

                // ChangeTrackCursor 메서드 후킹
                var changeTrackCursorMethod = managerType.GetMethod("ChangeTrackCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (changeTrackCursorMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(ManagerMusicSelectHook).GetMethod(nameof(ChangeTrackCursorPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                    harmony.Patch(changeTrackCursorMethod, postfix: postfix);
                    MelonLogger.Msg("[ManagerMusicSelectHook] ChangeTrackCursor 메서드 후킹 완료");
                }
                else
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] ChangeTrackCursor 메서드를 찾을 수 없습니다.");
                }

                // PlayPreview 메서드 후킹 (커스텀 트랙인 경우 원래 preview 재생 방지)
                var playPreviewMethod = managerType.GetMethod("PlayPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (playPreviewMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(ManagerMusicSelectHook).GetMethod(nameof(PlayPreviewPrefix), BindingFlags.NonPublic | BindingFlags.Static));
                    harmony.Patch(playPreviewMethod, prefix: prefix);
                    MelonLogger.Msg("[ManagerMusicSelectHook] PlayPreview 메서드 후킹 완료");
                }
                else
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] PlayPreview 메서드를 찾을 수 없습니다.");
                }

                _isInitialized = true;
                MelonLogger.Msg("[ManagerMusicSelectHook] 초기화 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void ChangeTrackCursorPostfix(object __instance, int delta)
        {
            try
            {
                InjectThumbnailAndDemo(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] ChangeTrackCursorPostfix 오류: {ex.Message}");
            }
        }


        /// <summary>
        /// 썸네일 및 demo.ogg를 주입합니다.
        /// </summary>
        private static void InjectThumbnailAndDemo(object managerInstance)
        {
            try
            {
                if (!ManagerMusicSelectBridge.TryGetSelectedTrackFromManagerInstance(managerInstance, out object trackData))
                    return;

                ManagerMusicSelectBridge.ReadTrackIdentity(trackData, out string trackId, out string displayName);

                // 커스텀 트랙 확인
                bool isCustomTrack = CustomTrackHelper.IsCustomTrack(trackData);

                if (!isCustomTrack)
                {
                    CustomTrackHelper.ClearSelectedTrack();
                    return;
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 커스텀 트랙 감지: ID={trackId}, DisplayName={displayName}");

                // 앨범 폴더 찾기 (DisplayName 기반)
                string albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);

                // 선택된 트랙 정보 캐싱 (PlayLoading, Result 화면에서 사용)
                CustomTrackHelper.SetSelectedTrack(trackId, displayName, albumFolder);

                // 썸네일 주입 (앨범 폴더 포함)
                LoadCustomThumbnail(trackId, albumFolder);

                // 음악 재생은 PlayPreview에서 처리하므로 여기서는 제거
                // LoadAndPlayDemo(managerInstance, trackId, albumFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] InjectThumbnailAndDemo 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// GameObject의 전체 경로를 반환합니다.
        /// </summary>
        private static string GetGameObjectPath(UnityEngine.GameObject obj)
        {
            try
            {
                if (obj == null)
                    return "null";
                
                string path = obj.name;
                UnityEngine.Transform current = obj.transform.parent;
                
                while (current != null)
                {
                    path = current.name + "/" + path;
                    current = current.parent;
                }
                
                return path;
            }
            catch
            {
                return obj != null ? obj.name : "unknown";
            }
        }

        public static string ResolveAlbumFolderForTrack(string displayName, string trackId)
        {
            return FindAlbumFolderByDisplayName(displayName, trackId);
        }
    }
}
