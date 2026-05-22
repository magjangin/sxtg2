using System;
using System.Reflection;
using UnityEngine;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static AudioSource FindBgmSource(object managerInstance)
        {
            Type managerType = managerInstance.GetType();
            FieldInfo bgmSourceField = managerType.GetField("bgmSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo bgmSourceProp = managerType.GetProperty("bgmSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            AudioSource bgmSource = null;
            if (bgmSourceField != null)
            {
                bgmSource = bgmSourceField.GetValue(managerInstance) as AudioSource;
            }
            else if (bgmSourceProp != null)
            {
                bgmSource = bgmSourceProp.GetValue(managerInstance) as AudioSource;
            }

            return bgmSource ?? UnityEngine.Object.FindObjectOfType<AudioSource>();
        }
    }
}
