using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace sxtg2.Helpers.UI
{
    /// <summary>
    /// 노트 스프라이트를 런타임에 교체한 뒤 Unity UI가 즉시 갱신되지 않는 문제를 강제로 해소합니다.
    /// </summary>
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
