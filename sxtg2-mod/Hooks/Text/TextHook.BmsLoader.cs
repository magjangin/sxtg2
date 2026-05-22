using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using sxtg2.Loaders;
using sxtg2.Processors;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
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


    }
}















