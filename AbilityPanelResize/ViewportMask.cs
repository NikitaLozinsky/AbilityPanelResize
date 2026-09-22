using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Настраивает обрезку содержимого на области прокрутки.
    ///
    /// Почему одного `RectMask2D` не хватило. У него две разные обязанности, и в
    /// этой игре работает только одна:
    ///
    /// * **отбраковка** (`CanvasRenderer.cull`) — выключает то, что целиком
    ///   снаружи. Делается на уровне рендерера, от шейдера не зависит, и
    ///   исправно работает: ряды, уехавшие высоко под шапку, действительно
    ///   пропадают.
    /// * **обрезка по прямоугольнику** — работает через шейдер: Unity кладёт
    ///   прямоугольник в `_ClipRect`, а отсекает пиксели уже сам шейдер. В игре
    ///   на всей UI-графике стоит собственный шейдер Owlcat `DefaultUI`, и он
    ///   `_ClipRect` не обрабатывает.
    ///
    /// Отсюда ровно тот симптом, что был виден: ряд, **целиком** уехавший под
    /// шапку, исчезает (отбраковка), а ряд, пересекающий её край, рисуется
    /// **полностью**, поверх заголовка — обрезать его нечему. И зависимость от
    /// размера окна: попадёт ряд на край шапки или нет, решает высота панели.
    ///
    /// Поэтому добавляем `Mask` — вторую, трафаретную (stencil) схему обрезки.
    /// Она не про шейдерный `_ClipRect`, а про буфер трафарета, и её сама игра
    /// использует в своих прокручиваемых списках. `RectMask2D` при этом
    /// оставляем: его отбраковка — бесплатная экономия отрисовки.
    /// </summary>
    public static class ViewportMask
    {
        private static bool s_SupportLogged;

        public static void Apply(GameObject viewport)
        {
            // Mask требует Graphic на том же объекте — он задаёт форму трафарета.
            //
            // Картинка обязана быть НЕПРОЗРАЧНОЙ. Прозрачной формой трафарет не
            // пишется, и тогда проверка `Comp:Equal` не проходит нигде — panel
            // становится пустой (ровно это и случилось при первой попытке).
            // Невидимой её делает не альфа, а `showMaskGraphic = false`: он
            // ставит материалу `ColorMask = 0`, то есть запрет на запись цвета
            // при сохранённой записи в буфер трафарета. Нужное шейдеру свойство
            // `_ColorMask` у него есть — проверено дампом свойств.
            Image shape = viewport.AddComponent<Image>();
            shape.color = Color.white;

            // Заодно эта картинка ловит клики по пустому месту области
            // прокрутки: задник окна до неё не достаёт, а клик мимо иконки
            // иначе проваливается в мир. Иконкам она не мешает — они её
            // потомки и лежат выше.
            shape.raycastTarget = true;

            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            mask.enabled = Main.Settings == null || Main.Settings.StencilMask;

            LogShaderSupport(shape);
        }

        /// <summary>
        /// Одноразово пишет в лог, поддерживает ли шейдер игры трафарет.
        ///
        /// Проверка осмысленна, в отличие от прошлой попытки с `_ClipRect`:
        /// `_ClipRect` шейдеры объявляют внутри программы, а не в блоке
        /// Properties, поэтому `HasProperty` о нём врёт. А вот `_Stencil` и
        /// `_StencilComp` — именно свойства материала, и `Mask` выставляет их
        /// через `StencilMaterial`. Нет их — трафарет работать не будет.
        /// </summary>
        private static void LogShaderSupport(Graphic graphic)
        {
            if (s_SupportLogged)
            {
                return;
            }

            s_SupportLogged = true;

            Material material = graphic.materialForRendering;
            if (material == null || material.shader == null)
            {
                Main.Logger.Warning(Localization.Get("AbilityPanelResize.Log.MaskShaderUnknown"));
                return;
            }

            bool stencil = material.HasProperty("_Stencil") && material.HasProperty("_StencilComp");

            string properties;
            try
            {
                var names = new StringBuilder();
                int count = material.shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    if (names.Length > 0)
                    {
                        names.Append(", ");
                    }

                    names.Append(material.shader.GetPropertyName(i));
                }

                properties = names.ToString();
            }
            catch (System.Exception exception)
            {
                properties = "не удалось перечислить: " + exception.Message;
            }

            Main.Logger.Log(Localization.Get(
                stencil ? "AbilityPanelResize.Log.MaskStencilOk" : "AbilityPanelResize.Log.MaskStencilMissing",
                material.shader.name,
                properties));
        }
    }
}
