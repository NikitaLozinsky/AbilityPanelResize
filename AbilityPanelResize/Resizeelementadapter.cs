using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.UI.Log;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    public class ResizeElementAdapter : MonoBehaviour, IResizeElement
    {
        private RectTransform m_Rect;
        private ActionBarGroupPCView m_GroupView;
        private Traverse m_GroupViewTraverse;
        private ScrollRect m_ScrollRect;
        private GridLayoutGroup m_Grid;

        private readonly List<GameObject> m_Handles = new List<GameObject>();
        private string m_CurrentCharacterId;

        private float? m_InitialCenterX;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        private ScrollRect Scroll => m_ScrollRect != null ? m_ScrollRect : (m_ScrollRect = GetComponent<ScrollRect>());

        private GridLayoutGroup Grid
        {
            get
            {
                if (m_Grid == null)
                {
                    Transform content = transform.Find(ModNames.ContentPath);
                    if (content != null)
                    {
                        m_Grid = content.GetComponent<GridLayoutGroup>();
                    }
                }

                return m_Grid;
            }
        }

        public void ApplyLiveSettings()
        {
            Settings settings = Main.Settings;
            if (settings == null)
            {
                return;
            }

            if (Scroll != null)
            {
                Scroll.scrollSensitivity = settings.ScrollSensitivity;
            }

            if (Grid != null)
            {
                Grid.childAlignment = settings.CenterIcons ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            }

            SetSizeDelta(ResizeLimits.Clamp(Rect.sizeDelta));
        }

        public void BeginResize()
        {
            if (Scroll != null)
            {
                Scroll.enabled = false;
            }
        }

        public void EndResize()
        {
            if (Scroll != null)
            {
                Scroll.enabled = true;
            }

            Settings settings = Main.Settings;
            if (settings == null || Main.ModEntry == null || !settings.RememberSize)
            {
                return;
            }

            Vector2 size = Rect.sizeDelta;
            settings.Width = size.x;
            settings.Height = size.y;
            if (m_CurrentCharacterId != null)
            {
                settings.SetCharacterSize(m_CurrentCharacterId, size);
            }

            settings.Save(Main.ModEntry);
        }

        public void RegisterHandle(GameObject handle)
        {
            handle.SetActive(false);
            m_Handles.Add(handle);
        }

        public void SetHandlesActive(bool active)
        {
            foreach (GameObject handle in m_Handles)
            {
                if (handle != null)
                {
                    handle.SetActive(active);
                }
            }
        }

        public void ApplyCharacterSize(string characterId)
        {
            if (characterId == m_CurrentCharacterId)
            {
                return;
            }

            m_CurrentCharacterId = characterId;

            Settings settings = Main.Settings;
            if (settings == null || !settings.RememberSize)
            {
                return;
            }

            if (!settings.TryGetCharacterSize(characterId, out Vector2 size))
            {
                if (!settings.HasSavedSize)
                {
                    return;
                }

                size = new Vector2(settings.Width, settings.Height);
            }

            SetSizeDelta(ResizeLimits.Clamp(size));
        }

        public void SetSizeDelta(Vector2 size)
        {
            if (m_InitialCenterX == null)
            {
                m_InitialCenterX = Rect.anchoredPosition.x + Rect.sizeDelta.x / 2f;
            }

            Rect.sizeDelta = size;

            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.x = m_InitialCenterX.Value - size.x / 2f;
            Rect.anchoredPosition = anchoredPosition;

            EnsureGroupView();
            RepositionToKeepBottomFixed();
        }

        public Vector2 GetSize()
        {
            return Rect.sizeDelta;
        }

        public RectTransform GetTransform()
        {
            return Rect;
        }

        private void Update()
        {
            if (Input.GetMouseButtonUp(0) && CursorController.IsResizeCursor)
            {
                CursorController.IsResizeCursor = false;
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void EnsureGroupView()
        {
            if (m_GroupView == null)
            {
                m_GroupView = GetComponent<ActionBarGroupPCView>();
                if (m_GroupView != null)
                {
                    m_GroupViewTraverse = Traverse.Create(m_GroupView);
                }
            }
        }

        private void RepositionToKeepBottomFixed()
        {
            if (m_GroupViewTraverse == null)
            {
                return;
            }

            bool visibleState = m_GroupViewTraverse.Field("VisibleState").GetValue<bool>();
            if (!visibleState)
            {
                return;
            }

            float visibleDelta = m_GroupViewTraverse.Method("GetVisiblePositionDelta").GetValue<float>();

            // 5f — не наша константа. Она из формулы самой игры, и там она тоже ничем не обоснована.
            float targetY = Rect.sizeDelta.y - 5f - visibleDelta;

            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.y = targetY;
            Rect.anchoredPosition = anchoredPosition;
        }
    }
}
