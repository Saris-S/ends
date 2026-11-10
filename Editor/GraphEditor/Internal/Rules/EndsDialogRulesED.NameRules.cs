#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        internal const string NAME_COLOR_HEX_PATTERN = "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$";
        internal const int COLOR_COMPONENT_COUNT = 4;
        internal const float NAME_COLOR_MIN = 0;
        internal const float NAME_COLOR_MAX = 1;

        #region DIALOG NAMES

        /// <summary> ENDS. Rejects empty dialog names and the dot reserved for localization key separators. </summary>
        internal static void RequireDialogName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Dialog names cannot be empty.");
            if (name.IndexOf('.') >= 0) throw new InvalidOperationException("Dialog names cannot contain '.' because dots separate localization key parts.");
        }

        #endregion DIALOG NAMES

        #region DISPLAY NAMES

        internal static CharacterNameColor FindDisplayName(DialogDatabase database, string name)
        {
            if (database?.CharacterNameColors == null || string.IsNullOrEmpty(name)) return null;

            foreach (CharacterNameColor entry in database.CharacterNameColors)
            {
                if (entry != null && entry.Name == name) return entry;
            }

            return null;
        }

        internal static string NormalizeDisplayName(string name)
        {
            string normalized = name?.Trim() ?? "";

            if (normalized.Length == 0) throw new InvalidOperationException("Display name is empty.");

            return normalized;
        }

        internal static string ResolveDisplayName(DialogDatabase database, string name, CharacterNameColor current = null, bool unique = false, bool reuseExisting = false)
        {
            string normalized = NormalizeDisplayName(name);
            string candidate = normalized;
            int suffix = 1;

            while (true)
            {
                CharacterNameColor existing = FindDisplayName(database, candidate);

                if (existing == null || ReferenceEquals(existing, current) || reuseExisting) return candidate;
                if (!unique) throw new InvalidOperationException("Display name already exists: " + candidate + ".");

                candidate = normalized + " " + suffix;
                suffix++;
            }
        }

        #endregion DISPLAY NAMES

        #region NAME COLORS

        internal static Color ParseNameColor(string hex)
        {
            if (hex == null || !Regex.IsMatch(hex, NAME_COLOR_HEX_PATTERN) || !ColorUtility.TryParseHtmlString(hex, out Color color)) throw new InvalidOperationException("Color requires #RRGGBB or #RRGGBBAA.");

            RequireNameColor(color);

            return color;
        }

        internal static bool IsValidNameColor(Color color)
        {
            for (int i = 0; i < COLOR_COMPONENT_COUNT; i++)
            {
                float component = color[i];

                if (float.IsNaN(component) || float.IsInfinity(component) || component < NAME_COLOR_MIN || component > NAME_COLOR_MAX) return false;
            }

            return true;
        }

        internal static void RequireNameColor(Color color)
        {
            if (!IsValidNameColor(color)) throw new InvalidOperationException("Color components must be finite numbers between 0 and 1.");
        }

        #endregion NAME COLORS
    }
}
#endif