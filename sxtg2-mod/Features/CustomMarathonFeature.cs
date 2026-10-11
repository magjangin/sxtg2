using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using RhythmGame;
using RhythmGame.MusicSelect;
using RhythmGame.Result;
using UnityEngine;
using UnityEngine.SceneManagement;
using sxtg2.Helpers;
using sxtg2.Models;

namespace sxtg2.Features
{
    /// <summary>
    /// 커스텀 곡 마라톤. 커스텀 곡 하나를 시작하면, 결과창에서 Enter를 누를 때마다 곡 목록을 거치지 않고
    /// 아직 안 친 다음 커스텀 곡을 바로 시작한다. 모든 커스텀 곡을 딱 한 번씩 돌면 목록으로 돌아간다.
    ///
    /// 처음 고른 곡이 1번이고, 나머지 커스텀 곡은 마라톤을 시작할 때마다 무작위로 섞어 겹치지 않게 이어진다.
    /// 리트라이는 같은 곡을 다시 치는 것이라 순번이 넘어가지 않는다. 플레이 중 일시정지에서 목록으로
    /// 나가거나 결과창에서 Esc를 누르면 그 자리에서 마라톤이 끝난다.
    /// </summary>
    public static class CustomMarathon
    {
        private sealed class Run
        {
            public List<CustomTrackData> Tracks;
            public int Current;
            public ELevels Level;
            public EPlayStyle Style;
        }

        private static readonly AccessTools.FieldRef<ManagerResult, bool> Backable =
            AccessTools.FieldRefAccess<ManagerResult, bool>("backable");
        private static readonly Action<ManagerResult> BackToMusicSelect =
            AccessTools.MethodDelegate<Action<ManagerResult>>(AccessTools.Method(typeof(ManagerResult), "BackToMusicSelect"));

        private static readonly System.Random Shuffler = new System.Random();

        private static Run _run;
        private static ManagerResult _result;
        private static float _resultShownAt;
        // 같은 결과창에서 Enter를 연타하거나 자동 진행과 겹쳐도 한 곡만 넘어가게 한다.
        private static int _advancedFromResult;

        private static Texture2D _whiteTex;
        private static GUIStyle _titleStyle;
        private static GUIStyle _bodyStyle;

        internal static bool IsActive => _run != null && SaveCustomKeyConfig.EnableCustomMarathon;

        /// <summary>곡 목록에서 곡을 시작할 때 호출된다. 커스텀 곡이면 그 곡부터 새 마라톤을 준비한다.</summary>
        internal static void Prepare(ManagerMusicSelect musicSelect)
        {
            _run = null;
            if (!(musicSelect.currentSelectedTrack is CustomTrackData selected))
                return;

            var tracks = new List<CustomTrackData>();
            var seenIds = new HashSet<string>();
            foreach (var track in musicSelect.trackDatas.OfType<CustomTrackData>())
            {
                if (seenIds.Add(track.ID))
                    tracks.Add(track);
            }

            int start = tracks.FindIndex(track => track.ID == selected.ID);
            if (start < 0)
                return;

            // 고른 곡은 1번에 두고 나머지만 섞는다(Fisher–Yates). 한 곡이 두 번 나오는 일은 없다.
            var rest = tracks.Where((track, index) => index != start).ToList();
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = Shuffler.Next(i + 1);
                var swap = rest[i];
                rest[i] = rest[j];
                rest[j] = swap;
            }
            rest.Insert(0, tracks[start]);

            _run = new Run
            {
                Tracks = rest,
                Current = 0,
                Level = musicSelect.LevelCursor,
                Style = musicSelect.playStyle
            };

            if (SaveCustomKeyConfig.EnableCustomMarathon)
            {
                ModLog.Msg($"[CustomMarathon] 시작: 커스텀 곡 {_run.Tracks.Count}개를 한 번씩 순회 ({_run.Level}, {_run.Style}) - 1번 \"{selected.DisplayName}\"");
                ModLog.Msg($"[CustomMarathon] 순서(무작위): {string.Join(" → ", _run.Tracks.Select(track => track.DisplayName).ToArray())}");
            }
        }

        internal static void Abort(string reason)
        {
            if (_run != null && SaveCustomKeyConfig.EnableCustomMarathon)
                ModLog.Msg($"[CustomMarathon] 종료 ({reason}) - {_run.Current + 1}/{_run.Tracks.Count}곡까지 진행");
            _run = null;
        }

