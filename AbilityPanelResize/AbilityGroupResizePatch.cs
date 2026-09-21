using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.Initialize))]
    public static class ActionBarGroupPCView_Initialize_AddResize_Patch
    {
        private const float TopHandleButtonGap = 60f;

        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, ActionBarGroupType type)
        {
            if (type != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find(ModNames.ResizeRight) != null)
            {
                return;
            }

            root.SetAsFirstSibling();

            RectTransform stableReference = root.parent as RectTransform ?? root;

            ResizeElementAdapter adapter = ResizeElementAdapter.Ensure(root.gameObject);

            CreateHandle(root, adapter, stableReference, ModNames.ResizeRight,
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                baseOffsetMin: Vector2.zero, thicknessDirMin: Vector2.zero,
                baseOffsetMax: Vector2.zero, thicknessDirMax: new Vector2(1f, 0f),
                xSign: 1f, ySign: 0f, CursorRoot.CursorType.ArrowHorizontalCursor);

            CreateHandle(root, adapter, stableReference, ModNames.ResizeLeft,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                baseOffsetMin: Vector2.zero, thicknessDirMin: new Vector2(-1f, 0f),
                baseOffsetMax: Vector2.zero, thicknessDirMax: Vector2.zero,
                xSign: -1f, ySign: 0f, CursorRoot.CursorType.ArrowHorizontalCursor);

            CreateHandle(root, adapter, stableReference, ModNames.ResizeTopLeftSeg,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0.5f, 1f),
                baseOffsetMin: Vector2.zero, thicknessDirMin: Vector2.zero,
                baseOffsetMax: new Vector2(-TopHandleButtonGap / 2f, 0f), thicknessDirMax: new Vector2(0f, 1f),
                xSign: 0f, ySign: 1f, CursorRoot.CursorType.ArrowVerticalCursor);

            CreateHandle(root, adapter, stableReference, ModNames.ResizeTopRightSeg,
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(1f, 1f),
                baseOffsetMin: new Vector2(TopHandleButtonGap / 2f, 0f), thicknessDirMin: Vector2.zero,
                baseOffsetMax: Vector2.zero, thicknessDirMax: new Vector2(0f, 1f),
                xSign: 0f, ySign: 1f, CursorRoot.CursorType.ArrowVerticalCursor);

            CreateHandle(root, adapter, stableReference, ModNames.ResizeTopRight,
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                baseOffsetMin: Vector2.zero, thicknessDirMin: Vector2.zero,
                baseOffsetMax: Vector2.zero, thicknessDirMax: new Vector2(1f, 1f),
                xSign: 1f, ySign: 1f, CursorRoot.CursorType.ArrowDiagonally01Cursor);

            UnitEntityData currentUnit = Game.Instance?.SelectionCharacter?.CurrentSelectedCharacter;
            if (currentUnit != null)
            {
                adapter.SeedCharacterId(currentUnit.UniqueId);
            }

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.HandlesAdded"));
        }

        private static void CreateHandle(
            RectTransform root,
            ResizeElementAdapter adapter,
            RectTransform reference,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 baseOffsetMin,
            Vector2 thicknessDirMin,
            Vector2 baseOffsetMax,
            Vector2 thicknessDirMax,
            float xSign,
            float ySign,
            CursorRoot.CursorType cursorType)
        {
            var handleGO = new GameObject(name, typeof(RectTransform));
            RectTransform handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.SetParent(root, worldPositionStays: false);
            handleRect.anchorMin = anchorMin;
            handleRect.anchorMax = anchorMax;

            Image image = handleGO.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            handleRect.SetAsLastSibling();

            PanelResizeHandle handle = handleGO.AddComponent<PanelResizeHandle>();
            handle.Target = root;
            handle.Reference = reference;
            handle.Adapter = adapter;
            handle.XSign = xSign;
            handle.YSign = ySign;
            handle.CursorType = cursorType;
            handle.BaseOffsetMin = baseOffsetMin;
            handle.BaseOffsetMax = baseOffsetMax;
            handle.ThicknessDirMin = thicknessDirMin;
            handle.ThicknessDirMax = thicknessDirMax;
            handle.ApplyThickness(Main.Settings != null ? Main.Settings.HandleThickness : 16f);

            adapter.RegisterHandle(handle);
        }
    }
}
