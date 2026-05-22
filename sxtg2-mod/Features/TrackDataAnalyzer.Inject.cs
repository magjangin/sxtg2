using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers.Track;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        private const string LogPrefix = "[TrackDataAnalyzer]";

        /// <summary>
        /// 첫 번째 TrackData를 복사하여 주입합니다. (단일 앨범 지원)
        /// </summary>
        public static void InjectFirstTrackData(object firstTrack, object trackDatasList, FieldInfo trackDatasField, object managerInstance)
        {
            try
            {
                MelonLogger.Msg($"{LogPrefix} === 첫 번째 TrackData 복사 및 주입 ===");

                if (firstTrack == null || trackDatasList == null)
                {
                    MelonLogger.Error($"{LogPrefix} 첫 번째 TrackData 또는 trackDatas 리스트가 null입니다.");
                    return;
                }

                object clonedTrack = TrackDataCloner.CloneTrackData(firstTrack);
                if (clonedTrack == null)
                {
                    MelonLogger.Error($"{LogPrefix} TrackData 복사 실패");
                    return;
                }

                ApplyTrackInfo(clonedTrack, firstTrack.GetType());
                AddToTrackDataList(clonedTrack, trackDatasList);

                MelonLogger.Msg($"{LogPrefix} === TrackData 주입 완료 ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"{LogPrefix} TrackData 주입 중 오류 발생: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// 여러 커스텀 앨범을 주입합니다. (다중 앨범 지원)
        /// </summary>
        public static void InjectMultipleTrackData(object firstTrack, object trackDatasList, FieldInfo trackDatasField, object managerInstance)
        {
            try
            {
                MelonLogger.Msg($"{LogPrefix} === 다중 커스텀 앨범 주입 시작 ===");

                if (firstTrack == null || trackDatasList == null)
                {
                    MelonLogger.Error($"{LogPrefix} 첫 번째 TrackData 또는 trackDatas 리스트가 null입니다.");
                    return;
                }

                Type trackDataType = firstTrack.GetType();
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                MelonLogger.Msg($"{LogPrefix} hwa 폴더 경로: {hwaFolder}");

                if (!Directory.Exists(hwaFolder))
                {
                    MelonLogger.Warning($"{LogPrefix} hwa 폴더가 존재하지 않습니다: {hwaFolder}");
                    return;
                }

                MelonLogger.Msg($"{LogPrefix} hwa 폴더 발견!");

                var albumFolders = GetAlbumFolders(hwaFolder);
                int folderSuccessCount = 0;
                int allBmsFileCount = 0;

                foreach (var albumFolder in albumFolders)
                {
                    if (TryInjectTrackFromAlbumFolder(firstTrack, trackDataType, trackDatasList, albumFolder, out var bmsFileCount))
                    {
                        folderSuccessCount++;
                    }
                    allBmsFileCount += bmsFileCount;
                }

                MelonLogger.Msg($"{LogPrefix} === 다중 앨범 주입 완료 ===");
                MelonLogger.Msg($"  폴더 트랙: {folderSuccessCount}/{allBmsFileCount}개 추가");
                MelonLogger.Msg($"  총 추가된 트랙: {folderSuccessCount}개");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"{LogPrefix} 다중 앨범 주입 중 오류 발생: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static bool TryInjectTrackFromAlbumFolder(
            object firstTrack,
            Type trackDataType,
            object trackDatasList,
            string albumFolder,
            out int bmsFileCount)
        {
            bmsFileCount = 0;
            string albumName = Path.GetFileName(albumFolder);
            if (string.IsNullOrEmpty(albumName))
                albumName = "Root";

            MelonLogger.Msg($"  앨범 처리 중: {albumName}");

            try
            {
                var bmsFiles = BmsFileResolver.FindInFolder(albumFolder);

                bmsFileCount = bmsFiles.Count;
                if (bmsFiles.Count == 0)
                {
                    MelonLogger.Msg("    BMS 파일 없음, 건너뜀");
                    return false;
                }

                var bmsFile = bmsFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(bmsFile);
                MelonLogger.Msg($"    BMS 파일: {Path.GetFileName(bmsFile)} (앨범: {albumName})");

                object clonedTrack = TrackDataCloner.CloneTrackData(firstTrack);
                if (clonedTrack == null)
                {
                    MelonLogger.Warning($"    TrackData 복사 실패: {albumName}");
                    return false;
                }

                FieldInfo idField = trackDataType.GetField("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string originalTrackId = idField?.GetValue(clonedTrack) as string ?? "";
                MelonLogger.Msg($"    원본 Track ID 유지: {originalTrackId}");

                ApplyTrackInfo(clonedTrack, trackDataType, originalTrackId, albumFolder, fileName);
                return AddToTrackDataList(clonedTrack, trackDatasList);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 앨범 폴더 처리 실패 ({albumName}): {ex.Message}");
                return false;
            }
        }

    }
}
