using System;
using System.Reflection;
using UnityEngine;

namespace sxtg2.Helpers
{
    /// <summary>
    /// ManagerMusicSelect 인스턴스·선택 트랙·트랙 ID 조회를 한곳에서 수행합니다.
    /// </summary>
    public static class ManagerMusicSelectBridge
    {
        private static readonly BindingFlags ManagerBinding =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static readonly BindingFlags InstanceBinding =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static Type TryGetManagerMusicSelectType()
        {
            return TypeFinderHelper.FindType(ReflectionMemberNames.Types.ManagerMusicSelect);
        }

        public static object TryGetManagerInstance(Type managerType)
        {
            if (managerType == null)
                return null;

            var instanceProp = managerType.GetProperty(ReflectionMemberNames.ManagerMusicSelect.Instance, ManagerBinding);
            var instanceField = managerType.GetField(ReflectionMemberNames.ManagerMusicSelect.Instance, ManagerBinding);

            if (instanceProp != null)
                return instanceProp.GetValue(null);
            if (instanceField != null)
                return instanceField.GetValue(null);

            return UnityEngine.Object.FindObjectOfType(managerType);
        }

        public static object TryGetCurrentSelectedTrack(Type managerType, object instance)
        {
            if (managerType == null || instance == null)
                return null;

            var trackProp = managerType.GetProperty(ReflectionMemberNames.ManagerMusicSelect.CurrentSelectedTrack, InstanceBinding);
            var trackField = managerType.GetField(ReflectionMemberNames.ManagerMusicSelect.CurrentSelectedTrack, InstanceBinding);

            if (trackProp != null)
                return trackProp.GetValue(instance);
            if (trackField != null)
                return trackField.GetValue(instance);

            return null;
        }

        public static bool TryGetCurrentSelectedTrackData(out object trackData)
        {
            trackData = null;
            var managerType = TryGetManagerMusicSelectType();
            if (managerType == null)
                return false;

            var instance = TryGetManagerInstance(managerType);
            if (instance == null)
                return false;

            trackData = TryGetCurrentSelectedTrack(managerType, instance);
            return trackData != null;
        }

        public static bool TryGetSelectedTrackFromManagerInstance(object managerInstance, out object trackData)
        {
            trackData = null;
            if (managerInstance == null)
                return false;

            var managerType = managerInstance.GetType();
            var trackProp = managerType.GetProperty(ReflectionMemberNames.ManagerMusicSelect.CurrentSelectedTrack, InstanceBinding);
            var trackField = managerType.GetField(ReflectionMemberNames.ManagerMusicSelect.CurrentSelectedTrack, InstanceBinding);

            if (trackProp != null)
                trackData = trackProp.GetValue(managerInstance);
            else if (trackField != null)
                trackData = trackField.GetValue(managerInstance);

            return trackData != null;
        }

        public static void ReadTrackIdentity(object trackData, out string trackId, out string displayName)
        {
            trackId = null;
            displayName = null;
            if (trackData == null)
                return;

            trackId = ReflectionHelper.GetFirstMemberValueSafe(trackData, ReflectionMemberNames.TrackData.Id) as string;
            displayName = ReflectionHelper.GetFirstMemberValueSafe(trackData, ReflectionMemberNames.TrackData.DisplayName) as string;
        }
    }
}
