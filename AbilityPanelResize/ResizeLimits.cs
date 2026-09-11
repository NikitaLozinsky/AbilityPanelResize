using UnityEngine;

namespace AbilityPanelResize
{
    // Общие границы размера панели способностей. Раньше жили приватными
    // константами прямо в AbilityGroupResizePatch - вынесены сюда, т.к.
    // теперь нужны ещё и в AbilityGroupScrollPatch (подрезать значение,
    // загруженное из Settings.xml, на случай если оно устарело/повреждено
    // или границы поменяются в будущей версии мода).
    public static class ResizeLimits
    {
        public static readonly Vector2 MinSize = new Vector2(200f, 150f);
        public static readonly Vector2 MaxSize = new Vector2(900f, 800f);
    }
}
