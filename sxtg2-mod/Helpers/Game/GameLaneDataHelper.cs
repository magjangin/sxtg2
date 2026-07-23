using System.Reflection;

namespace sxtg2.Helpers
{
    public enum LaneDataAccessFailure
    {
        None,
        InvalidArguments,
        NoLaneField,
        LaneDataNull,
        NoItemProperty
    }

    /// <summary>
    /// SXGTData 인스턴스에서 laneData 및 Dictionary 인덱서(Item)에 접근합니다.
    /// </summary>
    public static class GameLaneDataHelper
    {
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// laneData 객체와 Item 프로퍼티를 한 번에 해석합니다 (주입·삭제 루프용).
        /// </summary>
        public static bool TryGetLaneDataAndItemProperty(
            System.Type sxgtDataType,
            object sxgtDataInstance,
            out object laneData,
            out PropertyInfo itemProp,
            out LaneDataAccessFailure failure)
        {
            laneData = null;
            itemProp = null;
            failure = LaneDataAccessFailure.None;

            if (sxgtDataType == null || sxgtDataInstance == null)
            {
                failure = LaneDataAccessFailure.InvalidArguments;
                return false;
            }

            var laneDataField = sxgtDataType.GetField(ReflectionMemberNames.SXGTDataMembers.LaneData, InstanceFlags);
            if (laneDataField == null)
            {
                failure = LaneDataAccessFailure.NoLaneField;
                return false;
            }

            laneData = laneDataField.GetValue(sxgtDataInstance);
            if (laneData == null)
            {
                failure = LaneDataAccessFailure.LaneDataNull;
                return false;
            }

            itemProp = laneData.GetType().GetProperty(ReflectionMemberNames.SXGTDataMembers.ItemIndexer);
            if (itemProp == null)
            {
                failure = LaneDataAccessFailure.NoItemProperty;
                return false;
            }

            return true;
        }
    }
}
