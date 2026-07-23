using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Loaders
{
    /// <summary>
    /// CustomNotes 폴더의 PNG를 읽어 노트 타입별 커스텀 스프라이트를 제공합니다.
    /// </summary>
    public static class CustomNoteSpriteLoader
    {
        private static readonly Dictionary<string, Sprite> _customSprites = new Dictionary<string, Sprite>();
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                string customNoteFolder = GetCustomNoteFolder();

                if (!Directory.Exists(customNoteFolder))
                {
                    Directory.CreateDirectory(customNoteFolder);
                    MelonLogger.Msg($"[CustomNoteSpriteLoader] 커스텀 노트 폴더 생성: {customNoteFolder}");
                }

                LoadCustomSprites(customNoteFolder);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomNoteSpriteLoader] 초기화 실패: {ex.Message}");
            }
        }

        private static string GetCustomNoteFolder()
        {
            string gamePath = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(gamePath, "CustomNotes");
        }

        private static void LoadCustomSprites(string customNoteFolder)
        {
            _customSprites.Clear();

            var pngFiles = Directory.GetFiles(customNoteFolder, "*.png", SearchOption.TopDirectoryOnly);
            foreach (var filePath in pngFiles)
            {
                try
                {
                    var sprite = LoadSpriteFromFile(filePath);
                    if (sprite == null)
                        continue;

                    string key = NormalizeFileName(Path.GetFileNameWithoutExtension(filePath));
                    _customSprites[key] = sprite;
                    MelonLogger.Msg($"[CustomNoteSpriteLoader] 커스텀 노트 스프라이트 로드: {key}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomNoteSpriteLoader] 파일 로드 실패 ({Path.GetFileName(filePath)}): {ex.Message}");
                }
            }

            MelonLogger.Msg($"[CustomNoteSpriteLoader] 총 {_customSprites.Count}개의 커스텀 노트 스프라이트 로드 완료");
        }

        private static Sprite LoadSpriteFromFile(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);

            if (!texture.LoadImage(fileData))
            {
                UnityEngine.Object.Destroy(texture);
                MelonLogger.Warning($"[CustomNoteSpriteLoader] 이미지 로드 실패: {Path.GetFileName(filePath)}");
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(filePath);
            return sprite;
        }

        // "blue"/"BLUE"/"Blue" 등 파일명을 "Blue" 형태로 표준화
        private static string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            string lower = fileName.ToLowerInvariant();
            return lower.Length == 1 ? lower.ToUpperInvariant() : char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        public static Sprite GetCustomSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return null;

            if (_customSprites.TryGetValue(spriteName, out var sprite))
                return sprite;

            var matchedKey = _customSprites.Keys.FirstOrDefault(k => string.Equals(k, spriteName, StringComparison.OrdinalIgnoreCase));
            return matchedKey != null ? _customSprites[matchedKey] : null;
        }

        // 노트 GameObject 이름(예: "Default_Blue(Clone)")에서 타입 추출
        public static string ExtractNoteType(string noteName)
        {
            if (string.IsNullOrEmpty(noteName))
                return "";

            string lower = noteName.ToLowerInvariant();
            if (lower.Contains("_blue"))
                return "Blue";
            if (lower.Contains("_red"))
                return "Red";
            if (lower.Contains("_gate"))
                return "Gate";
            return "";
        }

        // 메인 노트(shortNote)용 스프라이트. Gate는 Blue로 폴백, 그 외 폴백 없음(없으면 null)
        public static Sprite GetCustomSpriteForNote(string noteName)
        {
            if (_customSprites.Count == 0)
                return null;

            string noteType = ExtractNoteType(noteName);
            if (string.IsNullOrEmpty(noteType))
                return null;

            var sprite = GetCustomSprite(noteType);
            if (sprite != null)
                return sprite;

            return noteType == "Gate" ? GetCustomSprite("Blue") : null;
        }

        // 끝노트(tailNote)용 스프라이트. 노트 타입별 우선순위, 폴백 없음(없으면 null)
        public static Sprite GetTailNoteSprite(string noteType) => GetPrioritizedSprite(noteType, "Tail", "TailNote");

        // 홀드 몸통(holdTexture)용 스프라이트. 노트 타입별 우선순위, 폴백 없음(없으면 null)
        public static Sprite GetHoldTextureSprite(string noteType) => GetPrioritizedSprite(noteType, "Hold", "HoldTexture");

        // [Type][Suffix] -> [Suffix][Type] -> [Suffix] -> [genericName] 순서로 탐색
        private static Sprite GetPrioritizedSprite(string noteType, string suffix, string genericName)
        {
            string[] spriteNames = string.IsNullOrEmpty(noteType)
                ? new[] { suffix, genericName }
                : new[] { $"{noteType}{suffix}", $"{suffix}{noteType}", suffix, genericName };

            return spriteNames.Select(GetCustomSprite).FirstOrDefault(s => s != null);
        }
    }
}
