using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Helpers.Finders;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
        public static void ProcessPendingNoteRemovalAndInjection()
        {
            MelonLogger.Msg("[SXGTDataHook] ProcessPendingNoteRemovalAndInjection 호출됨");

            if (!CustomTrackHelper.IsCustomPlayActive())
            {
                MelonLogger.Msg("[SXGTDataHook] 일반 트랙 - 노트 제거/주입 건너뜀");
                return;
            }

            object sxgtDataInstance = _pendingSXGTDataInstance;
            Type sxgtDataType = _pendingSXGTDataType;

            if (sxgtDataInstance == null || sxgtDataType == null)
            {
                MelonLogger.Msg("[SXGTDataHook] 대기 중인 인스턴스가 없어 직접 찾기 시도...");

                sxgtDataInstance = NoteDataFinder.FindNoteData();
                if (sxgtDataInstance != null)
                {
                    sxgtDataType = sxgtDataInstance.GetType();
                    MelonLogger.Msg($"[SXGTDataHook] NoteDataFinder로 SXGTData 인스턴스를 찾았습니다. (타입: {sxgtDataType.Name})");
                }
                else
                {
                    TryFindFromManagerPlay(ref sxgtDataInstance, ref sxgtDataType);
                }

                if (sxgtDataInstance == null)
                {
                    TryFindFromObjects(ref sxgtDataInstance, ref sxgtDataType);
                }

                if (sxgtDataInstance == null || sxgtDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 인스턴스를 찾을 수 없습니다.");
                    return;
                }
            }
            else
            {
                MelonLogger.Msg("[SXGTDataHook] 대기 중인 SXGTData 인스턴스 사용");
            }

            try
            {
                using (ModLog.BeginCorrelation("SXGTNoteInject", sxgtDataType?.Name ?? "sxgt"))
                {
                MelonLogger.Msg("[SXGTDataHook] 노트 제거 및 주입 시작");
                ProcessNoteRemovalAndInjection(sxgtDataInstance, sxgtDataType);

                if (sxgtDataInstance == _pendingSXGTDataInstance)
                {
                    _pendingSXGTDataInstance = null;
                    _pendingSXGTDataType = null;
                }

                MelonLogger.Msg("[SXGTDataHook] 노트 제거 및 주입 완료");
                }
            }
            catch (Exception ex)
            {
                ModLog.Exception("SXGTDataHook.ProcessPendingNoteRemovalAndInjection", ex);
            }
        }

        private static void TryFindFromManagerPlay(ref object sxgtDataInstance, ref Type sxgtDataType)
        {
            try
            {
                var managerPlayType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.ManagerPlay);
                if (managerPlayType == null)
                    return;

                object managerPlayInstance = GameSingletonFinder.TryFindSingleton(ReflectionMemberNames.Types.ManagerPlay);
                if (managerPlayInstance == null)
                {
                    var findObjectsOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                    if (findObjectsOfTypeMethod != null)
                    {
                        var managers = findObjectsOfTypeMethod.Invoke(null, new object[] { managerPlayType }) as UnityEngine.Object[];
                        if (managers != null && managers.Length > 0)
                            managerPlayInstance = managers[0];
                    }
                }

                if (managerPlayInstance == null)
                    return;

                var fields = managerPlayType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;
                    if (fieldType != null && fieldType.Name == ReflectionMemberNames.Types.SXGTData)
                    {
                        var fieldValue = field.GetValue(managerPlayInstance);
                        if (fieldValue != null)
                        {
                            sxgtDataInstance = fieldValue;
                            sxgtDataType = fieldValue.GetType();
                            MelonLogger.Msg($"[SXGTDataHook] ManagerPlay에서 SXGTData 인스턴스를 찾았습니다. (필드: {field.Name})");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] ManagerPlay에서 찾기 실패: {ex.Message}");
            }
        }

        private static void TryFindFromObjects(ref object sxgtDataInstance, ref Type sxgtDataType)
        {
            sxgtDataType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.SXGTData);
            if (sxgtDataType == null)
                return;

            try
            {
                var findObjectsOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                if (findObjectsOfTypeMethod != null)
                {
                    var objects = findObjectsOfTypeMethod.Invoke(null, new object[] { sxgtDataType }) as UnityEngine.Object[];
                    if (objects != null && objects.Length > 0)
                    {
                        sxgtDataInstance = objects[0];
                        MelonLogger.Msg($"[SXGTDataHook] FindObjectsOfType으로 SXGTData 인스턴스를 찾았습니다. ({objects.Length}개 발견)");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] FindObjectsOfType 호출 실패: {ex.Message}");
            }
        }

        private static void ProcessNoteRemovalAndInjection(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                MelonLogger.Msg("[SXGTDataHook] 원본 노트 제거 및 커스텀 차트 주입 시작");

                var noteDataType = ExtractNoteDataTypeFromLaneData(sxgtDataInstance, sxgtDataType);
                if (noteDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] laneData에서 NoteData 타입을 추출할 수 없습니다.");
                }
                else
                {
                    MelonLogger.Msg($"[SXGTDataHook] NoteData 타입 발견: {noteDataType.FullName}");
                }

                ClearAllNotes(sxgtDataInstance, sxgtDataType);

                if (noteDataType != null)
                {
                    Processors.CustomChartInjector.InjectBmsNotesToLaneData(sxgtDataInstance, sxgtDataType, noteDataType);
                }
                else
                {
                    MelonLogger.Warning("[SXGTDataHook] NoteData 타입을 찾을 수 없어 커스텀 차트 주입을 건너뜁니다.");
                }

            }
            catch (Exception ex)
            {
                ModLog.Exception("SXGTDataHook.ProcessNoteRemovalAndInjection", ex);
            }
        }
    }
}