        internal static void OnResultStarted(ManagerResult result)
        {
            _result = result;
            _resultShownAt = Time.unscaledTime;
        }

        /// <summary>결과창 Enter. 다음 곡으로 넘어갔으면 true(= 원래의 목록 이동을 막음).</summary>
        internal static bool TryAdvance(ManagerResult result)
        {
            if (!IsActive || !Backable(result))
                return false;

            if (_advancedFromResult == result.GetInstanceID())
                return true;

            int next = _run.Current + 1;
            if (next >= _run.Tracks.Count)
            {
                ModLog.Msg($"[CustomMarathon] 완주! 커스텀 곡 {_run.Tracks.Count}개를 모두 한 번씩 쳤습니다.");
                _run = null;
                return false;
            }

            _advancedFromResult = result.GetInstanceID();
            _run.Current = next;
            StartTrack(_run.Tracks[next], _run.Level, _run.Style, result);
            return true;
        }

        /// <summary>
        /// ManagerMusicSelect.MoveToPlayLoadingScene / OnMoveToPlayScene과 같은 순서로 로딩 씬을 거쳐 플레이 씬을 연다.
        /// 원본이 하는 서버 재생 횟수 증가 요청은 커스텀 곡이라 보내지 않는다.
        /// </summary>
        private static void StartTrack(CustomTrackData track, ELevels level, EPlayStyle style, ManagerResult result)
        {
            ModLog.Msg($"[CustomMarathon] {_run.Current + 1}/{_run.Tracks.Count} \"{track.DisplayName}\" 시작");

            UserAccountModule.Instance.userData.lastSelectedSongIndex = track.ID;
            UserAccountModule.Instance.SaveUserData();

            RG_SceneManager.Instance.MoveScene("PlayLoading", (scene, mode) =>
            {
                var loading = RG_SceneManager.Instance.GetManagerFromScene(scene).GetGameObject().GetComponent<PlayLoadingManager>();
                loading.specifiedLoadingClip = track.GetLoadingAnimation();
                loading.OnFinishAction = (playScene, playMode) =>
                {
                    var play = RG_SceneManager.Instance.GetManagerFromScene(playScene).GetGameObject().GetComponent<ManagerPlay>();
                    play.playSceneMode = PlaySceneMode.Defualt;
                    play.debugMode = false;
                    play.Set(track, level, style);
                };
                loading.track = track;
                loading.playLevel = level;
                loading.playStyle = style;

                if (result != null)
                    UnityEngine.Object.Destroy(result);
            });
        }

        internal static void ExitToMusicSelect(ManagerResult result)
        {
            if (!IsActive || result == null || !Backable(result))
                return;

            Abort("결과창에서 Esc");
            BackToMusicSelect(result);
        }

        public static void Update()
        {
            float delay = SaveCustomKeyConfig.CustomMarathonAutoNext;
            if (delay <= 0f || !IsActive || _result == null)
                return;

            // 마지막 곡의 결과창은 완주 화면이므로 저절로 목록으로 넘기지 않는다.
            if (_run.Current + 1 >= _run.Tracks.Count)
                return;

            if (Time.unscaledTime - _resultShownAt >= delay && Backable(_result))
                BackToMusicSelect(_result);
        }

