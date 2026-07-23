using System;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Helpers.Finders
{
    /// <summary>게임 내 SXGTData·레인 노트 등을 찾는 진입점(싱글턴·laneData 위임).</summary>
    public static class NoteDataFinder
    {
        /// <summary>
        /// 게임에서 노트 데이터를 찾습니다.
        /// </summary>
        public static object FindNoteData()
        {
            try
            {
                return GameSingletonFinder.TryFindSingleton(ReflectionMemberNames.Types.SXGTData);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteDataFinder] 노트 데이터 찾기 실패: {ex.Message}");
                return null;
            }
        }
    }
}
