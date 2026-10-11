using System;
using System.Collections.Generic;
using HarmonyLib;
using Inventory;
using MelonLoader;
using RhythmGame;
using RhythmGame.MusicSelect;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Hooks;
using sxtg2.Loaders;

namespace sxtg2.Features
{
    /// <summary>
    /// CustomNotes 폴더의 PNG를 게임 인벤토리의 노트 스킨 목록에 "커스텀 노트" 항목으로 끼워 넣는다.
    /// config.txt를 고치지 않아도 게임 자체 인벤토리 화면에서 고를 수 있다.
    ///
    /// 게임은 노트 프리팹(Util.GetNoteSkinDir)과 타격 이펙트(Lane.PlayShortEffect의 애니메이터 상태 이름)를
    /// 스킨 ID로 찾는다. "커스텀"이라는 진짜 스킨은 없으므로 userData.noteSkin에는 이 항목의 ID를 절대 남기지 않고
    /// 바탕 스킨(Default)의 ID를 넣어 둔다. 커스텀을 골랐는지는 config.txt의 CustomNotes가 기억한다.
    /// 그래서 모드를 지워도 유저 데이터(SaveUserData로 저장되는 값)에는 진짜 스킨만 남는다.
    /// </summary>
    [HarmonyPatch]
    public static class CustomNoteSkin
    {
        public const string ItemId = "sxtg2Custom";
        private const string ItemType = "NoteSkin";
        // 커스텀 노트를 덧씌울 바탕 스킨. 흰 노트 등 PNG가 없는 노트와 타격 이펙트는 이 스킨 것을 쓴다.
        private const string BaseSkinId = "Default";
        private const string TextKeyPrefix = "NoteSkin_" + ItemId + "_";

        private static readonly AccessTools.FieldRef<Setter_NoteSkin, ListViewer> SetterListViewer =
            AccessTools.FieldRefAccess<Setter_NoteSkin, ListViewer>("listViewer");
        private static readonly AccessTools.FieldRef<Setter_NoteSkin, List<InventoryItemInfo>> SetterItems =
            AccessTools.FieldRefAccess<Setter_NoteSkin, List<InventoryItemInfo>>("items");
        private static readonly AccessTools.FieldRef<ConfirmWindow, InventoryItemObject> ConfirmNoteItem =
            AccessTools.FieldRefAccess<ConfirmWindow, InventoryItemObject>("noteItem");
        private static readonly AccessTools.FieldRef<ConfirmWindow, TextLocalizer> ConfirmNoteName =
            AccessTools.FieldRefAccess<ConfirmWindow, TextLocalizer>("noteDisplayName");

        // 커스텀 노트로 미리보기를 돌리고 있는 인벤토리. 커스텀이 아니면 null.
        private static InventoryPopup _previewPopup;
        // Setter_NoteSkin이 열리면서 원래 스킨 위치로 커서를 맞추는 중이면 CustomNotes를 건드리지 않는다.
        private static bool _setterInitializing;
        // Setter_NoteSkin.ChangeCursorDelta 안에서 미리보기를 새로 만드는 중인지.
        private static bool _setterChanging;

        /// <summary>CustomNotes 폴더에 노트 PNG가 있어서 목록에 항목을 보여 줄 수 있는지.</summary>
        internal static bool Available => CustomNoteSpriteLoader.HasNoteSprites;

        /// <summary>지금 커스텀 노트가 장착된 상태인지(= 플레이에서 PNG가 입혀지는지).</summary>
        internal static bool IsSelected => Available && SaveCustomKeyConfig.EnableCustomNotes;

        /// <summary>노트 스킨 목록에 커스텀 항목이 없으면 끝에 붙인다. PNG가 없으면 붙이지 않고 null.</summary>
        private static InventoryItemInfo EnsureItem()
        {
            if (!Available || Util.items == null || !Util.items.TryGetValue(ItemType, out var list))
                return null;

            var item = list.Find(i => i.itemID == ItemId);
            if (item == null)
            {
                item = new InventoryItemInfo { itemID = ItemId, itemType = ItemType };
                list.Add(item);
                MelonLogger.Msg($"[CustomNoteSkin] 인벤토리 노트 스킨 목록에 \"커스텀 노트\" 항목을 추가했습니다 (바탕 스킨 {BaseSkinId})");
            }
            return item;
        }

