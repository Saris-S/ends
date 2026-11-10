#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal /*Why?*/ static class EndsDatabaseMaintenanceED
    {
        internal const string DefaultCategoryName = "Unnamed";

        public static int CountDialogsInCategory(DialogDatabase database, string category)
        {
            if (database?.Dialogs == null || string.IsNullOrEmpty(category)) return 0;

            int count = 0;
            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry dialog = database.Dialogs[i];
                if (dialog == null) continue;
                if (dialog.initiator == category) count++;
            }

            return count;
        }

        public static void ApplyCategoryRename(DialogDatabase database, string oldName, string newName)
        {
            if (database == null || string.IsNullOrEmpty(oldName) || string.IsNullOrWhiteSpace(newName)) return;

            if (database.Categories != null)
            {
                for (int i = 0; i < database.Categories.Count; i++)
                {
                    if (database.Categories[i] == oldName) database.Categories[i] = newName;
                }
            }

            EndsNodeReferencesED.Visit(database, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Category && reference.Value == oldName) reference.Value = newName;
            }, NodeTraversalMode.Stored);
        }

        public static void ApplyCategoryRemoval(DialogDatabase database, string category)
        {
            if (database == null || string.IsNullOrEmpty(category)) return;
            if (database.Categories != null) database.Categories.Remove(category);
            if (database.Dialogs == null) return;

            EndsNodeReferencesED.Visit(database, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Category && reference.Value == category) reference.Value = DefaultCategoryName;
            }, NodeTraversalMode.Stored);

            if (database.Categories != null && !database.Categories.Contains(DefaultCategoryName)) database.Categories.Insert(0, DefaultCategoryName);
        }

        public static int CountLanguageUsages(DialogDatabase database, string languageCode)
        {
            if (database == null || string.IsNullOrEmpty(languageCode)) return 0;

            int count = 0;
            CountLanguageInDialogs(database.Dialogs, languageCode, ref count);
            CountLanguageInNameColors(database.CharacterNameColors, languageCode, ref count);
            return count;
        }

        public readonly struct OrphanLanguageUsage
        {
            public readonly string LanguageCode;
            public readonly int UsageCount;

            public OrphanLanguageUsage(string languageCode, int usageCount)
            {
                LanguageCode = languageCode;
                UsageCount = usageCount;
            }
        }

        public static List<OrphanLanguageUsage> GetOrphanLanguageUsages(DialogDatabase database)
        {
            List<OrphanLanguageUsage> orphans = new List<OrphanLanguageUsage>();

            if (database == null) return orphans;

            Dictionary<string, int> usageByCode = new Dictionary<string, int>();
            CollectLanguageUsageCounts(database.Dialogs, database.CharacterNameColors, usageByCode);

            foreach (KeyValuePair<string, int> pair in usageByCode)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;
                if (IsAvailableLanguage(database, pair.Key)) continue;

                orphans.Add(new OrphanLanguageUsage(pair.Key, pair.Value));
            }

            orphans.Sort((a, b) => string.Compare(a.LanguageCode, b.LanguageCode, StringComparison.Ordinal));

            return orphans;
        }

        public static void RemoveLanguageTextsOnly(DialogDatabase database, string languageCode)
        {
            if (database == null || string.IsNullOrEmpty(languageCode)) return;

            RemoveLanguageFromDialogs(database.Dialogs, languageCode);
            RemoveLanguageFromNameColors(database.CharacterNameColors, languageCode);
        }

        private static bool IsAvailableLanguage(DialogDatabase database, string languageCode)
        {
            if (database?.AvailableLanguages == null) return false;

            return database.AvailableLanguages.Contains(languageCode);
        }

        private static void CollectLanguageUsageCounts( List<DialogEntry> dialogs, List<CharacterNameColor> nameColors, Dictionary<string, int> usageByCode)
        {
            if (usageByCode == null) return;
            VisitDialogLanguages(dialogs, reference =>
            {
                if (!string.IsNullOrEmpty(reference.Value)) IncrementUsageCount(usageByCode, reference.Value);
            });

            if (nameColors == null) return;

            for (int i = 0; i < nameColors.Count; i++)
            {
                CharacterNameColor nameColor = nameColors[i];

                if (nameColor?.Translations == null) continue;

                for (int j = 0; j < nameColor.Translations.Count; j++)
                {
                    NameTranslation translation = nameColor.Translations[j];

                    if (translation == null || string.IsNullOrEmpty(translation.languageCode)) continue;

                    IncrementUsageCount(usageByCode, translation.languageCode);
                }
            }
        }

        private static void IncrementUsageCount(Dictionary<string, int> usageByCode, string languageCode)
        {
            if (usageByCode.TryGetValue(languageCode, out int count)) usageByCode[languageCode] = count + 1;
            else usageByCode[languageCode] = 1;
        }

        public static void ApplyLanguageRename(DialogDatabase database, string oldCode, string newCode)
        {
            if (database == null || string.IsNullOrEmpty(oldCode) || string.IsNullOrWhiteSpace(newCode)) return;

            if (database.AvailableLanguages != null)
            {
                for (int i = 0; i < database.AvailableLanguages.Count; i++)
                {
                    if (database.AvailableLanguages[i] == oldCode) database.AvailableLanguages[i] = newCode;
                }
            }

            if (database.CurrentLanguage == oldCode) database.CurrentLanguage = newCode;

            RenameLanguageInDialogs(database.Dialogs, oldCode, newCode);
            RenameLanguageInNameColors(database.CharacterNameColors, oldCode, newCode);
        }

        public static void ApplyLanguageRemoval(DialogDatabase database, string languageCode)
        {
            if (database == null || string.IsNullOrEmpty(languageCode)) return;
            if (database.AvailableLanguages != null) database.AvailableLanguages.Remove(languageCode);

            if (database.CurrentLanguage == languageCode)
            {
                if (database.AvailableLanguages != null && database.AvailableLanguages.Count > 0) database.CurrentLanguage = database.AvailableLanguages[0];
                else database.CurrentLanguage = "en";
            }

            RemoveLanguageFromDialogs(database.Dialogs, languageCode);
            RemoveLanguageFromNameColors(database.CharacterNameColors, languageCode);
        }

        private static void CountLanguageInDialogs(List<DialogEntry> dialogs, string languageCode, ref int count)
        {
            int matches = 0;
            VisitDialogLanguages(dialogs, reference =>
            {
                if (reference.Value == languageCode) matches++;
            });
            count += matches;
        }

        private static void CountLanguageInNameColors(List<CharacterNameColor> nameColors, string languageCode, ref int count)
        {
            if (nameColors == null) return;

            for (int i = 0; i < nameColors.Count; i++)
            {
                CharacterNameColor nameColor = nameColors[i];
                if (nameColor?.Translations == null) continue;

                for (int j = 0; j < nameColor.Translations.Count; j++)
                {
                    NameTranslation translation = nameColor.Translations[j];
                    if (translation != null && translation.languageCode == languageCode) count++;
                }
            }
        }

        private static void RenameLanguageInDialogs(List<DialogEntry> dialogs, string oldCode, string newCode)
        {
            VisitDialogLanguages(dialogs, reference =>
            {
                if (reference.Value == oldCode) reference.Value = newCode;
            });
        }

        private static void RenameLanguageInNameColors(List<CharacterNameColor> nameColors, string oldCode, string newCode)
        {
            if (nameColors == null) return;

            for (int i = 0; i < nameColors.Count; i++)
            {
                CharacterNameColor nameColor = nameColors[i];
                
                if (nameColor?.Translations == null) continue;

                for (int j = 0; j < nameColor.Translations.Count; j++)
                {
                    NameTranslation translation = nameColor.Translations[j];

                    if (translation != null && translation.languageCode == oldCode) translation.languageCode = newCode;
                }
            }
        }

        private static void RemoveLanguageFromDialogs(List<DialogEntry> dialogs, string languageCode)
        {
            var removed = new List<EndsNodeReferencesED.Reference>();
            VisitDialogLanguages(dialogs, reference =>
            {
                if (reference.Value == languageCode && reference.Siblings != null) removed.Add(reference);
            });

            foreach (EndsNodeReferencesED.Reference reference in removed)
            {
                reference.Siblings.Remove(reference.Model);
            }
        }

        private static void VisitDialogLanguages(List<DialogEntry> dialogs, Action<EndsNodeReferencesED.Reference> action)
        {
            if (dialogs == null) return;

            foreach (DialogEntry dialog in dialogs)
            {
                if (dialog == null) continue;

                EndsNodeReferencesED.Visit(DialogSchema, dialog, reference =>
                {
                    if (reference.Kind == NodeReferenceKind.Language) action(reference);
                });
            }
        }

        private static void RemoveLanguageFromNameColors(List<CharacterNameColor> nameColors, string languageCode)
        {
            if (nameColors == null) return;

            for (int i = 0; i < nameColors.Count; i++)
            {
                CharacterNameColor nameColor = nameColors[i];

                if (nameColor?.Translations == null) continue;

                nameColor.Translations.RemoveAll(t => t != null && t.languageCode == languageCode);
            }
        }
    }
}
#endif
