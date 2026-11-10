#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsNameCommandsED
    {
        #region NAME

        internal static CharacterNameColor Add(DialogDatabase database, string name, Color color, bool reuseExisting, bool unique = false)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (database.CharacterNameColors == null) database.CharacterNameColors = new List<CharacterNameColor>();
            name = EndsDialogRulesED.ResolveDisplayName(database, name, unique: unique, reuseExisting: reuseExisting);
            EndsDialogRulesED.RequireNameColor(color);

            CharacterNameColor existing = Find(database, name);

            if (existing != null && reuseExisting) return existing;

            CharacterNameColor entry = new CharacterNameColor();
            entry.Name = name;
            entry.Color = color;
            entry.Translations = new List<NameTranslation>();
            database.CharacterNameColors.Add(entry);

            return entry;
        }

        internal static void Rename(DialogDatabase database, string oldName, string newName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (database.CharacterNameColors == null) throw new InvalidOperationException("Name not found.");

            CharacterNameColor entry = Find(database, oldName);

            if (entry == null) throw new InvalidOperationException("Name not found.");

            newName = EndsDialogRulesED.ResolveDisplayName(database, newName, entry);
            entry.Name = newName;
            RenameLineSpeakers(database, oldName, newName);
        }

        internal static void SetColor(CharacterNameColor entry, Color color)
        {
            if (entry == null) throw new InvalidOperationException("Name not found.");

            EndsDialogRulesED.RequireNameColor(color);
            entry.Color = color;
            EndsLegacyNameColorMigrationED.ClearLegacyColor(entry);
        }

        internal static void SetTranslation(CharacterNameColor entry, string language, string display)
        {
            if (entry == null) throw new InvalidOperationException("Name not found.");
            if (string.IsNullOrEmpty(language)) throw new InvalidOperationException("Language is empty.");
            if (entry.Translations == null) entry.Translations = new List<NameTranslation>();

            for (int i = 0; i < entry.Translations.Count; i++)
            {
                NameTranslation translation = entry.Translations[i];

                if (translation == null || translation.languageCode != language) continue;

                translation.displayName = display ?? "";

                return;
            }

            NameTranslation created = new NameTranslation();
            created.languageCode = language;
            created.displayName = display ?? "";
            entry.Translations.Add(created);
        }

        internal static void MarkMissing(DialogDatabase database, string name)
        {
            if (database == null || string.IsNullOrEmpty(name)) return;

            DialogReferenceCleanup.MarkLineCharacterNameMissingInDialogs(database, name);
        }

        internal static void Remove(DialogDatabase database, string name)
        {
            if (database?.CharacterNameColors == null || string.IsNullOrEmpty(name)) return;

            for (int i = database.CharacterNameColors.Count - 1; i >= 0; i--)
            {
                CharacterNameColor entry = database.CharacterNameColors[i];

                if (entry != null && entry.Name == name) database.CharacterNameColors.RemoveAt(i);
            }
        }

        #endregion NAME

        #region LOOKUP

        internal static CharacterNameColor Find(DialogDatabase database, string name)
        {
            return EndsDialogRulesED.FindDisplayName(database, name);
        }

        private static void RenameLineSpeakers(DialogDatabase database, string oldName, string newName)
        {
            if (database.Dialogs == null || oldName == newName) return;

            for (int d = 0; d < database.Dialogs.Count; d++)
            {
                DialogEntry dialog = database.Dialogs[d];

                if (dialog == null || dialog.nodes == null) continue;

                for (int n = 0; n < dialog.nodes.Count; n++)
                {
                    DialogNode node = dialog.nodes[n];

                    if (node == null || node.LineData == null) continue;
                    if (node.LineData.CharacterName != oldName) continue;

                    node.LineData.CharacterName = newName;
                }
            }
        }

        #endregion LOOKUP
    }
}
#endif
