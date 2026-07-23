using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Helpers
{
    public static class ReflectionHelper
    {
        /// <summary>
        /// 필드를 안전하게 가져옵니다.
        /// </summary>
        public static FieldInfo GetFieldSafe(Type type, string fieldName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        {
            try
            {
                if (type == null || string.IsNullOrEmpty(fieldName))
                    return null;

                return type.GetField(fieldName, flags);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 필드 값을 안전하게 가져옵니다.
        /// </summary>
        public static object GetFieldValueSafe(object instance, string fieldName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        {
            try
            {
                if (instance == null || string.IsNullOrEmpty(fieldName))
                    return null;

                var type = instance.GetType();
                var field = GetFieldSafe(type, fieldName, flags);
                if (field == null)
                    return null;

                return field.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 후보 멤버명 목록에서 첫 번째로 매칭되는 필드/프로퍼티 값을 가져옵니다.
        /// </summary>
        public static object GetFirstMemberValueSafe(object instance, params string[] memberNames)
        {
            try
            {
                if (instance == null || memberNames == null || memberNames.Length == 0)
                    return null;

                var type = instance.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

                foreach (var memberName in memberNames)
                {
                    if (string.IsNullOrWhiteSpace(memberName))
                        continue;

                    var field = type.GetField(memberName, flags);
                    if (field != null)
                        return field.GetValue(instance);

                    var property = type.GetProperty(memberName, flags);
                    if (property != null && property.CanRead)
                        return property.GetValue(instance, null);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