        [HarmonyPatch(typeof(Setter_NoteSkin), nameof(Setter_NoteSkin.OnInitialize))]
        [HarmonyPrefix]
        private static void OnInitializePrefix()
        {
            EnsureItem();
            _setterInitializing = true;
        }

        /// <summary>
        /// 원본은 userData.noteSkin 위치로 커서를 맞춘다. 커스텀이 장착돼 있으면 userData에는 바탕 스킨이 들어 있으므로
        /// 커서를 커스텀 항목으로 다시 옮긴다(원본과 같은 SelectIndex → ChangeCursorDelta(0) 순서).
        /// </summary>
        [HarmonyPatch(typeof(Setter_NoteSkin), nameof(Setter_NoteSkin.OnInitialize))]
        [HarmonyPostfix]
        private static void OnInitializePostfix(Setter_NoteSkin __instance)
        {
            _setterInitializing = false;
            if (!IsSelected)
                return;

            try
            {
                var items = SetterItems(__instance);
                int index = items?.FindIndex(i => i.itemID == ItemId) ?? -1;
                if (index < 0)
                    return;

                SetterListViewer(__instance).SelectIndex(index, 0);
                __instance.ChangeCursorDelta(0);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomNoteSkin] 인벤토리에서 커스텀 항목 선택 실패: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(Setter_NoteSkin), nameof(Setter_NoteSkin.OnInitialize))]
        [HarmonyFinalizer]
        private static void OnInitializeFinalizer()
        {
            _setterInitializing = false;
        }

        [HarmonyPatch(typeof(Setter_NoteSkin), nameof(Setter_NoteSkin.ChangeCursorDelta))]
        [HarmonyPrefix]
        private static void ChangeCursorDeltaPrefix()
        {
            _setterChanging = true;
        }

