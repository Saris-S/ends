#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region CATEGORIES AND LANGUAGES

        private static void ApplyCategoryAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));

            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Category name is empty.");
            if (database.Categories == null) database.Categories = new List<string>();
            if (database.Categories.Contains(name)) throw new InvalidOperationException("Category already exists.");

            database.Categories.Add(name);
        }

        private static void ApplyCategoryRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));
            string newName = String(command.Get("newName"));

            if (database.Categories == null || !database.Categories.Contains(name)) throw new InvalidOperationException("Category not found.");
            if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("Category name is empty.");
            if (name != newName && database.Categories.Contains(newName)) throw new InvalidOperationException("Category already exists.");

            if (name == EndsDatabaseMaintenanceED.DefaultCategoryName) throw new InvalidOperationException("The default category cannot be renamed.");

            EndsDatabaseMaintenanceED.ApplyCategoryRename(database, name, newName);
        }

        private static void ApplyCategoryRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));

            if (name == EndsDatabaseMaintenanceED.DefaultCategoryName) throw new InvalidOperationException("The default category cannot be removed.");
            if (database.Categories == null || !database.Categories.Contains(name)) throw new InvalidOperationException("Category not found.");

            EndsDatabaseMaintenanceED.ApplyCategoryRemoval(database, name);
        }

        private static void ApplyLanguageAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));

            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Language is empty.");
            if (database.AvailableLanguages == null) database.AvailableLanguages = new List<string>();
            if (database.AvailableLanguages.Contains(name)) throw new InvalidOperationException("Language already exists.");

            database.AvailableLanguages.Add(name);
        }

        private static void ApplyLanguageRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));
            string newName = String(command.Get("newName"));

            if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(name)) throw new InvalidOperationException("Language not found.");
            if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("Language is empty.");
            if (name != newName && database.AvailableLanguages.Contains(newName)) throw new InvalidOperationException("Language already exists.");

            EndsDatabaseMaintenanceED.ApplyLanguageRename(database, name, newName);
        }

        private static void ApplyLanguageRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));

            if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(name)) throw new InvalidOperationException("Language not found.");
            if (database.AvailableLanguages.Count <= 1) throw new InvalidOperationException("At least one language must stay.");

            EndsDatabaseMaintenanceED.ApplyLanguageRemoval(database, name);
        }

        private static void ApplyLanguageDeleteTexts(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));

            if (database.AvailableLanguages != null && database.AvailableLanguages.Contains(name)) throw new InvalidOperationException("Language is still in the available list.");

            EndsDatabaseMaintenanceED.RemoveLanguageTextsOnly(database, name);
        }

        #endregion CATEGORIES AND LANGUAGES

        #region REGISTRIES

        private static void ApplyNameAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string key = String(command.Get("name"));

            bool unique = command.Get("unique") != null && Boolean(command.Get("unique"));
            CharacterNameColor name = EndsNameCommandsED.Add(database, key, Color.black, false, unique);

            if (command.Get("patch") != null) ApplyModelPatch(name, command.Get("patch"), database);

            response.Set("name", EndsMcpJsonED.Str(name.Name));
        }

        private static void ApplyNameRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsNameCommandsED.Rename(database, String(command.Get("name")), String(command.Get("newName")));
            response.Set("name", EndsMcpJsonED.Str(EndsDialogRulesED.NormalizeDisplayName(String(command.Get("newName")))));
        }

        private static void ApplyNameRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = String(command.Get("name"));
            EndsNameCommandsED.MarkMissing(database, name);
            EndsNameCommandsED.Remove(database, name);
        }

        private static void ApplyNamePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            CharacterNameColor name = EndsNameCommandsED.Find(database, String(command.Get("name")));

            if (name == null) throw new InvalidOperationException("Display name not found.");

            ApplyModelPatch(name, command.Get("patch"), database);
        }

        private static CharacterGroup Character(DialogDatabase database, EndsMcpJsonED command)
        {
            CharacterGroup group = EndsCharacterCommands.Find(database, String(command.Get("name")));

            if (group == null) throw new InvalidOperationException("Character not found.");

            return group;
        }

        private static CharacterSprite Sprite(DialogDatabase database, EndsMcpJsonED command)
        {
            CharacterSprite sprite = EndsCharacterCommands.FindSprite(Character(database, command), String(command.Get("slot")));

            if (sprite == null) throw new InvalidOperationException("Sprite slot not found.");

            return sprite;
        }

        private static void ApplyCharacterAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            CharacterGroup group = EndsCharacterCommands.Create(database, String(command.Get("name")), true);
            EndsCharacterCommands.CopySpriteNames(EndsCharacterCommands.Find(database, Optional(command, "copyFrom")), group);

            if (command.Get("patch") != null) ApplyModelPatch(group, command.Get("patch"), database);
        }

        private static void ApplyCharacterRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsCharacterCommands.Rename(database, String(command.Get("name")), String(command.Get("newName")));
        }

        private static void ApplyCharacterRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsCharacterCommands.Delete(database, String(command.Get("name")), false);
        }

        private static void ApplySpriteAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            CharacterSprite sprite = EndsCharacterCommands.AddSprite(Character(database, command), String(command.Get("slot")));

            if (command.Get("patch") != null) ApplyModelPatch(sprite, command.Get("patch"), database);
        }

        private static void ApplySpriteRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsCharacterCommands.RenameSprite(database, String(command.Get("name")), String(command.Get("slot")), String(command.Get("newName")));
        }

        private static void ApplySpriteRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsCharacterCommands.DeleteSprite(database, String(command.Get("name")), String(command.Get("slot")));
        }

        private static void ApplySpritePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            ApplyModelPatch(Sprite(database, command), command.Get("patch"), database);
        }

        private static VariableDefinition Variable(DialogDatabase database, EndsMcpJsonED command)
        {
            VariableDefinition variable = EndsVariableCommandsED.Find(database, String(command.Get("name")));

            if (variable == null) throw new InvalidOperationException("Variable not found.");

            return variable;
        }

        private static void ApplyVariableAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string name = EndsDialogRulesED.NormalizeVariableName(String(command.Get("name")));
            command.Set("name", EndsMcpJsonED.Str(name));
            VariableDefinition variable = new VariableDefinition { variableName = name };

            if (command.Get("patch") != null) ApplyModelPatch(variable, command.Get("patch"), database);

            EndsVariableCommandsED.Create(database, variable, true);
            response.Set("definition", ReadModel(variable));
        }

        private static void ApplyVariableRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            response.Set("variableType", EndsMcpJsonED.Str(Variable(database, command).variableType.ToString()));
            string name = EndsDialogRulesED.NormalizeVariableName(String(command.Get("newName")));
            command.Set("newName", EndsMcpJsonED.Str(name));
            EndsVariableCommandsED.Rename(database, String(command.Get("name")), name);
            response.Set("name", EndsMcpJsonED.Str(name));
        }

        private static void ApplyVariableRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            response.Set("variableType", EndsMcpJsonED.Str(Variable(database, command).variableType.ToString()));
            EndsVariableCommandsED.Delete(database, String(command.Get("name")), true);
        }

        private static void ApplyVariablePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            ApplyModelPatch(Variable(database, command), command.Get("patch"), database);
        }

        private static void ApplyDatabasePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsMcpJsonED patch = command.Get("patch");
            RequireObject(patch);

            foreach (string name in patch.Keys)
            {
                if (name != nameof(DialogDatabase.CurrentLanguage) && name != nameof(DialogDatabase.VoiceClipsEnabled)) throw new InvalidOperationException("Use a dedicated operation to change database " + name + ".");
            }

            if (patch.Get(nameof(DialogDatabase.CurrentLanguage)) != null)
            {
                string language = String(patch.Get(nameof(DialogDatabase.CurrentLanguage)));

                if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(language)) throw new InvalidOperationException("Language is unavailable.");
            }

            ApplyModelPatch(database, patch, database);
        }

        #endregion REGISTRIES

        #region DATABASE SETTINGS

        private static void ApplyDatabaseInitialize(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            if (database.Dialogs == null) database.Dialogs = new List<DialogEntry>();
            if (database.Categories == null || database.Categories.Count == 0) database.Categories = new List<string> { EndsDatabaseMaintenanceED.DefaultCategoryName, "Main Story", "Side Quests", "NPC", "Tutorial" };
            if (database.AvailableLanguages == null || database.AvailableLanguages.Count == 0) database.AvailableLanguages = new List<string> { "en" };
            if (database.CharacterNameColors == null) database.CharacterNameColors = new List<CharacterNameColor>();
            if (database.CharacterGroups == null) database.CharacterGroups = new List<CharacterGroup>();
            if (database.Variables == null) database.SetVariables(new List<VariableDefinition>());

            foreach (DialogEntry dialog in database.Dialogs)
            {
                if (dialog == null) continue;
                if (dialog.initiator == null) dialog.initiator = EndsDatabaseMaintenanceED.DefaultCategoryName;
                if (dialog.nodes == null) continue;

                foreach (DialogNode node in dialog.nodes)
                {
                    if (node?.NodeType != NodeType.Line || node.LineData == null) continue;
                    if (node.LineData.CharacterName == null) node.LineData.CharacterName = "";
                    if (node.LineData.CharacterActions == null) node.LineData.CharacterActions = new List<CharacterActionData>();
                    if (node.LineData.DialogsText == null) node.LineData.DialogsText = new List<LineText>();
                }
            }
        }

        private static void ApplyDatabaseVoice(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            bool enabled = Boolean(command.Get("enabled"));
            bool clear = command.Get("clearClips") != null && Boolean(command.Get("clearClips"));

            if (clear && database.Dialogs != null)
            {
                foreach (DialogEntry dialog in database.Dialogs)
                {
                    if (dialog?.nodes == null) continue;

                    foreach (DialogNode node in dialog.nodes)
                    {
                        if (node?.LineData?.DialogsText == null) continue;

                        foreach (LineText text in node.LineData.DialogsText)
                        {
                            if (text != null) text.VoiceClip = null;
                        }
                    }
                }
            }

            database.VoiceClipsEnabled = enabled;
        }

        #endregion DATABASE SETTINGS
    }
}
#endif
