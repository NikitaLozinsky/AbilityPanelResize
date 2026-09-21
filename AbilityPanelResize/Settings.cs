using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace AbilityPanelResize
{
    public class CharacterSizeEntry
    {
        public string CharacterId;
        public float Width;
        public float Height;
    }

    public class Settings : UnityModManager.ModSettings
    {
        public float Width = -1f;
        public float Height = -1f;

        public bool RememberSize = true;
        public bool CenterIcons = true;

        /// Трафаретная обрезка иконок по краю окна. Выключается на лету —
        /// страховка на случай, если на чужом железе/драйвере она подведёт.
        public bool StencilMask = true;

        /// Дамп геометрии панели в лог (сам при прокрутке под шапку и по
        /// Ctrl+Alt+D). Отладочное, по умолчанию выключено.
        public bool Diagnostics;

        public float DefaultHeight = 400f;
        public float MaxWidth = 900f;
        public float MaxHeight = 800f;

        public float ScrollSensitivity = 20f;
        public float HandleThickness = 16f;

        public List<CharacterSizeEntry> CharacterSizes = new List<CharacterSizeEntry>();

        public bool HasSavedSize => Width > 0f && Height > 0f;

        public bool TryGetCharacterSize(string characterId, out Vector2 size)
        {
            if (!string.IsNullOrEmpty(characterId))
            {
                foreach (CharacterSizeEntry entry in CharacterSizes)
                {
                    if (entry.CharacterId == characterId)
                    {
                        size = new Vector2(entry.Width, entry.Height);
                        return true;
                    }
                }
            }

            size = default;
            return false;
        }

        public void SetCharacterSize(string characterId, Vector2 size)
        {
            foreach (CharacterSizeEntry entry in CharacterSizes)
            {
                if (entry.CharacterId == characterId)
                {
                    entry.Width = size.x;
                    entry.Height = size.y;
                    return;
                }
            }

            CharacterSizes.Add(new CharacterSizeEntry
            {
                CharacterId = characterId,
                Width = size.x,
                Height = size.y
            });
        }

        public void ResetSize()
        {
            Width = -1f;
            Height = -1f;
            CharacterSizes.Clear();
        }
    }
}
