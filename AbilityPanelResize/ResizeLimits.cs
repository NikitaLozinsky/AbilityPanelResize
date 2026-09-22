using UnityEngine;

namespace AbilityPanelResize
{
    public static class ResizeLimits
    {
        public static readonly Vector2 MinSize = new Vector2(200f, 150f);

        private const float AbsoluteWidthCeiling = 2400f;
        private const float AbsoluteHeightCeiling = 1600f;

        /// <summary>
        /// Размер холста интерфейса в его собственных единицах — не в пикселях
        /// экрана: между ними стоит <c>CanvasScaler</c>. Сообщает его сама
        /// панель при постройке, сюда со сцены дотянуться неоткуда.
        ///
        /// Зачем он нужен: окно выше холста невозможно уменьшить обратно.
        /// Верхние грани уезжают за край экрана, а нижней грани у окна нет —
        /// низ прижат к экшн-бару. Мышью в такой размер и не попасть, значит
        /// незачем его и разрешать: по холсту обрезаются и потолки ползунков в
        /// настройках, и размер, восстановленный из сохранения.
        /// </summary>
        private static Vector2? s_CanvasSize;

        public static float MaxWidthCeiling => s_CanvasSize.HasValue
            ? Mathf.Clamp(s_CanvasSize.Value.x, MinSize.x, AbsoluteWidthCeiling)
            : AbsoluteWidthCeiling;

        public static float MaxHeightCeiling => s_CanvasSize.HasValue
            ? Mathf.Clamp(s_CanvasSize.Value.y, MinSize.y, AbsoluteHeightCeiling)
            : AbsoluteHeightCeiling;

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

        public static void ReportCanvasSize(Vector2 size)
        {
            if (size.x >= MinSize.x && size.y >= MinSize.y)
            {
                s_CanvasSize = size;
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
