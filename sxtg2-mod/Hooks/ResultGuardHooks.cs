using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using RhythmGame.SixtarServer;
using sxtg2.Features;
using sxtg2.Helpers;
using sxtg2.Models;

namespace sxtg2.Hooks
{
    /// <summary>
    /// 결과 화면의 기록 저장과 랭킹 전송을 막는다.
    /// ManagerResult.ComparePlayResultHighScore/PostRequestPlayResult 래퍼를 통째로 건너뛰면 래퍼 끝에 있는
    /// 화면 갱신(베스트 점수 표시, ManagerResult.cs:300-301)까지 같이 사라진다. 그래서 실제로 기록을 쓰고
    /// 서버로 보내는 말단 메서드만 막는다: 저장은 UserAccountModule.SavePlayData, 전송은 LyrebirdServer.PostUserScore.
    /// 둘 다 원본에서 ManagerResult 말고는 부르는 곳이 없다.
    /// </summary>
    [HarmonyPatch]
    public static class ResultSaveBlockHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var save = AccessTools.Method(typeof(UserAccountModule), nameof(UserAccountModule.SavePlayData));
            if (save != null)
                yield return save;
            else
                ModLog.Warning("[ResultSaveBlock] UserAccountModule.SavePlayData를 찾지 못해 기록 저장 차단이 적용되지 않습니다.");

