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

        public static string ExtractNoteType(string gameObjectName)
        {
            if (string.IsNullOrEmpty(gameObjectName))
                return null;

            int firstUnderscore = gameObjectName.IndexOf('_');
            if (firstUnderscore < 0 || firstUnderscore >= gameObjectName.Length - 1)
                return null;

            int secondUnderscore = gameObjectName.IndexOf('_', firstUnderscore + 1);

            if (secondUnderscore > firstUnderscore)
            {
                return gameObjectName.Substring(firstUnderscore + 1, secondUnderscore - firstUnderscore - 1);
            }

            return gameObjectName.Substring(firstUnderscore + 1);
        }

        public static Sprite GetCustomSpriteForNote(string gameObjectName)
        {
            string noteType = ExtractNoteType(gameObjectName);

            if (!string.IsNullOrEmpty(noteType))
            {
                if (LoadedCustomSprites.TryGetValue(noteType, out Sprite exactTypeSprite))
                    return exactTypeSprite;
            }

            if (!string.IsNullOrEmpty(gameObjectName))
            {
                if (LoadedCustomSprites.TryGetValue(gameObjectName, out Sprite fullNameSprite))
                    return fullNameSprite;
            }

            return null;
        }

        public static Sprite GetTailNoteSprite(string noteType)
        {
            if (!string.IsNullOrEmpty(noteType))
            {
                string key = noteType + "_tail";
                if (LoadedCustomSprites.TryGetValue(key, out Sprite tailSprite))
                    return tailSprite;
            }

            if (LoadedCustomSprites.TryGetValue("tailNote", out Sprite genericTailSprite))
                return genericTailSprite;

            return GetCustomSpriteForNote(noteType);
        }

        public static Sprite GetHoldTextureSprite(string noteType)
        {
            if (!string.IsNullOrEmpty(noteType))
            {
                string key = noteType + "_hold";
                if (LoadedCustomSprites.TryGetValue(key, out Sprite holdSprite))
                    return holdSprite;
            }

            if (LoadedCustomSprites.TryGetValue("holdTexture", out Sprite genericHoldSprite))
                return genericHoldSprite;

            return GetCustomSpriteForNote(noteType);
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
