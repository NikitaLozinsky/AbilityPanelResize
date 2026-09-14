using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;

namespace AbilityPanelResize
{
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.SetVisible))]
    public static class ActionBarGroupPCView_SetVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, bool state)
        {
            ResizeElementAdapter adapter = __instance.GetComponent<ResizeElementAdapter>();
            adapter?.SetHandlesActive(state);
        }
    }
}
