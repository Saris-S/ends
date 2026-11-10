#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        #region VALIDATION DATA

        internal sealed class GraphValidationIssue
        {
            internal string Code;
            internal string Severity;
            internal string Dialog;
            internal string Node;
            internal string Field;
            internal string Message;

            internal string Owner;
            internal string Subject;

            internal string Key => Code + "|" + Severity + "|" + Owner + "|" + System.Text.RegularExpressions.Regex.Replace(Field ?? "", @"\[\d+\]", "[]") + "|" + Subject;
        }

        internal sealed class GraphValidationException : InvalidOperationException
        {
            internal readonly List<GraphValidationIssue> Issues;

            internal GraphValidationException(List<GraphValidationIssue> issues) : base(issues[0].Message)
            {
                Issues = issues;
            }
        }

        private sealed class ReferenceCatalog
        {
            internal readonly HashSet<string> Variables = new HashSet<string>();
            internal readonly HashSet<string> DisplayNames = new HashSet<string>();
            internal readonly Dictionary<string, HashSet<string>> Characters = new Dictionary<string, HashSet<string>>();
            internal readonly Dictionary<string, DialogEntry> Dialogs = new Dictionary<string, DialogEntry>();
            internal readonly HashSet<string> Languages = new HashSet<string>();
            internal readonly HashSet<string> Categories = new HashSet<string>();

            internal ReferenceCatalog(DialogDatabase database)
            {
                if (database.Variables != null)
                {
                    foreach (VariableDefinition variable in database.Variables)
                    {
                        if (variable != null) Variables.Add(variable.variableType + "|" + variable.variableName);
                    }
                }

                if (database.CharacterNameColors != null)
                {
                    foreach (CharacterNameColor name in database.CharacterNameColors)
                    {
                        if (name?.Name != null) DisplayNames.Add(name.Name);
                    }
                }

                if (database.CharacterGroups != null)
                {
                    foreach (CharacterGroup character in database.CharacterGroups)
                    {
                        if (character?.characterName == null) continue;

                        HashSet<string> slots = new HashSet<string>();

                        if (character.sprites != null)
                        {
                            foreach (CharacterSprite sprite in character.sprites)
                            {
                                if (sprite?.spriteName != null) slots.Add(sprite.spriteName);
                            }
                        }

                        Characters[character.characterName] = slots;
                    }
                }

                if (database.Dialogs != null)
                {
                    foreach (DialogEntry dialog in database.Dialogs)
                    {
                        if (dialog?.dialogName != null && !Dialogs.ContainsKey(dialog.dialogName)) Dialogs.Add(dialog.dialogName, dialog);
                    }
                }

                if (database.AvailableLanguages != null)
                {
                    foreach (string language in database.AvailableLanguages)
                    {
                        if (language != null) Languages.Add(language);
                    }
                }

                if (database.Categories != null)
                {
                    foreach (string category in database.Categories)
                    {
                        if (category != null) Categories.Add(category);
                    }
                }
            }
        }

        #endregion VALIDATION DATA

        #region CONNECTION RULES

        internal static void RequireValidConnection(DialogNode source, DialogNode target, string dialog, string field)
        {
            string error = GetConnectionError(source, target);

            if (error == null) return;

            List<GraphValidationIssue> issues = new List<GraphValidationIssue>();
            AddValidationIssue(issues, "connection", "error", dialog, source?.nodeId, field, error);

            throw new GraphValidationException(issues);
        }

        internal static string GetConnectionError(DialogNode source, DialogNode target)
        {
            if (source == null || target == null) return "The connection references a missing node.";
            if (source.nodeId == target.nodeId) return "A node cannot connect to itself.";
            if (target.NodeType == NodeType.Start) return "Start nodes cannot have incoming connections.";

            return null;
        }

        #endregion CONNECTION RULES

        #region WRITE VALIDATION

        internal static void ValidateDatabaseChanges(DialogDatabase before, DialogDatabase after, ISet<string> dialogScopes = null)
        {
            Dictionary<string, int> existingErrors = new Dictionary<string, int>();

            foreach (GraphValidationIssue issue in CollectGraphValidation(before, null, false, dialogScopes))
            {
                if (issue.Severity != "error") continue;

                existingErrors.TryGetValue(issue.Key, out int count);
                existingErrors[issue.Key] = count + 1;
            }

            List<GraphValidationIssue> introduced = new List<GraphValidationIssue>();

            foreach (GraphValidationIssue issue in CollectGraphValidation(after, null, false, dialogScopes))
            {
                if (issue.Severity != "error") continue;

                if (existingErrors.TryGetValue(issue.Key, out int count) && count > 0) existingErrors[issue.Key] = count - 1;
                else introduced.Add(issue);
            }

            if (introduced.Count > 0) throw new GraphValidationException(introduced);
        }

        #endregion WRITE VALIDATION

        #region GRAPH REPORT

        internal static List<GraphValidationIssue> CollectGraphValidation(DialogDatabase database, string dialogName, bool includeOverlaps, ISet<string> dialogScopes = null)
        {
            List<GraphValidationIssue> issues = new List<GraphValidationIssue>();
            ReferenceCatalog catalog = new ReferenceCatalog(database);
            AppendModelValidation(database, "", "", "", catalog, issues, dialogScopes);
            HashSet<string> dialogNames = new HashSet<string>();
            HashSet<string> categories = new HashSet<string>();

            if (database.CharacterNameColors != null)
            {
                HashSet<string> displayNames = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < database.CharacterNameColors.Count; i++)
                {
                    CharacterNameColor name = database.CharacterNameColors[i];

                    if (name?.Name != null && !displayNames.Add(name.Name)) AddValidationIssue(issues, "duplicate_display_name", "error", "", "", "CharacterNameColors[" + i + "].Name", "Display name already exists: " + name.Name + ".", name.Name);
                }
            }

            if (database.Dialogs == null) return issues;

            foreach (DialogEntry dialog in database.Dialogs)
            {
                if (dialog == null) continue;

                if (string.IsNullOrEmpty(dialog.dialogName)) AddValidationIssue(issues, "dialog_name", "error", dialog.dialogName, "", "dialogName", "Dialog names cannot be empty.");
                else if (dialog.dialogName.IndexOf('.') >= 0) AddValidationIssue(issues, "dialog_name", "error", dialog.dialogName, "", "dialogName", "Dialog names cannot contain '.' because dots separate localization key parts.");
                else if (!dialogNames.Add(dialog.dialogName)) AddValidationIssue(issues, "duplicate_dialog", "error", dialog.dialogName, "", "dialogName", "Dialog names must be unique.");

                if (!string.IsNullOrEmpty(dialog.initiator)) categories.Add(dialog.initiator);

                if (!string.IsNullOrEmpty(dialog.initiator) && !catalog.Categories.Contains(dialog.initiator)) AddValidationIssue(issues, "dialog_category", "error", dialog.dialogName, "", "initiator", "Dialog uses an unavailable category: " + dialog.initiator);

                if (dialogScopes != null && !dialogScopes.Contains(dialog.NodeIdentityKey)) continue;

                Dictionary<string, DialogNode> nodes = new Dictionary<string, DialogNode>();
                int starts = 0;

                if (dialog.nodes != null)
                {
                    foreach (DialogNode node in dialog.nodes)
                    {
                        if (node == null) continue;

                        if (node.NodeType == NodeType.Start) starts++;
                        if (string.IsNullOrEmpty(node.nodeId)) AddValidationIssue(issues, "node_id", "error", dialog.dialogName, "", "nodeId", "Node ids cannot be empty.");
                        else
                        {
                            if (nodes.ContainsKey(node.nodeId)) AddValidationIssue(issues, "duplicate_node", "error", dialog.dialogName, node.nodeId, "nodeId", "Node ids must be unique within a dialog.");
                            if (!nodes.ContainsKey(node.nodeId)) nodes.Add(node.nodeId, node);
                        }
                    }
                }

                if (starts != 1) AddValidationIssue(issues, "start_count", "error", dialog.dialogName, "", "nodes", "A dialog must contain exactly one Start node.");

                foreach (DialogNode node in nodes.Values)
                {
                    bool payloadMissing = false;

                    if (node.NodeType == NodeType.Start) payloadMissing = node.StartData == null;
                    if (node.NodeType == NodeType.Line) payloadMissing = node.LineData == null;
                    if (node.NodeType == NodeType.Choice) payloadMissing = node.ChoiceData == null;
                    if (node.NodeType == NodeType.Condition) payloadMissing = node.ConditionData == null;
                    if (node.NodeType == NodeType.Redirect) payloadMissing = node.RedirectData == null;
                    if (payloadMissing) AddValidationIssue(issues, "node_payload", "error", dialog.dialogName, node.nodeId, "NodeType", "The node is missing its " + node.NodeType + " data.");
                }

                var variableLists = new HashSet<IList>();
                EndsNodeReferencesED.Visit(DialogSchema, dialog, reference => AppendReferenceValidation(reference, catalog, nodes, variableLists, issues));
            }

            if (includeOverlaps)
            {
                foreach (string category in categories)
                {
                    foreach (EndsDialogSelectionDiagnosticsED.ValidationIssue overlap in EndsDialogSelectionDiagnosticsED.ValidateCategory(database.Dialogs, category))
                    {
                        if (!string.IsNullOrEmpty(dialogName) && !EndsDialogSelectionDiagnosticsED.InvolvesDialog(overlap, dialogName)) continue;

                        string owner = dialogName;

                        if (string.IsNullOrEmpty(owner) && overlap.InvolvedDialogNames?.Count > 0) owner = overlap.InvolvedDialogNames[0];

                        AddValidationIssue(issues, overlap.Kind.ToString(), "warning", owner, "", "conditions", overlap.Message);
                    }
                }
            }

            foreach (GraphValidationIssue issue in issues)
            {
                issue.Owner = "database";

                if (!catalog.Dialogs.TryGetValue(issue.Dialog ?? "", out DialogEntry ownerDialog)) continue;

                string scope = ownerDialog.NodeIdentityKey;

                if (string.IsNullOrEmpty(scope)) scope = ownerDialog.dialogName;

                issue.Owner = scope + "/" + issue.Node;
            }

            if (!string.IsNullOrEmpty(dialogName)) issues.RemoveAll(issue => !string.IsNullOrEmpty(issue.Dialog) && issue.Dialog != dialogName);

            return issues;
        }

        private static void AppendReferenceValidation(EndsNodeReferencesED.Reference reference, ReferenceCatalog catalog, Dictionary<string, DialogNode> nodes, HashSet<IList> variableLists, List<GraphValidationIssue> issues)
        {
            string dialog = reference.Dialog.dialogName;
            string node = reference.Node?.nodeId ?? "";
            string path = reference.Path;
            string name = reference.Value;

            if (reference.Kind == NodeReferenceKind.Node)
            {
                AppendEdgeValidation(reference.Node, name, path, dialog, nodes, issues);
            }
            else if (reference.Kind == NodeReferenceKind.Variable)
            {
                AppendNamedReference(name, reference.State, catalog.Variables.Contains(reference.VariableType + "|" + name), "variable", dialog, node, path, issues, reference.MissingLabel);

                if (reference.Siblings == null || !variableLists.Add(reference.Siblings)) return;

                string listPath = reference.OwnerPath;
                int row = listPath.LastIndexOf('[');

                if (row >= 0) listPath = listPath.Substring(0, row);
                if (reference.Siblings is IReadOnlyList<DialogCondition> conditions) AppendVariableValidation(conditions, listPath, dialog, node, catalog, issues, _dialogConditionFields);
                else if (reference.Siblings is IReadOnlyList<ChoiceCondition> choiceConditions) AppendVariableValidation(choiceConditions, listPath, dialog, node, catalog, issues, _choiceConditionFields);
                else if (reference.Siblings is IReadOnlyList<ChoiceConsequence> consequences) AppendVariableValidation(consequences, listPath, dialog, node, catalog, issues, _choiceConsequenceFields);
            }
            else if (reference.Kind == NodeReferenceKind.Speaker) AppendNamedReference(name, reference.State, catalog.DisplayNames.Contains(name ?? ""), "speaker", dialog, node, path, issues, reference.MissingLabel);
            else if (reference.Kind == NodeReferenceKind.Actor)
            {
                AppendNamedReference(name, reference.State, catalog.Characters.ContainsKey(name ?? ""), "character", dialog, node, reference.OwnerPath, issues, reference.MissingLabel);

                if (reference.State != DialogReferenceState.Missing && reference.Siblings is IReadOnlyList<CharacterActionData> animations && CollectUsedActionCharacters(animations, reference.OwnerAddress.Index).Contains(name))
                    AddValidationIssue(issues, "character_reuse", "error", dialog, node, path, "A character can have only one action in a Line node.", name);
            }
            else if (reference.Kind == NodeReferenceKind.Sprite)
            {
                if (catalog.Characters.TryGetValue(reference.Qualifier ?? "", out HashSet<string> slots)) AppendNamedReference(name, reference.State, slots.Contains(name ?? ""), "sprite slot", dialog, node, path, issues, reference.MissingLabel);
            }
            else if (reference.Kind == NodeReferenceKind.Category)
            {
                if (!string.IsNullOrEmpty(name) && !catalog.Categories.Contains(name) && reference.Node != null) AddValidationIssue(issues, "redirect_category", "error", dialog, node, path, "Redirect uses an unavailable category: " + name);
            }
            else if (reference.Kind == NodeReferenceKind.Dialog)
            {
                bool exists = catalog.Dialogs.TryGetValue(name ?? "", out DialogEntry target);
                AppendNamedReference(name, reference.State, exists, "dialog", dialog, node, path, issues, reference.MissingLabel);

                if (string.IsNullOrEmpty(name)) AddValidationIssue(issues, "empty_redirect", "warning", dialog, node, path, "Redirect has no target dialog.");
                else if (exists && reference.State == DialogReferenceState.Valid && (target.initiator != reference.Qualifier || !DialogEntryUtility.IsRedirectTargetCandidate(target)))
                    AddValidationIssue(issues, "redirect_target", "error", dialog, node, reference.OwnerPath, "Redirect target does not belong to the category or is not an eligible target.");
            }
        }

        private static void AppendEdgeValidation(DialogNode source, string targetId, string field, string dialog, Dictionary<string, DialogNode> nodes, List<GraphValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(targetId)) return;

            nodes.TryGetValue(targetId, out DialogNode target);
            string error = GetConnectionError(source, target);

            if (error != null) AddValidationIssue(issues, "connection", "error", dialog, source.nodeId, field, error, targetId);
        }

        private static void AppendNamedReference(string name, DialogReferenceState state, bool exists, string kind, string dialog, string node, string field, List<GraphValidationIssue> issues, string missingLabel)
        {
            if (state == DialogReferenceState.Missing)
            {
                string subject = missingLabel;

                if (string.IsNullOrEmpty(subject)) subject = name;
                if (string.IsNullOrEmpty(subject)) subject = "(original name unavailable)";

                AddValidationIssue(issues, "missing_reference", "warning", dialog, node, field, "Missing " + kind + " reference: " + subject, subject);
            }
            else if (!string.IsNullOrEmpty(name) && !exists) AddValidationIssue(issues, "unknown_reference", "error", dialog, node, field, "Unknown " + kind + " reference: " + name, name);
        }

        private static void AppendVariableValidation<T>(IReadOnlyList<T> values, string field, string dialog, string node, ReferenceCatalog catalog, List<GraphValidationIssue> issues, VariableFields<T> fields) where T : class
        {
            if (values == null) return;

            for (int i = 0; i < values.Count; i++)
            {
                T value = values[i];

                if (value == null) continue;

                string name = fields.Name.Read(value);
                ConditionVariableType variableType = fields.Type.Read(value);
                DialogReferenceState state = fields.State.Read(value);
                string path = field + "[" + i + "]";

                if (state == DialogReferenceState.Missing || string.IsNullOrEmpty(name)) continue;

                List<string> excluded = null;
                ConditionOperator[] allowed = null;

                if (values is IReadOnlyList<DialogCondition> conditions)
                {
                    excluded = CollectUsedVariableNames(conditions, i);
                    allowed = GetAllowedNumericConditionOperators(conditions, i, name);
                }
                else if (values is IReadOnlyList<ChoiceCondition> choiceConditions)
                {
                    excluded = CollectUsedVariableNames(choiceConditions, i);
                    allowed = GetAllowedNumericConditionOperators(choiceConditions, i, name);
                }
                else if (values is IReadOnlyList<ChoiceConsequence> consequences) excluded = CollectUsedVariableNames(consequences, i);

                if (excluded != null && excluded.Contains(name)) AddValidationIssue(issues, "variable_reuse", "error", dialog, node, path + ".VariableName", "This variable cannot be reused in the same list: " + name, name);

                if (fields.Operator == null) continue;

                ConditionOperator conditionOperator = fields.Operator.Read(value);

                if (variableType == ConditionVariableType.Bool || variableType == ConditionVariableType.String)
                {
                    if (Array.IndexOf(GetSupportedConditionOperators(variableType), conditionOperator) < 0) AddValidationIssue(issues, "condition_operator",
                        "error", dialog, node, path + ".ConditionOperator", "Bool and String conditions support only Equals and NotEquals.", name + "|" + conditionOperator);
                }
                else if (allowed != null && Array.IndexOf(allowed, conditionOperator) < 0) AddValidationIssue(issues, "condition_operator", "error", dialog, node, path + ".ConditionOperator", "This numeric operator conflicts with another condition for " + name + ".", name + "|" + conditionOperator);
            }
        }

        private static void AppendModelValidation(object model, string fieldPath, string dialog, string node, ReferenceCatalog catalog, List<GraphValidationIssue> issues, ISet<string> dialogScopes = null)
        {
            if (model == null || !Models.TryGetValue(model.GetType(), out ModelField[] fields)) return;

            if (model is ChoiceConsequence consequence && !consequence.IsOperationSupported) AddValidationIssue(issues, "consequence_operation", "error", dialog, node,
                fieldPath + nameof(ChoiceConsequence.Operation), "Only int and float consequences support addition or subtraction, and the operation must be defined.");

            if (model is CharacterNameColor nameColor)
            {
                if (string.IsNullOrWhiteSpace(nameColor.Name) || nameColor.Name != nameColor.Name.Trim()) AddValidationIssue(issues, "display_name", "error", dialog, node, fieldPath + nameof(CharacterNameColor.Name), "Display names must be nonempty and trimmed.");
                if (!IsValidNameColor(nameColor.Color)) AddValidationIssue(issues, "name_color", "error", dialog, node, fieldPath + nameof(CharacterNameColor.Color), "Color components must be finite numbers between 0 and 1.");
            }

            if (model is DialogEntry entry)
            {
                if (dialogScopes != null && !dialogScopes.Contains(entry.NodeIdentityKey)) return;

                dialog = entry.dialogName;
                fieldPath = "";
            }
            if (model is DialogNode graphNode)
            {
                node = graphNode.nodeId;
                fieldPath = "";
            }

            foreach (ModelField field in fields)
            {
                object value = field.Read(model);
                string path = fieldPath + field.Name;

                if (value == null)
                {
                    if (IsRequiredCollection(model.GetType(), field.Name, field.ValueType)) AddValidationIssue(issues, "null_collection", "error", dialog, node, path, "Collections cannot be null. Use an empty list.");

                    continue;
                }

                NumberRange? range = GetNumberRange(model.GetType(), field.Name);
                string pattern = GetStringPattern(model.GetType(), field.Name);

                if (range.HasValue && value is float scalar && !range.Value.Contains(scalar)) AddValidationIssue(issues, "number_range", "error", dialog, node,
                    path, "Value must be between " + range.Value.Minimum + " and " + range.Value.Maximum + ".", scalar.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (pattern != null && value is string nameValue && !System.Text.RegularExpressions.Regex.IsMatch(nameValue, pattern)) AddValidationIssue(issues, "variable_name", "error", dialog, node, path, "Variable names use ASCII letters, digits, and underscore.", nameValue);
                if (field.ValueType.IsEnum && !Enum.IsDefined(field.ValueType, value)) AddValidationIssue(issues, "enum_value", "error", dialog, node, path, "Unknown enum value in " + path + ".");
                if (value is float number && (float.IsNaN(number) || float.IsInfinity(number))) AddValidationIssue(issues, "finite_number", "error", dialog, node, path, "Numbers must be finite in " + path + ".");
                if (value is Vector2 position && (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y))) AddValidationIssue(issues, "finite_position", "error", dialog, node, path, "Node coordinates must be finite.");
                if (value is IList list)
                {
                    HashSet<string> languages = new HashSet<string>();

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == null && RequiresListEntries(field.ValueType))
                        {
                            AddValidationIssue(issues, "null_entry", "error", dialog, node, path + "[" + i + "]", "List entries cannot be null.");

                            continue;
                        }

                        string language = null;

                        if (list[i] is IDialogText translated) language = translated.LanguageCode;
                        if (list[i] is NameTranslation translatedName) language = translatedName.languageCode;
                        if (!string.IsNullOrEmpty(language) && !languages.Add(language)) AddValidationIssue(issues, "duplicate_translation", "error", dialog, node, path, "Translations must use unique language codes: " + language, language);

                        AppendModelValidation(list[i], path + "[" + i + "].", dialog, node, catalog, issues, dialogScopes);
                    }
                }
                else if (Models.ContainsKey(field.ValueType)) AppendModelValidation(value, path + ".", dialog, node, catalog, issues, dialogScopes);
            }

            if (model is IDialogText text && !string.IsNullOrEmpty(text.LanguageCode) && !catalog.Languages.Contains(text.LanguageCode)) AddValidationIssue(issues,
                "translation_language", "error", dialog, node, fieldPath + "LanguageCode", "Translation uses an unavailable language: " + text.LanguageCode);
            if (model is NameTranslation name && !string.IsNullOrEmpty(name.languageCode) && !catalog.Languages.Contains(name.languageCode)) AddValidationIssue(issues,
                "translation_language", "error", dialog, node, fieldPath + "languageCode", "Name translation uses an unavailable language: " + name.languageCode);
        }

        private static void AddValidationIssue(List<GraphValidationIssue> issues, string code, string severity, string dialog, string node, string field, string message, string subject = "")
        {
            issues.Add(new GraphValidationIssue { Code = code, Severity = severity, Dialog = dialog ?? "", Node = node ?? "", Field = field, Message = message, Subject = subject ?? "" });
        }

        #endregion GRAPH REPORT
    }
}
#endif