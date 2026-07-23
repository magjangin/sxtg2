using HarmonyLib;
using MelonLoader;
using System.IO;
using System.Reflection;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using sxtg2.Helpers.Track;
using sxtg2.Helpers;
using sxtg2.Hooks.Manager;
using sxtg2.Loaders;
using sxtg2.Processors;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
        private static bool _isInitialized = false;
        private static bool _bmsInjected = false; // 중복 주입 방지

        /// <summary>
        /// 텍스트 설정 시 호출되는 Prefix 메서드
        /// </summary>
        private static bool TextSetterPrefix(ref object __instance, ref string __0)
        {
            try
            {
                // "커스텀 차트" 텍스트만 로그 출력 (디버깅용)
                if (__0 != null && __0.Contains("커스텀 차트"))
                {
                    string currentScene = SceneManager.GetActiveScene().name;
                    MelonLogger.Msg($"[TextHook] '커스텀 차트' 텍스트 감지! 씬: {currentScene}, 전체 텍스트: {__0}");
                }

                // 플레이 로딩 씬에서만 처리
                string currentScene2 = SceneManager.GetActiveScene().name;
                bool isPlayLoadingScene = currentScene2.Contains("Play") || 
                                         currentScene2.Contains("Loading");

                if (!isPlayLoadingScene)
                {
                    return true; // 다른 씬에서는 그대로 진행
                }

                // "커스텀 차트" 텍스트 감지
                if (__0 != null && __0.Contains("커스텀 차트"))
                {
                    MelonLogger.Msg($"[TextHook] '커스텀 차트' 텍스트 감지! 씬: {currentScene2}");
                    
                    // 중복 주입 방지
                    if (_bmsInjected)
                    {
                        MelonLogger.Msg("[TextHook] 이미 BMS가 주입되었습니다. 건너뜁니다.");
                        return true;
                    }

                    // Track ID 가져오기
                    string trackId = FindCurrentTrackId();
                    if (!string.IsNullOrEmpty(trackId))
                    {
                        MelonLogger.Msg($"[TextHook] 현재 Track ID: {trackId}");
                        LoadAndInjectBmsForTrack(trackId);
                        _bmsInjected = true; // 주입 완료 플래그 설정
                    }
                    else
                    {
                        MelonLogger.Warning("[TextHook] Track ID를 찾을 수 없습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] 텍스트 설정 후킹 중 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }

            return true; // 원본 메서드 계속 실행
        }


        /// <summary>
        /// 씬 변경 시 주입 플래그를 리셋합니다.
        /// </summary>
        public static void ResetInjectionFlag()
        {
            _bmsInjected = false;
            MelonLogger.Msg("[TextHook] 주입 플래그 리셋");
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        /// <summary>
        /// Track ID 기반으로 BMS 파일을 찾아서 로드하고 주입합니다.
        /// </summary>
        public static void LoadAndInjectBmsForTrack(string trackId, string displayName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(trackId))
                {
                    MelonLogger.Warning("[TextHook] Track ID가 비어있습니다.");
                    return;
                }

                using (ModLog.BeginCorrelation("BmsTrackLoad", trackId))
                {

                MelonLogger.Msg($"[TextHook] BMS 파일 로드 시작: Track ID = {trackId}");

                // hwa 폴더 경로
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                if (!Directory.Exists(hwaFolder))
                {
                    MelonLogger.Warning($"[TextHook] hwa 폴더가 존재하지 않습니다: {hwaFolder}");
                    return;
                }

                // DisplayName이 전달되지 않았으면 가져오기
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = GetCurrentTrackDisplayName();
                }
                
                // 앨범 폴더 찾기 (DisplayName 기반)
                string albumFolder = null;
                if (!string.IsNullOrEmpty(displayName))
                {
                    albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);
                    if (albumFolder != null && Directory.Exists(albumFolder))
                    {
                        MelonLogger.Msg($"[TextHook] 앨범 폴더 발견: {Path.GetFileName(albumFolder)}");
                    }
                }

                // BMS 파일 찾기 (앨범 폴더 우선)
                string bmsFile = BmsFileResolver.FindForTrack(trackId, hwaFolder, albumFolder);
                if (bmsFile == null || !File.Exists(bmsFile))
                {
                    MelonLogger.Warning($"[TextHook] BMS 파일을 찾을 수 없습니다. Track ID: {trackId}");
                    return;
                }

                // BMS 파일 파싱 및 주입
                ParseAndInjectBms(bmsFile);

                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] BMS 파일 로드 및 주입 중 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void ParseAndInjectBms(string bmsFile)
        {
            MelonLogger.Msg($"[TextHook] BMS 파일 파싱 시작: {Path.GetFileName(bmsFile)}");
            MelonLogger.Msg($"[TextHook] BMS 파일 전체 경로: {bmsFile}");
            MelonLogger.Msg($"[TextHook] BMS 파일 존재 여부: {File.Exists(bmsFile)}");
            
            var parsedNotes = BmsParser.ParseBmsFile(bmsFile);
            
            if (parsedNotes != null && parsedNotes.Count > 0)
            {
                // CustomChartInjector에 설정
                CustomChartInjector.SetParsedBmsNotes(parsedNotes);
                MelonLogger.Msg($"[TextHook] BMS 파일 파싱 완료: {parsedNotes.Count}개의 노트 발견");
                MelonLogger.Msg($"[TextHook] CustomChartInjector에 노트 설정 완료");
            }
            else
            {
                MelonLogger.Warning("[TextHook] BMS 파일에서 노트를 찾을 수 없습니다.");
                if (parsedNotes == null)
                {
                    MelonLogger.Warning("[TextHook] parsedNotes가 null입니다.");
                }
                else
                {
                    MelonLogger.Warning($"[TextHook] parsedNotes.Count = {parsedNotes.Count}");
                }
            }
        }


    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static string GetCurrentTrackDisplayName()
        {
            try
            {
                if (!ManagerMusicSelectBridge.TryGetCurrentSelectedTrackData(out var trackData))
                    return null;
                return ReflectionHelper.GetFirstMemberValueSafe(trackData, ReflectionMemberNames.TrackData.DisplayName) as string;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextHook] DisplayName 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        private static string FindAlbumFolderByDisplayName(string displayName, string trackId)
        {
            return ManagerMusicSelectHook.ResolveAlbumFolderForTrack(displayName, trackId);
        }

    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        [HarmonyPatch(typeof(UnityEngine.UI.Text), "text", MethodType.Setter)]
        private static class UnityEngineUITextHook
        {
            [HarmonyPrefix]
            private static bool Prefix(object __instance, ref string value)
            {
                return TextSetterPrefix(ref __instance, ref value);
            }
        }

        [HarmonyPatch(typeof(TMPro.TextMeshProUGUI), "text", MethodType.Setter)]
        private static class TMProUGUIHook
        {
            [HarmonyPrefix]
            private static bool Prefix(object __instance, ref string value)
            {
                return TextSetterPrefix(ref __instance, ref value);
            }
        }

        /// <summary>
        /// TextHook 초기화 로직
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            MelonLogger.Msg("[TextHook] Initialize() - 자동 HarmonyPatch 적용 상태");
            _isInitialized = true;
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        /// <summary>
        /// 현재 선택된 Track ID를 찾습니다.
        /// </summary>
        private static string FindCurrentTrackId()
        {
            try
            {
                if (!ManagerMusicSelectBridge.TryGetCurrentSelectedTrackData(out var trackData))
                {
                    MelonLogger.Warning("[TextHook] currentSelectedTrack을 찾을 수 없습니다.");
                    return null;
                }

                ManagerMusicSelectBridge.ReadTrackIdentity(trackData, out var trackId, out _);
                if (string.IsNullOrEmpty(trackId))
                    MelonLogger.Warning("[TextHook] TrackData에서 ID를 읽을 수 없습니다.");
                return trackId;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] Track ID 찾기 중 오류: {ex.Message}");
                return null;
            }
        }
    
    }
}