        /// <summary>
        /// 원본은 고른 항목의 ID를 userData.noteSkin에 그대로 넣는다. 커스텀 ID가 남으면 플레이에서 노트 프리팹을 못 찾으므로
        /// 바탕 스킨 ID로 바꾸고, 커스텀을 골랐는지는 CustomNotes(config.txt)에 기록한다.
        /// 원본에서 예외가 나도 커스텀 ID가 남지 않도록 Finalizer에서 처리한다.
        /// </summary>
        [HarmonyPatch(typeof(Setter_NoteSkin), nameof(Setter_NoteSkin.ChangeCursorDelta))]
        [HarmonyFinalizer]
        private static void ChangeCursorDeltaFinalizer()
        {
            _setterChanging = false;
            try
            {
                var userData = UserAccountModule.Instance != null ? UserAccountModule.Instance.userData : null;
                if (userData == null)
                    return;

                bool custom = userData.noteSkin == ItemId;
                if (custom)
                    userData.noteSkin = BaseSkinId;

                if (!_setterInitializing)
                    SaveCustomKeyConfig.SetCustomNotesFromInventory(custom);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomNoteSkin] 노트 스킨 선택 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 인벤토리 미리보기 노트 생성. 커스텀 ID로 불리면 바탕 스킨 프리팹을 쓰게 바꾸고, 생성되는 노트에 PNG를 입힌다.
        /// 인벤토리를 열 때(인자 없는 RefreshNoteGenerate → userData.noteSkin)는 커스텀이 장착돼 있으면 커스텀으로 보여 준다.
        /// </summary>
        [HarmonyPatch(typeof(InventoryPopup), nameof(InventoryPopup.RefreshNoteGenerate), new[] { typeof(string) })]
        [HarmonyPrefix]
        private static void RefreshNoteGeneratePrefix(InventoryPopup __instance, ref string noteID)
        {
            bool custom;
            if (noteID == ItemId)
            {
                custom = true;
                noteID = BaseSkinId;
            }
            else
            {
                // 커서 이동 중에 들어온 진짜 스킨 ID는 그 스킨 미리보기다(CustomNotes는 이 다음 Finalizer에서 바뀐다).
                custom = !_setterChanging && IsSelected;
            }

            _previewPopup = custom ? __instance : null;
        }

        [HarmonyPatch(typeof(RG_NoteObject), nameof(RG_NoteObject.SetTiming))]
        [HarmonyPostfix]
        private static void SetTimingPostfix(RG_NoteObject __instance)
        {
            var popup = _previewPopup;
            if (popup == null || __instance == null)
                return;

            try
            {
                // 플레이 노트는 NoteSpriteHook이 따로 처리한다. 여기서는 이 인벤토리 기어 아래에 생긴 미리보기 노트만.
                if (popup.currentGear == null || !__instance.transform.IsChildOf(popup.currentGear.transform))
                    return;

                NoteSpriteHook.ApplyCustomSkin(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomNoteSkin] 미리보기 노트에 커스텀 스킨 적용 실패: {ex.Message}");
            }
        }

        /// <summary>목록 칸 그림. CustomNotes\Thumbnail.png가 있으면 그걸 쓰고, 없으면 원본 흐름(물음표 아이콘)으로 둔다.</summary>
        [HarmonyPatch(typeof(InventoryItemInfo), nameof(InventoryItemInfo.GetThumbnailSprite))]
        [HarmonyPrefix]
        private static bool GetThumbnailSpritePrefix(InventoryItemInfo __instance, ref Sprite __result)
        {
            if (__instance.itemID != ItemId || __instance.itemType != ItemType || CustomNoteSpriteLoader.ThumbnailSprite == null)
                return true;

            __result = CustomNoteSpriteLoader.ThumbnailSprite;
            return false;
        }

        /// <summary>게임 CSV에 없는 커스텀 항목의 이름/설명. 키는 "NoteSkin_" + ID + "_DisplayName" 형식으로 들어온다.</summary>
        [HarmonyPatch(typeof(CSVParser), nameof(CSVParser.GetLocalizedString))]
        [HarmonyPrefix]
        private static bool GetLocalizedStringPrefix(string key, ELanguage lan, ref string __result)
        {
            if (key == null || !key.StartsWith(TextKeyPrefix, StringComparison.Ordinal))
                return true;

            bool description = key.EndsWith("_Description", StringComparison.Ordinal);
            switch (lan)
            {
                case ELanguage.KR:
                    __result = description ? "CustomNotes 폴더의 PNG로 바꾼 노트 (sxtg2)" : "커스텀 노트";
                    break;
                case ELanguage.JP:
                    __result = description ? "CustomNotesフォルダのPNGで置き換えたノーツ (sxtg2)" : "カスタムノーツ";
                    break;
                default:
                    __result = description ? "Notes replaced with PNGs from the CustomNotes folder (sxtg2)" : "Custom Notes";
                    break;
            }
            return false;
        }

        /// <summary>곡 선택 확인창의 장착 노트 표시. userData에는 바탕 스킨이 들어 있으므로 커스텀으로 바꿔 보여 준다.</summary>
        [HarmonyPatch(typeof(ConfirmWindow), nameof(ConfirmWindow.RefreshItemInfo))]
        [HarmonyPostfix]
        private static void ConfirmWindowRefreshItemInfoPostfix(ConfirmWindow __instance)
        {
            if (!IsSelected || UserAccountModule.Instance == null)
                return;

            try
            {
                var item = EnsureItem();
                if (item == null)
                    return;

                ConfirmNoteItem(__instance)?.Fetch(item);
                ConfirmNoteName(__instance)?.Refresh(TextKeyPrefix + "DisplayName");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomNoteSkin] 확인창 노트 스킨 표시 실패: {ex.Message}");
            }
        }

        /// <summary>마지막 안전장치: 어떤 경로로든 커스텀 ID가 userData에 남은 채 저장되지 않게 한다.</summary>
        [HarmonyPatch(typeof(UserAccountModule), nameof(UserAccountModule.SaveUserData))]
        [HarmonyPrefix]
        private static void SaveUserDataPrefix(UserAccountModule __instance)
        {
            var userData = __instance.userData;
            if (userData == null || userData.noteSkin != ItemId)
                return;

            userData.noteSkin = BaseSkinId;
            MelonLogger.Warning($"[CustomNoteSkin] 저장 직전 noteSkin에 커스텀 ID가 남아 있어 {BaseSkinId}로 바꿔 저장합니다.");
        }

        /// <summary>안전장치: 커스텀 ID로 스킨 폴더를 찾으면 바탕 스킨 폴더를 돌려준다(프리팹이 null이 되는 것 방지).</summary>
        [HarmonyPatch(typeof(Util), nameof(Util.GetNoteSkinDir))]
        [HarmonyPrefix]
        private static void GetNoteSkinDirPrefix(ref string noteSkinID)
        {
            if (noteSkinID == ItemId)
                noteSkinID = BaseSkinId;
        }
    }
}
