#if UNITY_EDITOR
using System;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region CHOICE AND ACTIONS

        private static DialogNode RequireChoiceNode(DialogDatabase database, EndsMcpJsonED command)
        {
            DialogNode node = FindNode(database, command);

            if (node.NodeType != NodeType.Choice) throw new InvalidOperationException("The target is not a Choice node.");

            return node;
        }

        private static LineNode Line(DialogDatabase database, EndsMcpJsonED command)
        {
            DialogNode node = FindNode(database, command);

            if (node.NodeType != NodeType.Line || node.LineData == null) throw new InvalidOperationException("The target has no Line data.");

            return node.LineData;
        }

        private static void ApplyChoiceAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = RequireChoiceNode(database, command);
            int index = EndsNodeContentCommandsED.AddChoice(node);

            if (command.Get("patch") != null) ApplyModelPatch(node.ChoiceData[index], command.Get("patch"), database);

            ValidateChangedEdges(FindDialog(database, command), node, ChoiceOptionSchema, null, node.ChoiceData[index], "ChoiceData[" + index + "].");
            response.Set("index", EndsMcpJsonED.Num(index));
        }

        private static void ApplyChoiceRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = RequireChoiceNode(database, command);
            EndsNodeContentCommandsED.RemoveChoice(node, Index(command, node.ChoiceData?.Count ?? 0));
        }

        private static void ApplyChoicePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = RequireChoiceNode(database, command);
            int index = Index(command, node.ChoiceData?.Count ?? 0);
            ChoiceNode choice = node.ChoiceData[index];

            if (choice == null) throw new InvalidOperationException("Choice data is missing.");

            ChoiceNode before = Snapshot(choice);
            ApplyModelPatch(choice, command.Get("patch"), database);
            ValidateChangedEdges(FindDialog(database, command), node, ChoiceOptionSchema, before, choice, "ChoiceData[" + index + "].");
        }

        private static void ApplyActionAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            CharacterActionData action = EndsDialogRulesED.CreateAction();

            if (command.Get("patch") != null) ApplyModelPatch(action, command.Get("patch"), database);

            response.Set("index", EndsMcpJsonED.Num(EndsNodeContentCommandsED.AddAction(Line(database, command), action)));
        }

        private static void ApplyActionRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            LineNode line = Line(database, command);
            EndsNodeContentCommandsED.RemoveAction(line, Index(command, line.CharacterActions?.Count ?? 0));
        }

        private static void ApplyActionPatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            LineNode line = Line(database, command);
            CharacterActionData action = EndsNodeContentCommandsED.ActionAt(line, Index(command, line.CharacterActions?.Count ?? 0), false);
            ApplyModelPatch(action, command.Get("patch"), database);
        }

        private static void ApplyActionCopy(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            LineNode targetLine = Line(database, command);
            EndsMcpJsonED sourceCommand = NodeCommand("action.patch", String(command.Get("dialog")), String(command.Get("sourceNode")));
            LineNode sourceLine = Line(database, sourceCommand);
            int sourceIndex = Integer(command.Get("sourceIndex"));
            CharacterActionData source = EndsNodeContentCommandsED.ActionAt(sourceLine, sourceIndex, false);
            CharacterActionData target = EndsNodeContentCommandsED.ActionAt(targetLine, Index(command, targetLine.CharacterActions?.Count ?? 0), false);
            EndsNodeContentCommandsED.CopyAction(source, target);
        }

        #endregion CHOICE AND ACTIONS

        #region LOCALIZATION

        private static void ApplyLocalizationText(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string language = String(command.Get("language"));

            if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(language)) throw new InvalidOperationException("Language is unavailable.");

            if (!EndsDialogLocalizationCsvED.TryApplyText(database, String(command.Get("key")), String(command.Get("language")), String(command.Get("text")), out bool changed, out string error)) throw new InvalidOperationException(error);

            response.Set("changed", EndsMcpJsonED.Boolean(changed));
        }

        private static void ApplyLocalizationSayer(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string language = String(command.Get("language"));

            if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(language)) throw new InvalidOperationException("Language is unavailable.");

            if (!EndsDialogLocalizationCsvED.TryApplySayer(database, String(command.Get("key")), String(command.Get("language")), String(command.Get("text")), out string error)) throw new InvalidOperationException(error);
        }

        private static void ApplyLocalizationVoice(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string language = String(command.Get("language"));

            if (database.AvailableLanguages == null || !database.AvailableLanguages.Contains(language)) throw new InvalidOperationException("Language is unavailable.");

            if (!EndsDialogLocalizationCsvED.TryApplyVoice(database, String(command.Get("key")), String(command.Get("language")), String(command.Get("asset")), out string error)) throw new InvalidOperationException(error);
        }

        private static void ApplyLocalizationCsv(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string csv = String(command.Get("csv"));

            if (command.Get("addLanguages") != null && Boolean(command.Get("addLanguages"))) EndsDialogLocalizationCsvED.AddLanguagesToAvailableList(database, EndsDialogLocalizationCsvED.GetMissingLanguageColumns(csv, database));

            EndsDialogLocalizationCsvED.ImportResult imported = EndsDialogLocalizationCsvED.ApplyCsv(database, csv);

            if (!imported.Ok) throw new InvalidOperationException(imported.Error);

            response.Set("updatedRows", EndsMcpJsonED.Num(imported.UpdatedRows));
            response.Set("unknownKeys", EndsMcpJsonED.Num(imported.UnknownKeys));
            response.Set("malformedKeys", EndsMcpJsonED.Num(imported.MalformedKeys));
            response.Set("message", EndsMcpJsonED.Str(imported.BuildMessage()));
        }

        #endregion LOCALIZATION
    }
}
#endif
