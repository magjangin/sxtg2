using System;
using MelonLoader;
using sxtg2.Hooks.SXGT;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static partial class CustomChartInjector
    {
        private static object CreateNoteInstance(BmsParser.ParsedNote parsedNote, bool isHoldNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            object noteInstance = null;

            if (isHoldNote)
            {
                noteInstance = CreateHoldNoteInstance(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }
            else
            {
                noteInstance = CreateShortNoteInstance(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }

            if (noteInstance == null)
            {
                noteInstance = CreateNoteInstanceFallback(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }

            return noteInstance;
        }

        private static object CreateHoldNoteInstance(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            if (parsedNote.Length > 0f)
            {
                var tickTimeArray = GenerateTickTimeArray(parsedNote.Time, parsedNote.Length);

                var durationCtor = SXGTDataHook.GetHoldNoteDurationConstructor();
                if (durationCtor != null && nTypeValue != null && nColorValue != null)
                {
                    try
                    {
                        var instance = durationCtor.Invoke(new object[]
                        {
                            parsedNote.Time,
                            nTypeValue,
                            nColorValue,
                            parsedNote.Lane,
                            parsedNote.Length
                        });
                        MelonLogger.Msg("[CustomChartInjector] HoldNote duration 생성자 사용 성공");
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[CustomChartInjector] HoldNote duration 생성자 호출 실패: {ex.Message}");
                    }
                }

                var tickTimeCtor = SXGTDataHook.GetHoldNoteTickTimeConstructor();
                if (tickTimeCtor != null && nTypeValue != null && nColorValue != null)
                {
                    try
                    {
                        var instance = tickTimeCtor.Invoke(new object[]
                        {
                            parsedNote.Time,
                            nTypeValue,
                            nColorValue,
                            parsedNote.Lane,
                            tickTimeArray
                        });
                        MelonLogger.Msg("[CustomChartInjector] HoldNote tickTime 생성자 사용 성공");
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[CustomChartInjector] HoldNote tickTime 생성자 호출 실패: {ex.Message}");
                    }
                }
            }

            var basicCtor = SXGTDataHook.GetHoldNoteBasicConstructor();
            if (basicCtor != null)
            {
                try
                {
                    var instance = basicCtor.Invoke(new object[] { parsedNote.Time, parsedNote.Lane });

                    SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                    SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                    if (parsedNote.Length > 0f)
                    {
                        SetNoteField(instance, actualNoteType, "duration", parsedNote.Length);
                    }

                    MelonLogger.Msg("[CustomChartInjector] HoldNote 기본 생성자 사용 성공");
                    return instance;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] HoldNote 기본 생성자 호출 실패: {ex.Message}");
                }
            }

            return null;
        }

        private static object CreateShortNoteInstance(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            var shortCtor = SXGTDataHook.GetShortNoteConstructor();
            if (shortCtor != null)
            {
                try
                {
                    var instance = shortCtor.Invoke(new object[] { parsedNote.Time, parsedNote.Lane });

                    SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                    SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                    return instance;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] ShortNote 생성자 호출 실패: {ex.Message}");
                }
            }

            return null;
        }

        private static object CreateNoteInstanceFallback(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            try
            {
                var instance = Activator.CreateInstance(actualNoteType);
                SetNoteField(instance, actualNoteType, "timing", parsedNote.Time);
                SetNoteField(instance, actualNoteType, "targetLane", parsedNote.Lane);
                SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                if (parsedNote.Length > 0f)
                {
                    SetNoteField(instance, actualNoteType, "duration", parsedNote.Length);
                }

                MelonLogger.Warning("[CustomChartInjector] 저장된 생성자를 찾을 수 없어 기본 생성자 사용");
                return instance;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 노트 생성 실패: {ex.Message}");
                return null;
            }
        }
    }
}
