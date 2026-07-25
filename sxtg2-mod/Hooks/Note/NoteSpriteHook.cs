using System;
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
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> ShortNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("shortNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> TailNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("tailNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldTexture =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("holdTexture");

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
        private static void GeneratePostfix(RG_NoteObject __result)
        {
            try
            {
                if (__result == null)
                    return;

                var noteObject = __result.gameObject;
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

        private static void ApplyCustomSprites(RG_NoteObject noteInstance, GameObject noteObject)
        {
            string noteType = CustomNoteSpriteLoader.ExtractNoteType(noteObject.name);
            ModLog.Verbose($"[NoteSpriteHook] 노트 생성: name={noteObject.name}, 추출된 타입={(string.IsNullOrEmpty(noteType) ? "(없음)" : noteType)}");

            ApplyToTarget(ShortNote(noteInstance), CustomNoteSpriteLoader.GetCustomSpriteForNote(noteObject.name));
            ApplyToTarget(TailNote(noteInstance), CustomNoteSpriteLoader.GetTailNoteSprite(noteType));
            ApplyToTarget(HoldTexture(noteInstance), CustomNoteSpriteLoader.GetHoldTextureSprite(noteType));
        }

        private static void ApplyToTarget(RectTransform target, Sprite sprite)
        {
            if (sprite == null || target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }
    }
}
