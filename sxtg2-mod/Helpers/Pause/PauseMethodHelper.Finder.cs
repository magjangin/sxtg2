using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Hooks.Manager;

namespace sxtg2.Helpers
{
    public static partial class PauseMethodHelper
    {
        private static MethodInfo _cachedFindObjectOfTypeGenericMethodDefinition;
        private static MethodInfo _cachedFindObjectsOfTypeByTypeMethod;
        private static MethodInfo _cachedResourcesFindObjectsOfTypeAllGenericMethodDefinition;

        private static object FindPauseInstance(Type pauseType)
        {
            if (pauseType == null) return null;

            object pauseInstance = TryFindByGenericObjectOfType(pauseType);
            if (pauseInstance != null) return pauseInstance;

            pauseInstance = TryFindByFindObjectsOfType(pauseType);
            if (pauseInstance != null) return pauseInstance;

            pauseInstance = TryFindBySingleton(pauseType);
            if (pauseInstance != null) return pauseInstance;

            pauseInstance = TryFindByGameObjects(pauseType);
            if (pauseInstance != null) return pauseInstance;

            return TryFindByResources(pauseType);
        }

        private static object TryFindByGenericObjectOfType(Type pauseType)
        {
            if (_cachedFindObjectOfTypeGenericMethodDefinition == null)
            {
                var methods = typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static);
                _cachedFindObjectOfTypeGenericMethodDefinition = methods.FirstOrDefault(
                    m => m.Name == "FindObjectOfType" && m.IsGenericMethod && m.GetParameters().Length == 0);
            }

            if (_cachedFindObjectOfTypeGenericMethodDefinition == null)
                return null;

            try
            {
                var genericMethod = _cachedFindObjectOfTypeGenericMethodDefinition.MakeGenericMethod(pauseType);
                var instance = genericMethod.Invoke(null, null);
                if (instance != null)
                {
                    MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (FindObjectOfType<T>)");
                    return instance;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} FindObjectOfType<T> 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            return null;
        }

        private static object TryFindByFindObjectsOfType(Type pauseType)
        {
            try
            {
                if (_cachedFindObjectsOfTypeByTypeMethod == null)
                {
                    _cachedFindObjectsOfTypeByTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                }

                if (_cachedFindObjectsOfTypeByTypeMethod != null)
                {
                    var objects = _cachedFindObjectsOfTypeByTypeMethod.Invoke(null, new object[] { pauseType }) as UnityEngine.Object[];
                    if (objects != null && objects.Length > 0)
                    {
                        MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (FindObjectsOfType, {objects.Length}개 발견)");
                        return objects[0];
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} FindObjectsOfType 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            return null;
        }

        private static object TryFindBySingleton(Type pauseType)
        {
            try
            {
                var instanceProperty = pauseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (instanceProperty != null && instanceProperty.CanRead)
                {
                    var instance = instanceProperty.GetValue(null);
                    if (instance != null)
                    {
                        MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (Instance 속성)");
                        return instance;
                    }
                }
                else
                {
                    var instanceField = pauseType.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (instanceField != null)
                    {
                        var instance = instanceField.GetValue(null);
                        if (instance != null)
                        {
                            MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (Instance 필드)");
                            return instance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} Instance 속성/필드 확인 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            return null;
        }

        private static object TryFindByGameObjects(Type pauseType)
        {
            try
            {
                var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                MelonLogger.Msg($"{LogPrefix} 씬의 GameObject 검색 중... ({allGameObjects.Length}개)");

                foreach (var go in allGameObjects)
                {
                    if (go == null) continue;

                    var component = go.GetComponent(pauseType);
                    if (component != null)
                    {
                        MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (GameObject: {go.name})");
                        return component;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} GameObject 순회 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            return null;
        }

        private static object TryFindByResources(Type pauseType)
        {
            try
            {
                if (_cachedResourcesFindObjectsOfTypeAllGenericMethodDefinition == null)
                {
                    _cachedResourcesFindObjectsOfTypeAllGenericMethodDefinition = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethod && m.GetParameters().Length == 0);
                }

                if (_cachedResourcesFindObjectsOfTypeAllGenericMethodDefinition != null)
                {
                    var genericMethod = _cachedResourcesFindObjectsOfTypeAllGenericMethodDefinition.MakeGenericMethod(pauseType);
                    var objects = genericMethod.Invoke(null, null) as UnityEngine.Object[];
                    if (objects != null && objects.Length > 0)
                    {
                        MelonLogger.Msg($"{LogPrefix} RG_PS_Pause 인스턴스 발견 (Resources.FindObjectsOfTypeAll, {objects.Length}개 발견)");
                        return objects[0];
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} Resources.FindObjectsOfTypeAll 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            return null;
        }

        private static string FindAlbumFolderByDisplayName(string displayName, string trackId)
        {
            return ManagerMusicSelectHook.ResolveAlbumFolderForTrack(displayName, trackId);
        }
    }
}
