using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Откуда подменю вариантов способности было выдернуто и куда его вернуть.
    /// Живёт на самом подменю, а не в статике — умирает вместе с ним.
    /// </summary>
    public class ConvertPopupOriginalParent : MonoBehaviour
    {
        public Transform Parent;
        public int SiblingIndex;

        /// <summary>
        /// Возвращает подменю на место. Отвечает <c>false</c>, если возвращать
        /// уже некуда — слот, которому оно принадлежало, не пережил перерисовку.
        /// </summary>
        public bool Restore()
        {
            if (Parent == null)
            {
                return false;
            }

            transform.SetParent(Parent, worldPositionStays: false);
            transform.SetSiblingIndex(SiblingIndex);
            return true;
        }
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

            SweepStrayPopups(root, popup);

            ConvertPopupOriginalParent marker = popup.GetComponent<ConvertPopupOriginalParent>();
            if (marker == null)
            {
                marker = popup.gameObject.AddComponent<ConvertPopupOriginalParent>();
            }

            if (popup.parent != root)
            {
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

        /// <summary>
        /// Подменю мы уносим из слота в корень окна — значит вместе со слотом
        /// оно уже не умрёт. Обычно его возвращает префикс на закрытии, но если
        /// слот успел исчезнуть раньше (перерисовка группы, смена героя),
        /// возвращать станет некуда, и подменю останется висеть в окне навсегда.
        ///
        /// Открытие нового подменю — естественный момент это подмести: старое в
        /// любом случае уже закрыто, игра держит открытым не больше одного.
        /// </summary>
        private static void SweepStrayPopups(RectTransform root, RectTransform current)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == current)
                {
                    continue;
                }

                ConvertPopupOriginalParent stray = child.GetComponent<ConvertPopupOriginalParent>();
                if (stray == null)
                {
                    continue;
                }

                if (!stray.Restore())
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static RectTransform FindPatchedAbilityRoot(Transform from)
        {
            ActionBarGroupPCView group = from.GetComponentInParent<ActionBarGroupPCView>();
            if (group == null)
            {
                return null;
            }

            ResizeElementAdapter adapter = group.GetComponent<ResizeElementAdapter>();
            return adapter != null && adapter.IsBuilt ? group.transform as RectTransform : null;
        }
    }

    [HarmonyPatch(typeof(ActionBarConvertedView), "DestroyViewImplementation")]
    public static class ActionBarConvertedView_Destroy_RestoreParent_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ActionBarConvertedView __instance)
        {
            __instance.GetComponent<ConvertPopupOriginalParent>()?.Restore();
        }
    }
}
