using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Loaders;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        private static void ApplyTrackInfo(object clonedTrack, Type trackDataType, string trackId = null, string albumFolder = null, string bmsFileName = null)
        {
            try
            {
                FieldInfo idField = trackDataType.GetField("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (trackId == null)
                {
                    trackId = idField?.GetValue(clonedTrack) as string ?? "";
                }

                if (albumFolder == null)
                {
                    string gamePath = Path.GetDirectoryName(Application.dataPath);
                    albumFolder = Path.Combine(gamePath, "hwa");
                }

                string searchKey = bmsFileName ?? trackId;
                TrackInfoParser.TrackInfo trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, searchKey);

                FieldInfo displayNameField = trackDataType.GetField("DisplayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (displayNameField != null)
                {
                    string displayName = !string.IsNullOrEmpty(trackInfo.Title) ? trackInfo.Title : ReflectionMemberNames.TextValues.DefaultCustomChartTitle;
                    displayNameField.SetValue(clonedTrack, displayName);
                }

                if (!string.IsNullOrEmpty(trackInfo.Artist))
                {
                    SetFieldValue(clonedTrack, trackDataType, new[] { "Artist", "artist", "Composer", "composer", "작곡가" }, trackInfo.Artist);
                }

                if (trackInfo.Difficulties.Count > 0)
                {
                    SetDifficultyFields(clonedTrack, trackDataType, trackInfo.Difficulties);
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 곡 정보 적용 실패: {ex.Message}");
            }
        }
    }
}
