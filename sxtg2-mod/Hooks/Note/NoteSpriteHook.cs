using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using sxtg2.Helpers;
using sxtg2.Helpers.UI;
using sxtg2.Loaders;

namespace sxtg2.Hooks.Note
{
    /// <summary>
    /// RhythmGame.NoteGenerator.Generate 후킹으로 생성된 노트에 CustomNotes 폴더의 커스텀 스프라이트를 적용합니다.
    /// </summary>
    public static class NoteSpriteHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            MelonLogger.Msg("[NoteSpriteHook] Initialize() 호출됨");

            if (_isInitialized)
            {
                MelonLogger.Msg("[NoteSpriteHook] 이미 초기화됨, 리턴");
                return;
            }

            try
            {
                CustomNoteSpriteLoader.Initialize();

                var noteGeneratorType = TypeFinderHelper.FindType("RhythmGame.NoteGenerator");
                if (noteGeneratorType == null)
                {
                    MelonLogger.Warning("[NoteSpriteHook] RhythmGame.NoteGenerator 타입을 찾을 수 없습니다.");
                    return;
                }

                var generateMethod = noteGeneratorType.GetMethod("Generate", BindingFlags.Public | BindingFlags.Instance);
                if (generateMethod == null)
                {
                    MelonLogger.Warning("[NoteSpriteHook] NoteGenerator.Generate 메서드를 찾을 수 없습니다.");
                    return;
                }

                var harmony = new HarmonyLib.Harmony("sxtg2.NoteSpriteHook");
                var postfix = new HarmonyMethod(typeof(NoteSpriteHook).GetMethod(nameof(GeneratePostfix), BindingFlags.NonPublic | BindingFlags.Static));
                harmony.Patch(generateMethod, postfix: postfix);

                MelonLogger.Msg("[NoteSpriteHook] NoteGenerator.Generate 후킹 완료");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[NoteSpriteHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        // __result: NoteGenerator.Generate가 반환한 RG_NoteObject 인스턴스 (Assembly-CSharp 미참조라 object로 받음)
        private static void GeneratePostfix(object __result)
        {
            try
            {
                if (__result == null)
                    return;

                var noteObject = ReflectionHelper.GetFirstMemberValueSafe(__result, "gameObject") as GameObject;
                if (noteObject == null)
                    return;

                ApplyCustomSprites(__result, noteObject);
                NoteRendererRecovery.RecoverNoteRenderer(noteObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteSpriteHook] Generate 후처리 오류: {ex.Message}");
            }
        }

        private static void ApplyCustomSprites(object noteInstance, GameObject noteObject)
        {
            string noteType = CustomNoteSpriteLoader.ExtractNoteType(noteObject.name);
            ModLog.Verbose($"[NoteSpriteHook] 노트 생성: name={noteObject.name}, 추출된 타입={(string.IsNullOrEmpty(noteType) ? "(없음)" : noteType)}");

            ApplyToField(noteInstance, "shortNote", CustomNoteSpriteLoader.GetCustomSpriteForNote(noteObject.name));
            ApplyToField(noteInstance, "tailNote", CustomNoteSpriteLoader.GetTailNoteSprite(noteType));
            ApplyToField(noteInstance, "holdTexture", CustomNoteSpriteLoader.GetHoldTextureSprite(noteType));
        }

        private static void ApplyToField(object noteInstance, string fieldName, Sprite sprite)
        {
            if (sprite == null)
                return;

            var rectTransform = ReflectionHelper.GetFieldValueSafe(noteInstance, fieldName) as RectTransform;
            if (rectTransform == null)
                return;

            var image = rectTransform.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }
    }
}
