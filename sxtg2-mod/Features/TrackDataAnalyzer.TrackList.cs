using System;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        private static bool AddToTrackDataList(object clonedTrack, object trackDatasList)
        {
            try
            {
                MelonLogger.Msg($"[AddToTrackDataList] 트랙 추가 시도 중... trackDatasList 타입: {trackDatasList?.GetType().Name}");

                if (trackDatasList is System.Collections.IList list)
                {
                    MelonLogger.Msg($"[AddToTrackDataList] IList로 캐스팅 성공, 현재 크기: {list.Count}");
                    list.Add(clonedTrack);
                    MelonLogger.Msg($"[AddToTrackDataList] IList.Add() 성공, 새 크기: {list.Count}");
                    return true;
                }

                MelonLogger.Msg("[AddToTrackDataList] IList가 아님, Add 메서드 찾기 시도");
                Type listType = trackDatasList.GetType();
                MelonLogger.Msg($"[AddToTrackDataList] 리스트 타입: {listType.FullName}");

                MethodInfo addMethod = listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                if (addMethod != null)
                {
                    MelonLogger.Msg($"[AddToTrackDataList] Add 메서드 발견: {addMethod.Name}");
                    addMethod.Invoke(trackDatasList, new object[] { clonedTrack });
                    MelonLogger.Msg("[AddToTrackDataList] Add 메서드 호출 성공");
                    return true;
                }

                MelonLogger.Error("[AddToTrackDataList] trackDatas 리스트에 Add 메서드를 찾을 수 없습니다.");
                var methods = listType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                MelonLogger.Msg("[AddToTrackDataList] 사용 가능한 메서드들:");
                foreach (var method in methods)
                {
                    MelonLogger.Msg($"  - {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AddToTrackDataList] TrackData 리스트 추가 실패: {ex.Message}");
                MelonLogger.Error($"[AddToTrackDataList] 스택 트레이스: {ex.StackTrace}");
                return false;
            }
        }
    }
}
