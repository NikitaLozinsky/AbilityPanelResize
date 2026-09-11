using UnityEngine;

namespace AbilityPanelResize
{
    public static class ResizeLimits
    {
        public static readonly Vector2 MinSize = new Vector2(200f, 150f);

        public const float MaxWidthCeiling = 2400f;
        public const float MaxHeightCeiling = 1600f;

        public static Vector2 MaxSize
        {
            get
            {
                Settings settings = Main.Settings;
                float width = settings != null ? settings.MaxWidth : 900f;
                float height = settings != null ? settings.MaxHeight : 800f;
                return new Vector2(
                    Mathf.Clamp(width, MinSize.x, MaxWidthCeiling),
                    Mathf.Clamp(height, MinSize.y, MaxHeightCeiling));
            }
        }

        public static Vector2 Clamp(Vector2 size)
        {
            Vector2 max = MaxSize;
            return new Vector2(
                Mathf.Clamp(size.x, MinSize.x, max.x),
                Mathf.Clamp(size.y, MinSize.y, max.y));
        }
    }
}
