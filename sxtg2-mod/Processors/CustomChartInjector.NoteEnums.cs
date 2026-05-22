using System;
using System.Collections.Generic;
using MelonLoader;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static partial class CustomChartInjector
    {
        private static readonly Dictionary<System.Reflection.Assembly, (Type noteTypeEnum, Type noteColorEnum)> _enumTypeCache =
            new Dictionary<System.Reflection.Assembly, (Type, Type)>();
        private static object _cachedValShort;
        private static object _cachedValHold;
        private static object _cachedValOpen;
        private static object _cachedValRed;
        private static object _cachedValBlue;

        private static void FindNoteEnums(BmsParser.ParsedNote parsedNote, Type actualNoteType, out object nTypeValue, out object nColorValue)
        {
            nTypeValue = null;
            nColorValue = null;

            var assembly = actualNoteType.Assembly;
            if (!_enumTypeCache.TryGetValue(assembly, out var enumTypes))
            {
                enumTypes = CacheEnumsForAssembly(assembly);
                _enumTypeCache[assembly] = enumTypes;
            }

            if (enumTypes.noteTypeEnum != null)
            {
                if (parsedNote.NoteType == BmsParser.NoteType.Normal)
                {
                    nTypeValue = _cachedValShort;
                }
                else if (parsedNote.NoteType == BmsParser.NoteType.Long ||
                         parsedNote.NoteType == BmsParser.NoteType.Open)
                {
                    nTypeValue = _cachedValHold;
                }
            }

            if (enumTypes.noteColorEnum != null)
            {
                if (parsedNote.NoteType == BmsParser.NoteType.Open ||
                    parsedNote.NoteType == BmsParser.NoteType.Close)
                {
                    nColorValue = _cachedValOpen;
                }
                else if (parsedNote.Lane == 4 || parsedNote.Lane == 5)
                {
                    nColorValue = _cachedValRed;
                }
                else
                {
                    nColorValue = _cachedValBlue;
                }
            }
        }

        private static object TryParseEnumMemberIgnoreCase(Type enumType, string name)
        {
            if (enumType == null || !enumType.IsEnum || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var n in Enum.GetNames(enumType))
            {
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(enumType, n);
                }
            }

            return null;
        }

        private static (Type noteTypeEnum, Type noteColorEnum) CacheEnumsForAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                Type noteTypeEnum = null;
                Type noteColorEnum = null;

                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsEnum)
                    {
                        continue;
                    }

                    if (noteTypeEnum == null && (type.Name.Contains("NoteType") || type.Name == "nType"))
                    {
                        noteTypeEnum = type;
                    }
                    else if (noteColorEnum == null && (type.Name.Contains("NoteColor") || type.Name == "nColor"))
                    {
                        noteColorEnum = type;
                    }

                    if (noteTypeEnum != null && noteColorEnum != null)
                    {
                        break;
                    }
                }

                if (_cachedValShort == null && noteTypeEnum != null)
                {
                    _cachedValShort = TryParseEnumMemberIgnoreCase(noteTypeEnum, "SHORT");
                    _cachedValHold = TryParseEnumMemberIgnoreCase(noteTypeEnum, "HOLD");
                }

                if (_cachedValOpen == null && noteColorEnum != null)
                {
                    _cachedValOpen = TryParseEnumMemberIgnoreCase(noteColorEnum, "OPEN");
                    _cachedValRed = TryParseEnumMemberIgnoreCase(noteColorEnum, "RED");
                    _cachedValBlue = TryParseEnumMemberIgnoreCase(noteColorEnum, "BLUE");
                }

                MelonLogger.Msg($"[CustomChartInjector] Enum 타입 캐싱 완료 (Assembly: {assembly.GetName().Name})");
                return (noteTypeEnum, noteColorEnum);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartInjector] Enum 캐싱 실패: {ex.Message}");
                return (null, null);
            }
        }
    }
}