            var post = AccessTools.Method(typeof(LyrebirdServer), nameof(LyrebirdServer.PostUserScore));
            if (post != null)
                yield return post;
            else
                ModLog.Warning("[ResultSaveBlock] LyrebirdServer.PostUserScore를 찾지 못해 랭킹 전송 차단이 적용되지 않습니다.");
        }

        [HarmonyPrefix]
        private static bool Prefix(MethodBase __originalMethod, object[] __args)
        {
            string reason = GetBlockReason(__args);
            if (reason == null)
                return true;

            ModLog.Msg($"[차단] 하이스코어 및 랭킹 저장 차단({reason}): {__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name}");
            return false;
        }

        /// <summary>
        /// 차단 사유. 오토플레이/올퍼펙트/점수 상한 변경/커스텀 곡은 항상 막고(점수나 곡이 정상 기록이 아니므로),
        /// 그 밖의 원본 곡 플레이는 config.txt의 BlockSave를 따른다. null이면 막지 않는다.
        /// </summary>
        private static string GetBlockReason(object[] args)
        {
            if (ModLog.EnableAutoPlay) return "오토플레이";
            if (ModLog.EnableAllPerfect) return "올퍼펙트";
            if (SaveCustomKeyConfig.IsMaxScoreCustom) return "점수 상한 변경";
            if (IsCustomTrackCall(args)) return "커스텀 곡";
            if (ModLog.BlockSaveBestRanking) return "BlockSave";
            return null;
        }

        /// <summary>SavePlayData(TrackData, TrackPlayData) 또는 PostUserScore(RankData)가 커스텀 곡에 대한 호출인지.</summary>
        private static bool IsCustomTrackCall(object[] args)
        {
            if (args == null)
                return false;

            foreach (object arg in args)
            {
                if (arg is CustomTrackData)
                    return true;

                if (arg is RankData rank && CustomTrackData.IsCustomId(rank.trackWithLevel?.trackID))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 결과 화면(ManagerResult.Start)이 하는 일 중 ResultSaveBlockHook이 못 막는 것을, "기록되면 안 되는 플레이"일 때 막는다:
    /// Steam 업적(PUREBLUE_FIRST, FULLCOMBO_FIRST 등)과 userData의 플레이/실패 횟수, 마지막 플레이 곡.
    /// 오토플레이/올퍼펙트/점수 상한 변경/커스텀 곡일 때만 동작하고, 평범한 원본 곡 플레이는 건드리지 않는다.
    /// </summary>
    [HarmonyPatch]
    public static class ResultTaintHook
    {
        private static bool _tainted;
        private static bool _hasSnapshot;
        private static int _playCount;
        private static int _failCount;
        private static int _sameTrackPlayCount;
        private static string _lastPlayedTrackId;

        [HarmonyPatch(typeof(RhythmGame.Result.ManagerResult), "Start")]
        [HarmonyPrefix]
        private static void StartPrefix(TrackData ___trackData)
        {
            _tainted = false;
            _hasSnapshot = false;

            try
            {
                bool custom = ___trackData is CustomTrackData;
                if (!custom && !ModLog.IsTaintedPlayConfig)
                    return;

                _tainted = true;

                SXGT_UserData userData = UserAccountModule.Instance?.userData;
                if (userData != null)
                {
                    _playCount = userData.playCount;
                    _failCount = userData.failCount;
                    _sameTrackPlayCount = userData.sameTrackPlayCount;
                    _lastPlayedTrackId = userData.lastPlayedTrackID;
                    _hasSnapshot = true;
                }

                ModLog.Msg($"[ResultTaint] 이번 결과는 업적/플레이 횟수에 반영하지 않습니다 ({(custom ? "커스텀 곡" : "오토/올퍼펙트/점수 상한 변경")}).");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[ResultTaint] 결과 화면 사전 처리 실패: {ex.Message}");
            }
        }

        /// <summary>Start가 예외로 끝나도 반드시 실행된다(Postfix는 예외 시 건너뛰어진다).</summary>
        [HarmonyPatch(typeof(RhythmGame.Result.ManagerResult), "Start")]
        [HarmonyFinalizer]
        private static Exception StartFinalizer(Exception __exception)
        {
            try
            {
                if (_tainted && _hasSnapshot)
                {
                    SXGT_UserData userData = UserAccountModule.Instance?.userData;
                    if (userData != null)
                    {
                        userData.playCount = _playCount;
                        userData.failCount = _failCount;
                        userData.sameTrackPlayCount = _sameTrackPlayCount;
                        userData.lastPlayedTrackID = _lastPlayedTrackId;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[ResultTaint] 플레이 횟수 되돌리기 실패: {ex.Message}");
            }

            _tainted = false;
            _hasSnapshot = false;
            return __exception;
        }

        [HarmonyPatch(typeof(UserAccountModule), nameof(UserAccountModule.RequestAchievementUnlock))]
        [HarmonyPrefix]
        private static bool AchievementPrefix(ref bool __result, string __0)
        {
            if (!_tainted)
                return true;

            ModLog.Verbose($"[ResultTaint] 업적 해금 요청을 막았습니다: {__0}");
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// 커스텀 곡 ID가 공식 서버로 나가거나, 원본 곡 목록에 없어서 생기는 오류를 막는다.
    /// </summary>
    [HarmonyPatch]
    public static class ServerGuardHook
    {
        /// <summary>곡을 시작할 때마다 원본이 서버에 플레이 카운트를 올린다(ManagerMusicSelect.MoveToPlayLoadingScene).</summary>
        [HarmonyPatch(typeof(LyrebirdServer), nameof(LyrebirdServer.IncreaseTrackPlayCount))]
        [HarmonyPrefix]
        private static bool IncreaseTrackPlayCountPrefix(TrackWithLevel twl)
        {
            return !CustomTrackData.IsCustomId(twl?.trackID);
        }

        /// <summary>
        /// 커스텀 곡의 랭킹은 서버에 없으므로 묻지 않고, 랭킹 창이 빈 목록으로 열리도록 빈 결과를 바로 돌려준다.
        /// (서버가 새 곡에 대해 빈 목록을 주는 경우와 같은 경로라 원본 UI가 그대로 처리한다.)
        /// </summary>
        [HarmonyPatch(typeof(LyrebirdServer), nameof(LyrebirdServer.GetHighScoreList))]
        [HarmonyPrefix]
        private static bool GetHighScoreListPrefix(TrackWithLevel twl, Action<ScoreList> callback)
        {
            if (!CustomTrackData.IsCustomId(twl?.trackID))
                return true;

            callback?.Invoke(new ScoreList { result = new List<RankData>() });
            return false;
        }

        /// <summary>
        /// 원본의 Util.FindTrackByID는 CSV 곡 목록만 뒤져서 커스텀 곡 ID에 null을 돌려준다. 랭킹 창(RG_RankingView.ShowFetch)이
        /// 그 결과의 .DisplayName을 바로 읽어 NullReferenceException이 나므로, 못 찾았을 때만 커스텀 곡 목록에서 찾아 준다.
        /// </summary>
        [HarmonyPatch(typeof(Util), nameof(Util.FindTrackByID))]
        [HarmonyPostfix]
        private static void FindTrackByIdPostfix(string _id, ref TrackData __result)
        {
            if (__result != null || !CustomTrackData.IsCustomId(_id))
                return;

            __result = TrackDataAnalyzer.FindCustomTrack(_id);
        }
    }
}