        public static void Draw()
        {
            if (!IsActive)
                return;

            try
            {
                EnsureStyles();

                if (_result == null)
                {
                    DrawLoadingBanner();
                    return;
                }

                int done = _run.Current + 1;
                int total = _run.Tracks.Count;
                bool isLast = done >= total;

                string title = isLast
                    ? $"커스텀 마라톤 완주!  {done} / {total}"
                    : $"커스텀 마라톤  {done} / {total}";
                string body;
                if (isLast)
                {
                    body = "Enter: 곡 목록으로";
                }
                else
                {
                    body = $"다음 곡: {_run.Tracks[done].DisplayName}{UpcomingAfter(done)}\nEnter: 다음 곡    Esc: 그만하기";
                    float delay = SaveCustomKeyConfig.CustomMarathonAutoNext;
                    if (delay > 0f)
                    {
                        float left = Mathf.Max(0f, delay - (Time.unscaledTime - _resultShownAt));
                        body += $"    ({left:0}초 뒤 자동 진행)";
                    }
                }

                const float width = 680f;
                float height = isLast ? 70f : 94f;
                var box = new Rect((Screen.width - width) / 2f, 18f, width, height);

                GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.82f);
                GUI.DrawTexture(box, _whiteTex);
                GUI.color = isLast ? new Color(1f, 0.82f, 0.3f, 1f) : new Color(0.45f, 0.8f, 1f, 1f);
                GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), _whiteTex);
                GUI.color = Color.white;

                GUI.Label(new Rect(box.x, box.y + 8f, box.width, 30f), title, _titleStyle);
                GUI.Label(new Rect(box.x, box.y + 38f, box.width, height - 42f), body, _bodyStyle);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[CustomMarathon] OnGUI 드로우 에러: {ex.Message}");
            }
        }

        // "다음 곡" 뒤에 이어질 곡을 두 개까지 붙여 순서가 보이게 한다. 예: " → press start → rise …"
        private static string UpcomingAfter(int index)
        {
            var names = _run.Tracks.Skip(index + 1).Take(2).Select(track => track.DisplayName).ToArray();
            if (names.Length == 0)
                return "  (마지막 곡)";

            string tail = " → " + string.Join(" → ", names);
            return index + 3 < _run.Tracks.Count ? tail + " …" : tail;
        }

        // 결과창에서 넘어온 직후 로딩 화면 위쪽에 지금 시작하는 곡을 띄운다.
        private static void DrawLoadingBanner()
        {
            if (SceneManager.GetActiveScene().name != "PlayLoading")
                return;

            var track = _run.Tracks[_run.Current];
            string text = $"커스텀 마라톤  {_run.Current + 1} / {_run.Tracks.Count}  ·  {track.DisplayName}";

            const float width = 520f;
            var box = new Rect((Screen.width - width) / 2f, 18f, width, 40f);
            GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.82f);
            GUI.DrawTexture(box, _whiteTex);
            GUI.color = new Color(0.45f, 0.8f, 1f, 1f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), _whiteTex);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x, box.y + 4f, box.width, 36f), text, _titleStyle);
        }

        private static void EnsureStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20
                };
                _titleStyle.normal.textColor = Color.white;
            }

            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 15
                };
                _bodyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            }
        }
    }

    [HarmonyPatch(typeof(ManagerMusicSelect))]
    public static class CustomMarathonMusicSelectHook
    {
        // 곡 목록으로 돌아왔다는 건 마라톤이 끝났거나(완주/중단) 아직 시작 전이라는 뜻이다.
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix()
        {
            CustomMarathon.Abort("곡 목록으로 돌아옴");
        }

        [HarmonyPatch("MoveToPlayLoadingScene")]
        [HarmonyPrefix]
        private static void MoveToPlayLoadingScenePrefix(ManagerMusicSelect __instance)
        {
            try
            {
                CustomMarathon.Prepare(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomMarathon] 준비 실패: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ManagerResult))]
    public static class CustomMarathonResultHook
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(ManagerResult __instance)
        {
            CustomMarathon.OnResultStarted(__instance);
        }

        [HarmonyPatch("BackToMusicSelect")]
        [HarmonyPrefix]
        private static bool BackToMusicSelectPrefix(ManagerResult __instance)
        {
            try
            {
                return !CustomMarathon.TryAdvance(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomMarathon] 다음 곡으로 넘어가지 못해 곡 목록으로 돌아갑니다: {ex}");
                CustomMarathon.Abort("오류");
                return true;
            }
        }

        // 원본 결과창은 Enter(A)만 쓰므로 비어 있는 Esc(B)에 "마라톤 그만하기"를 붙인다.
        [HarmonyPatch("GetKeyMap")]
        [HarmonyPostfix]
        private static void GetKeyMapPostfix(ManagerResult __instance, Dictionary<SixtarInput, Dictionary<KeyState, Action>> __result)
        {
            if (__result == null || !CustomMarathon.IsActive || __result.ContainsKey(SixtarInput.B))
                return;

            __result[SixtarInput.B] = new Dictionary<KeyState, Action>
            {
                { KeyState.DOWN, () => CustomMarathon.ExitToMusicSelect(__instance) }
            };
        }
    }
}
