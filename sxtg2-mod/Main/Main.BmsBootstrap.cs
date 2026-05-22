using System;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;
using sxtg2.Loaders;
using sxtg2.Processors;

namespace sxtg2
{
    public partial class Main
    {
        /// <summary>
        /// hwa 폴더에서 BMS 파일을 스캔하고 파싱합니다.
        /// 폴더 기반 BMS 파일만 지원합니다.
        /// </summary>
        private void ScanAndParseBmsFiles()
        {
            try
            {
                // hwa 폴더 경로
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, ReflectionMemberNames.Paths.HwaFolder);

                using (ModLog.BeginCorrelation("BmsScan", ReflectionMemberNames.Paths.HwaFolder))
                {

                if (!Directory.Exists(hwaFolder))
                {
                    MelonLogger.Msg($"[Main] hwa 폴더가 존재하지 않습니다. 자동 생성합니다: {hwaFolder}");
                    Directory.CreateDirectory(hwaFolder);
                    MelonLogger.Msg($"[Main] hwa 폴더 생성 완료: {hwaFolder}");
                }

                MelonLogger.Msg("[Main] === BMS 파일 스캔 시작 ===");
                var allBmsFiles = BmsFileResolver.FindRootAndAlbumBmsFiles(hwaFolder, "[Main]");

                // 발견된 BMS 파일 수 확인
                if (allBmsFiles.Count == 0)
                {
                    MelonLogger.Msg("[Main] hwa 폴더에서 BMS 파일을 찾을 수 없습니다.");
                    return;
                }

                MelonLogger.Msg($"[Main] 총 {allBmsFiles.Count}개의 BMS 파일 발견, 파싱 시작...");

                // 모든 BMS 파일 파싱 및 통계 수집
                var fileStats = new System.Collections.Generic.List<(string fileName, string folder, BmsParser.ParseResult result)>();
                var successCount = 0;
                var failCount = 0;

                // BMS 파일 파싱
                MelonLogger.Msg($"[Main] === BMS 파일 파싱 시작 ({allBmsFiles.Count}개) ===");
                foreach (var bmsFile in allBmsFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(bmsFile);
                        var folder = Path.GetDirectoryName(bmsFile);
                        var folderName = folder != null && folder != hwaFolder ? Path.GetFileName(folder) : "루트";

                        var parseResult = BmsParser.ParseBmsFileWithStatistics(bmsFile);
                        if (parseResult != null)
                        {
                            fileStats.Add((fileName, folderName, parseResult));
                            successCount++;
                            MelonLogger.Msg($"[Main] ✓ BMS 파싱 성공: [{folderName}] {fileName} ({parseResult.Statistics.TotalNotes}개 노트)");
                        }
                        else
                        {
                            failCount++;
                            MelonLogger.Warning($"[Main] ✗ BMS 파싱 실패: [{folderName}] {fileName} (결과가 null)");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        MelonLogger.Error($"[Main] ✗ BMS 파싱 예외: {Path.GetFileName(bmsFile)} - {ex.Message}");
                    }
                }

                // 파싱 결과 요약
                MelonLogger.Msg("[Main] === BMS 파싱 완료 ===");
                MelonLogger.Msg($"[Main] 성공: {successCount}개, 실패: {failCount}개, 총 {successCount + failCount}개");

                // 통계 출력
                MelonLogger.Msg("[Main] === BMS 파일 파싱 결과 ===");
                foreach (var (fileName, folderName, result) in fileStats)
                {
                    var stats = result.Statistics;
                    MelonLogger.Msg($"[Main] [{folderName}] {fileName}:");
                    MelonLogger.Msg($"[Main]   - 총 노트: {stats.TotalNotes}개");
                    MelonLogger.Msg($"[Main]   - 일반 노트(01): {stats.NormalNotes}개");
                    MelonLogger.Msg($"[Main]   - 홀드 노트(02): {stats.LongNotes}개");
                    MelonLogger.Msg($"[Main]   - 오픈 노트(04): {stats.OpenNotes}개");

                    if (stats.MissingEndNotes.Count > 0)
                    {
                        MelonLogger.Warning($"[Main]   - ⚠️ 끝노트 누락: {stats.MissingEndNotes.Count}개");
                        var groupedByLane = stats.MissingEndNotes.GroupBy(m => m.Lane);
                        foreach (var group in groupedByLane)
                        {
                            var laneName = group.Key == 9 ? "레인 9 (오픈)" : $"레인 {group.Key}";
                            MelonLogger.Warning($"[Main]     {laneName}: {group.Count()}개 (시간: {string.Join(", ", group.Select(m => $"{m.Time:F2}"))})");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg("[Main]   - ✓ 모든 홀드 노트에 끝노트가 있습니다");
                    }
                }

                // 첫 번째 파일을 기본으로 사용
                if (fileStats.Count > 0)
                {
                    var firstResult = fileStats[0];
                    CustomChartInjector.SetParsedBmsNotes(firstResult.result.Notes);
                    MelonLogger.Msg($"[Main] 기본 차트로 '{firstResult.fileName}' 사용");
                }
                else
                {
                    MelonLogger.Warning("[Main] 파싱된 BMS 파일이 없습니다.");
                }

                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Main] BMS 파일 스캔/파싱 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
    }
}
