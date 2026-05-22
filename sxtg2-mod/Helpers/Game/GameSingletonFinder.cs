using System.Reflection;

namespace sxtg2.Helpers
{
    /// <summary>
    /// Instance 속성/필드 또는 정적 Find()로 노출되는 게임 싱글톤을 찾습니다.
    /// </summary>
    public static class GameSingletonFinder
    {
        private static readonly BindingFlags SingletonFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static object TryFindSingleton(string typeShortName)
        {
            if (string.IsNullOrEmpty(typeShortName))
                return null;

            try
            {
                var type = TypeFinderHelper.FindType(typeShortName);
                if (type == null)
                    return null;

                var instanceProp = type.GetProperty(ReflectionMemberNames.GameSingleton.InstanceMember, SingletonFlags);
                if (instanceProp != null)
                    return instanceProp.GetValue(null);

                var instanceField = type.GetField(ReflectionMemberNames.GameSingleton.InstanceMember, SingletonFlags);
                if (instanceField != null)
                    return instanceField.GetValue(null);

                var findMethod = type.GetMethod(ReflectionMemberNames.GameSingleton.FindMethod, SingletonFlags);
                if (findMethod != null)
                    return findMethod.Invoke(null, null);

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
