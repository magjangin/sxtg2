using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using sxtg2.Helpers;
using UnityEngine;

namespace sxtg2.Loaders
{
    public static class CustomNoteSpriteLoader
    {
        private const string LogPrefix = "[CustomNoteSpriteLoader]";

        // Unity가 Instantiate한 복제본 이름 끝에 붙이는 접미사. 노트 이름은 `_Blue(Clone)` 형태가 된다.
        private const string CloneSuffix = "(Clone)";

        private static readonly Dictionary<string, Sprite> LoadedCustomSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>로드된 커스텀 스프라이트가 하나라도 있는지. 없으면 노트마다 할 일이 없다.</summary>
        public static bool HasAnySprite => LoadedCustomSprites.Count > 0;

        private static string StripCloneSuffix(string name)
        {
            if (!string.IsNullOrEmpty(name) && name.EndsWith(CloneSuffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - CloneSuffix.Length);

            return name;
        }

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

            // 노트 이름은 `_Blue(Clone)`이라 그대로 자르면 `Blue(Clone)`이 나와 `Blue.png`와 맞지 않았다. 접미사를 뗀다.
            if (secondUnderscore > firstUnderscore)
            {
                return StripCloneSuffix(gameObjectName.Substring(firstUnderscore + 1, secondUnderscore - firstUnderscore - 1));
            }

            return StripCloneSuffix(gameObjectName.Substring(firstUnderscore + 1));
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

                if (LoadedCustomSprites.TryGetValue(StripCloneSuffix(gameObjectName), out Sprite fullNameWithoutCloneSprite))
                    return fullNameWithoutCloneSprite;
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

                        // 예전 안내대로 `Blue(Clone).png`처럼 접미사까지 붙인 파일도 `Blue`로 찾히게 한다.
                        string normalizedName = noteName.Replace(CloneSuffix, "");
                        if (normalizedName.Length > 0 && !LoadedCustomSprites.ContainsKey(normalizedName))
                            LoadedCustomSprites[normalizedName] = sprite;

                        ModLog.Msg($"{LogPrefix} 커스텀 노트 스프라이트 로드: {noteName} ({texture.width}x{texture.height})");
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);
                        ModLog.Warning($"{LogPrefix} PNG로 읽지 못해 건너뜁니다: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warning($"{LogPrefix} 스프라이트 로드 실패 ({Path.GetFileName(file)}): {ex.Message}");
                }
            }

            ModLog.Msg($"{LogPrefix} 총 {LoadedCustomSprites.Count}개의 커스텀 노트 스프라이트 로드 완료");
        }
    }
}
