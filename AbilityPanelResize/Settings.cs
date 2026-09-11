using UnityModManagerNet;

namespace AbilityPanelResize
{
    public class Settings : UnityModManager.ModSettings
    {
        public float Width = -1f;
        public float Height = -1f;

        public bool RememberSize = true;
        public bool CenterIcons = true;

        public float DefaultHeight = 400f;
        public float MaxWidth = 900f;
        public float MaxHeight = 800f;

        public float ScrollSensitivity = 20f;
        public float HandleThickness = 16f;

        public bool HasSavedSize => Width > 0f && Height > 0f;

        public void ResetSize()
        {
            Width = -1f;
            Height = -1f;
        }
    }
}
