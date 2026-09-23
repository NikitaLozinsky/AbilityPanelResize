using System.Text;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI;
using Kingmaker.UI.AbilityTarget;
using TurnBased.Controllers;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Почему курсор выглядит не так, как ожидается.
    ///
    /// Рисунок курсора живёт в двух местах сразу, и снаружи они неразличимы:
    /// обычно это системный курсор, который рисует ОС, а в пошаговом бою, при
    /// прицеливании и в тактическом бою — <c>PCCursor</c>, элемент интерфейса.
    /// Одна и та же текстура через эти два пути выходит на экран по-разному, и
    /// «курсор поменял вид при входе в бой» — ожидаемое следствие такой смены
    /// рисовальщика, а не обязательно чужая правка.
    ///
    /// Проба выписывает всё, что отличает один путь от другого: кто сейчас
    /// рисует (<c>Cursor.visible</c>), какие текстуры отдаёт блюпринт и что в
    /// действительности стоит на слоях <c>PCCursor</c> — вместе с цветом и
    /// шейдером, потому что подложку курсора можно и перекрасить.
    ///
    /// Живёт отдельно от <see cref="AbilityPanelDiagnostics"/> и вызывается из
    /// <c>Main.OnUpdate</c> намеренно. Тот компонент висит на самой панели, и
    /// Unity не зовёт его <c>Update</c>, пока панель выключена, — а вне боя она
    /// как раз чаще всего выключена, то есть дамп снимался бы ровно в том
    /// состоянии, которое и надо было сравнить. Курсор же к панели отношения не
    /// имеет: это глобальное состояние игры.
    /// </summary>
    internal static class CursorProbe
    {
        public static string Build()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: состояние курсора ===");

            report.AppendLine($"экран={Screen.width}x{Screen.height} режимОкна={Screen.fullScreenMode}");
            report.AppendLine($"IsResizeCursor={CursorController.IsResizeCursor} "
                              + $"держитНашХендл={PanelResizeHandle.CursorHeld} "
                              + $"пошаговыйБой={SafeTurnBasedState()} "
                              + $"режимИгры={SafeGameMode()}");

            // Cursor.visible == false означает, что курсор рисует PCCursor:
            // его SetActive первой же строкой прячет системный.
            report.AppendLine($"рисует: {(Cursor.visible ? "система (аппаратный курсор)" : "игра (PCCursor)")} "
                              + $"[Cursor.visible={Cursor.visible} lockState={Cursor.lockState}]");

            try
            {
                bool software = Game.Instance.UISettingsManager.IsOnlySoftwareMode;
                report.AppendLine($"IsOnlySoftwareMode={software} => режим CursorMode.{(software ? "ForceSoftware" : "Auto")}, "
                                  + $"набор текстур {(software ? "Cursor64/96/128 (по высоте экрана)" : "CursorHardware")}");
            }
            catch (System.Exception exception)
            {
                report.AppendLine("IsOnlySoftwareMode не прочитать: " + exception.Message);
            }

            report.AppendLine("текстуры из блюпринта (то, что игра и мы ставим курсору):");
            AppendCursorTexture(report, CursorRoot.CursorType.DefaultCursor);
            AppendCursorTexture(report, CursorRoot.CursorType.MoveCursor);
            AppendCursorTexture(report, CursorRoot.CursorType.ArrowHorizontalCursor);
            AppendCursorTexture(report, CursorRoot.CursorType.ArrowVerticalCursor);

            PCCursor pcCursor = PCCursor.Instance;
            if (pcCursor == null)
            {
                report.AppendLine("PCCursor.Instance == null — курсор рисует только система");
                return report.ToString();
            }

            Canvas canvas = pcCursor.GetComponentInParent<Canvas>();
            CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            report.AppendLine($"PCCursor: canvas={(canvas != null ? canvas.name : "NULL")} "
                              + $"renderMode={(canvas != null ? canvas.renderMode.ToString() : "?")} "
                              + $"scaleFactor={(scaler != null ? scaler.scaleFactor.ToString("0.##") : "?")}");

            report.AppendLine("слои PCCursor:");
            foreach (Image image in pcCursor.GetComponentsInChildren<Image>(includeInactive: true))
            {
                Sprite sprite = image.sprite;
                Texture texture = sprite != null ? sprite.texture : null;
                Material material = image.materialForRendering;

                report.AppendLine($"  {PathFrom(pcCursor.transform, image.transform)} "
                                  + $"активен={image.gameObject.activeInHierarchy} "
                                  + $"color={image.color} "
                                  + $"sprite={(sprite != null ? sprite.name : "NULL")} "
                                  + $"pivot={(sprite != null ? sprite.pivot.ToString("0") : "-")} "
                                  + $"texture={(texture != null ? $"{texture.name} {texture.width}x{texture.height}" : "NULL")} "
                                  + $"shader='{(material != null && material.shader != null ? material.shader.name : "NULL")}'");
            }

            return report.ToString();
        }

        private static void AppendCursorTexture(StringBuilder report, CursorRoot.CursorType type)
        {
            try
            {
                CursorRoot cursors = BlueprintRoot.Instance != null ? BlueprintRoot.Instance.Cursors : null;
                Texture2D texture = cursors != null ? cursors.GetCursorTexture(type) : null;
                report.AppendLine(texture != null
                    ? $"  {type}: {texture.name} {texture.width}x{texture.height} "
                      + $"format={texture.format} readable={texture.isReadable}"
                    : $"  {type}: NULL");
            }
            catch (System.Exception exception)
            {
                report.AppendLine($"  {type}: ошибка — {exception.Message}");
            }
        }

        private static string SafeTurnBasedState()
        {
            try
            {
                return CombatController.IsInTurnBasedCombat().ToString();
            }
            catch (System.Exception exception)
            {
                return "не определить: " + exception.Message;
            }
        }

        private static string SafeGameMode()
        {
            try
            {
                return Game.Instance.CurrentMode.ToString();
            }
            catch (System.Exception exception)
            {
                return "не определить: " + exception.Message;
            }
        }

        private static string PathFrom(Transform root, Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null && t != root; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
