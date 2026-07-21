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

        /// <summary>
        /// TextHook 초기화 로직
        /// </summary>
        public static void Initialize()
        {
            MelonLogger.Msg("[TextHook] Initialize() 호출됨");
            
            if (_isInitialized)
            {
                MelonLogger.Warning("[TextHook] 이미 초기화되었습니다.");
                return;
            }

            try
            {
                MelonLogger.Msg("[TextHook] Harmony 인스턴스 생성 중...");
                var harmony = new HarmonyLib.Harmony("sxtg2.TextHook");
                MelonLogger.Msg("[TextHook] Harmony 인스턴스 생성 완료");

                // TextSetterPrefix 메서드 찾기
                var prefixMethod = typeof(TextHook).GetMethod(nameof(TextSetterPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                if (prefixMethod == null)
                {
                    MelonLogger.Error("[TextHook] TextSetterPrefix 메서드를 찾을 수 없습니다!");
                    return;
                }
                MelonLogger.Msg($"[TextHook] TextSetterPrefix 메서드 찾기 성공: {prefixMethod.Name}");

                // UnityEngine.UI.Text.text 속성 후킹
                HookUnityText(harmony, prefixMethod);

                // TMPro.TextMeshProUGUI.text 속성 후킹
                HookTextMeshPro(harmony, prefixMethod);

                _isInitialized = true;
                MelonLogger.Msg("[TextHook] 초기화 완료!");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void HookUnityText(HarmonyLib.Harmony harmony, MethodInfo prefixMethod)
        {
            MelonLogger.Msg("[TextHook] UnityEngine.UI.Text 타입 확인 중...");
            var textType = typeof(UnityEngine.UI.Text);
            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text 타입: {textType.FullName}");
            
            var textProperty = textType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProperty == null)
            {
                MelonLogger.Warning("[TextHook] UnityEngine.UI.Text.text 속성을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text 속성 찾기 성공");
            if (textProperty.SetMethod == null)
            {
                MelonLogger.Warning("[TextHook] UnityEngine.UI.Text.text SetMethod가 null입니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text SetMethod 찾기 성공, 패치 시도 중...");
            var prefix = new HarmonyMethod(prefixMethod);
            var patchResult = harmony.Patch(textProperty.SetMethod, prefix: prefix);
            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text 패치 결과: {patchResult}");
            if (patchResult != null)
            {
                MelonLogger.Msg("[TextHook] UnityEngine.UI.Text.text 후킹 완료!");
            }
            else
            {
                MelonLogger.Error("[TextHook] UnityEngine.UI.Text.text 패치 실패!");
            }
        }

        private static void HookTextMeshPro(HarmonyLib.Harmony harmony, MethodInfo prefixMethod)
        {
            MelonLogger.Msg("[TextHook] TMPro.TextMeshProUGUI 타입 찾기 중...");
            var tmproType = Helpers.TypeFinderHelper.FindType("TMPro.TextMeshProUGUI");
            if (tmproType == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI 타입을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI 타입 찾기 성공: {tmproType.FullName}");
            var tmproProperty = tmproType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (tmproProperty == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI.text 속성을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text 속성 찾기 성공");
            if (tmproProperty.SetMethod == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI.text SetMethod가 null입니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text SetMethod 찾기 성공, 패치 시도 중...");
            var prefix = new HarmonyMethod(prefixMethod);
            var patchResult = harmony.Patch(tmproProperty.SetMethod, prefix: prefix);
            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text 패치 결과: {patchResult}");
            if (patchResult != null)
            {
                MelonLogger.Msg("[TextHook] TMPro.TextMeshProUGUI.text 후킹 완료!");
            }
            else
            {
                MelonLogger.Error("[TextHook] TMPro.TextMeshProUGUI.text 패치 실패!");
            }
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
