using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using RhythmGame;
using sxtg2.Helpers;
using sxtg2.Helpers.UI;
using sxtg2.Loaders;

namespace sxtg2.Hooks.Note
{
    /// <summary>
    /// RhythmGame.NoteGenerator.Generate 후킹으로 생성된 노트에 CustomNotes 폴더의 커스텀 스프라이트를 적용합니다.
    /// </summary>
    [HarmonyPatch(typeof(NoteGenerator))]
    public static class NoteSpriteHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            CustomNoteSpriteLoader.Initialize();
            MelonLogger.Msg("[NoteSpriteHook] Initialize() - 자동 HarmonyPatch 적용 상태");
            _isInitialized = true;
        }

        [HarmonyPatch("Generate")]
        [HarmonyPostfix]
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
