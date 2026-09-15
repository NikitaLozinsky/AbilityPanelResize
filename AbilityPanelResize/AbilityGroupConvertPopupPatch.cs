using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    public class ConvertPopupOriginalParent : MonoBehaviour
    {
        public Transform Parent;
        public int SiblingIndex;
    }

    [HarmonyPatch(typeof(ActionBarConvertedView), "BindViewImplementation")]
    public static class ActionBarConvertedView_Bind_EscapeMask_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarConvertedView __instance)
        {
            RectTransform popup = __instance.transform as RectTransform;
            RectTransform root = FindPatchedAbilityRoot(__instance.transform);
            if (popup == null || root == null)
            {
                return;
            }

            ConvertPopupOriginalParent marker = popup.GetComponent<ConvertPopupOriginalParent>();
            if (marker == null)
            {
                marker = popup.gameObject.AddComponent<ConvertPopupOriginalParent>();
                marker.Parent = popup.parent;
                marker.SiblingIndex = popup.GetSiblingIndex();
            }

            popup.SetParent(root, worldPositionStays: true);
            popup.SetAsLastSibling();

            foreach (MaskableGraphic graphic in popup.GetComponentsInChildren<MaskableGraphic>(includeInactive: true))
            {
                graphic.RecalculateClipping();
            }

            foreach (RectMask2D mask in popup.GetComponentsInChildren<RectMask2D>(includeInactive: true))
            {
                mask.enabled = false;
                mask.enabled = true;
            }
        }

        private static RectTransform FindPatchedAbilityRoot(Transform from)
        {
            ActionBarGroupPCView group = from.GetComponentInParent<ActionBarGroupPCView>();
            if (group == null)
            {
                return null;
            }

            RectTransform root = group.transform as RectTransform;
            if (root == null || root.Find(ModNames.Viewport) == null)
            {
                return null;
            }

            return root;
        }
    }

    [HarmonyPatch(typeof(ActionBarConvertedView), "DestroyViewImplementation")]
    public static class ActionBarConvertedView_Destroy_RestoreParent_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ActionBarConvertedView __instance)
        {
            RectTransform popup = __instance.transform as RectTransform;
            ConvertPopupOriginalParent marker = popup == null ? null : popup.GetComponent<ConvertPopupOriginalParent>();
            if (marker == null || marker.Parent == null)
            {
                return;
            }

            popup.SetParent(marker.Parent, worldPositionStays: false);
            popup.SetSiblingIndex(marker.SiblingIndex);
        }
    }
}
