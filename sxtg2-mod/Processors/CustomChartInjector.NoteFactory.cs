using System;
using MelonLoader;
using sxtg2.Hooks.SXGT;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static partial class CustomChartInjector
    {
        /// <summary>
        /// 게임 노트 인스턴스를 생성합니다.
        /// </summary>
        private static object CreateGameNote(BmsParser.ParsedNote parsedNote, Type noteDataType)
        {
            try
            {
                // 노트 타입 결정 (홀드 노트인지 확인 및 실제 타입 선택)
                bool isHoldNote;
                Type actualNoteType;
                DetermineNoteType(parsedNote, noteDataType, out isHoldNote, out actualNoteType);

                // Enum 값 찾기 및 설정
                object nTypeValue, nColorValue;
                FindNoteEnums(parsedNote, actualNoteType, out nTypeValue, out nColorValue);

                // 생성자 호출하여 노트 인스턴스 생성
                object noteInstance = CreateNoteInstance(parsedNote, isHoldNote, actualNoteType, nTypeValue, nColorValue);

                // 필드 설정
                SetNoteFields(noteInstance, parsedNote, actualNoteType, nTypeValue, nColorValue);

                return noteInstance;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 게임 노트 생성 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 노트 타입을 결정합니다 (홀드 노트인지 확인 및 실제 타입 선택).
        /// </summary>
        private static void DetermineNoteType(BmsParser.ParsedNote parsedNote, Type noteDataType, out bool isHoldNote, out Type actualNoteType)
        {
            // 홀드 노트인지 확인
            isHoldNote = parsedNote.NoteType == BmsParser.NoteType.Long || 
                         parsedNote.NoteType == BmsParser.NoteType.Open;

            // 노트 타입에 따라 적절한 타입 선택
            actualNoteType = noteDataType;
            if (isHoldNote)
            {
                var holdNoteType = SXGTDataHook.GetHoldNoteType();
                if (holdNoteType != null)
                {
                    actualNoteType = holdNoteType;
                    MelonLogger.Msg($"[CustomChartInjector] HoldNote 타입 사용: {holdNoteType.FullName}");
                }
            }
            else
            {
                var shortNoteType = SXGTDataHook.GetShortNoteType();
                if (shortNoteType != null)
                {
                    actualNoteType = shortNoteType;
                }
            }
        }

    }
}
