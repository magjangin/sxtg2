using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace sxtg2.Loaders
{
    public static class CustomNoteSpriteLoader
    {
        private const string LogPrefix = "[CustomNoteSpriteLoader]";
        private static readonly Dictionary<string, Sprite> LoadedCustomSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            string gamePath = Path.GetDirectoryName(Application.dataPath);
            string folderPath = Path.Combine(gamePath, "CustomNotes");
            Directory.CreateDirectory(folderPath);

            LoadCustomSpritesFromFolder(folderPath);
        }

        // 노트 GameObject 이름(예: "_Blue(Clone)", "Default_Blue(Clone)")에서 타입을 뽑는다.
        // 이름 끝에 "(Clone)"이 붙으므로 밑줄 위치로 자르지 않고 "_blue" 같은 표식이 들어 있는지로 판별한다.
        public static string ExtractNoteType(string gameObjectName)
        {
            if (string.IsNullOrEmpty(gameObjectName))
                return null;

            string lower = gameObjectName.ToLowerInvariant();
            if (lower.Contains("_blue"))
                return "Blue";
            if (lower.Contains("_red"))
                return "Red";
            if (lower.Contains("_gate"))
                return "Gate";
            return null;
        }

        // 메인 노트(shortNote)용. Gate만 Blue로 폴백하고, 나머지는 없으면 원본 스프라이트를 그대로 둔다.
        public static Sprite GetCustomSpriteForNote(string noteType)
        {
            if (string.IsNullOrEmpty(noteType))
                return null;

            return GetSprite(noteType) ?? (noteType == "Gate" ? GetSprite("Blue") : null);
        }

        // 끝노트(tailNote)/홀드 몸통(holdTexture)용. 없으면 원본을 그대로 둔다(메인 노트 스프라이트로 폴백하지 않음).
        public static Sprite GetTailNoteSprite(string noteType) => GetPrioritizedSprite(noteType, "Tail", "TailNote");

        public static Sprite GetHoldTextureSprite(string noteType) => GetPrioritizedSprite(noteType, "Hold", "HoldTexture");

        // [Type][Suffix] → [Suffix][Type] → [Suffix] → [genericName] 순서로 탐색 (예: RedTail → TailRed → Tail → TailNote)
        private static Sprite GetPrioritizedSprite(string noteType, string suffix, string genericName)
        {
            if (!string.IsNullOrEmpty(noteType))
            {
                Sprite typed = GetSprite(noteType + suffix) ?? GetSprite(suffix + noteType);
                if (typed != null)
                    return typed;
            }

            return GetSprite(suffix) ?? GetSprite(genericName);
        }

        private static Sprite GetSprite(string name)
        {
            return LoadedCustomSprites.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

        private static void LoadCustomSpritesFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                    if (texture.LoadImage(bytes))
                    {
                        string noteName = Path.GetFileNameWithoutExtension(file);
                        Sprite sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f));

                        LoadedCustomSprites[noteName] = sprite;
                        MelonLogger.Msg($"{LogPrefix} 커스텀 노트 스프라이트 로드: {noteName}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"{LogPrefix} 스프라이트 로드 실패 ({Path.GetFileName(file)}): {ex.Message}");
                }
            }

            MelonLogger.Msg($"{LogPrefix} 총 {LoadedCustomSprites.Count}개의 커스텀 노트 스프라이트 로드 완료");
        }
    }
}

namespace sxtg2.Helpers.UI
{
    public static class NoteRendererRecovery
    {
        public static void RecoverNoteRenderer(GameObject noteObject)
        {
            if (noteObject == null)
                return;

            try
            {
                RecoverImage(noteObject);

                var transform = noteObject.transform;
                for (int i = 0; i < transform.childCount; i++)
                {
                    RecoverImage(transform.GetChild(i).gameObject);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteRendererRecovery] 렌더러 복구 오류: {ex.Message}");
            }
        }

        private static void RecoverImage(GameObject target)
        {
            var image = target.GetComponent<Image>();
            if (image == null)
                return;

            image.SetNativeSize();
            image.SetAllDirty();
        }
    }
}
