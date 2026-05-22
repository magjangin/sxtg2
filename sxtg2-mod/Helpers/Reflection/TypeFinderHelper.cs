using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Helpers
{
    public static class TypeFinderHelper
    {
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();

        public static Type FindType(string typeName)
        {
            // ConcurrentDictionary는 TryGetValue를 사용하는 것이 더 효율적
            if (_typeCache.TryGetValue(typeName, out Type cachedType))
            {
                return cachedType;
            }

            try
            {
                // 모든 로드된 어셈블리에서 타입 찾기
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type foundType = null;

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        foundType = assembly.GetType(typeName);
                        if (foundType != null)
                        {
                            break;
                        }

                        // 네임스페이스 없이 찾기
                        foundType = assembly.GetTypes()
                            .FirstOrDefault(t => t.Name == typeName);
                        if (foundType != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Verbose($"[TypeFinderHelper] 어셈블리 '{assembly.GetName().Name}' 타입 검색 실패: {ex.Message}");
                    }
                }

                if (foundType != null)
                {
                    // ConcurrentDictionary는 AddOrUpdate 또는 TryAdd 사용
                    _typeCache.TryAdd(typeName, foundType);
                }

                return foundType;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TypeFinderHelper] 타입 찾기 실패 ({typeName}): {ex.Message}");
                return null;
            }
        }
    }
}
