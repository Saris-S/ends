#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public static class EndsDialogSelectionDiagnosticsED
    {
        private const int MAX_SCENARIO_COUNT = 1024;
        private const int MAX_SUMMARY_LINES = 12;

        public enum IssueKind
        {
            MultipleDefaultDialogs,
            OverlappingConditionalDialogs
        }

        public struct ValidationIssue
        {
            public IssueKind Kind;
            public string Message;
            public List<string> InvolvedDialogNames;
        }

        private readonly struct VariableKey : IEquatable<VariableKey>
        {
            public readonly string Name;
            public readonly ConditionVariableType Type;

            public VariableKey(string name, ConditionVariableType type)
            {
                Name = name;
                Type = type;
            }

            public bool Equals(VariableKey other) => Type == other.Type && Name == other.Name;

            public override bool Equals(object obj) => obj is VariableKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (int)Type;
                }
            }
        }

        private sealed class TestContext
        {
            private readonly Dictionary<VariableKey, object> _values = new Dictionary<VariableKey, object>();

            public TestContext Clone()
            {
                var copy = new TestContext();
                foreach (KeyValuePair<VariableKey, object> pair in _values)
                {
                    copy._values[pair.Key] = pair.Value;
                }

                return copy;
            }

            public void Set(VariableKey key, object value)
            {
                _values[key] = value;
            }

            public bool TryGetBool(VariableKey key, out bool value)
            {
                value = false;
                if (!_values.TryGetValue(key, out object raw) || raw is not bool boolValue) return false;
                value = boolValue;
                return true;
            }

            public bool TryGetInt(VariableKey key, out int value)
            {
                value = 0;
                if (!_values.TryGetValue(key, out object raw) || raw is not int intValue) return false;
                value = intValue;
                return true;
            }

            public bool TryGetFloat(VariableKey key, out float value)
            {
                value = 0f;
                if (!_values.TryGetValue(key, out object raw) || raw is not float floatValue) return false;
                value = floatValue;
                return true;
            }

            public bool TryGetString(VariableKey key, out string value)
            {
                value = "";
                if (!_values.TryGetValue(key, out object raw) || raw is not string stringValue) return false;
                value = stringValue;
                return true;
            }

            public string FormatAssignment()
            {
                if (_values.Count == 0) return "any variable values";

                var parts = new List<string>(_values.Count);
                foreach (KeyValuePair<VariableKey, object> pair in _values)
                {
                    string valueText;
                    if (pair.Value is bool boolValue) valueText = boolValue ? "true" : "false";
                    else if (pair.Value is string stringValue) valueText = $"\"{stringValue}\"";
                    else valueText = pair.Value?.ToString() ?? "null";

                    parts.Add($"{pair.Key.Name}={valueText}");
                }

                return string.Join(", ", parts);
            }
        }

        public static List<ValidationIssue> ValidateCategory(IReadOnlyList<DialogEntry> dialogsInDatabaseOrder, string category)
        {
            var issues = new List<ValidationIssue>();
            if (dialogsInDatabaseOrder == null || string.IsNullOrEmpty(category)) return issues;

            var categoryDialogs = CollectCategoryDialogs(dialogsInDatabaseOrder, category);
            if (categoryDialogs.Count == 0) return issues;

            AppendMultipleDefaultIssues(category, categoryDialogs, issues);
            AppendOverlapIssues(category, categoryDialogs, issues);
            return issues;
        }

        public static bool InvolvesDialog(in ValidationIssue issue, string dialogName)
        {
            if (string.IsNullOrEmpty(dialogName) || issue.InvolvedDialogNames == null) return false;

            for (int i = 0; i < issue.InvolvedDialogNames.Count; i++)
            {
                if (issue.InvolvedDialogNames[i] == dialogName) return true;
            }

            return false;
        }

        public static string FormatSummary(IReadOnlyList<ValidationIssue> issues, int maxLines = MAX_SUMMARY_LINES)
        {
            if (issues == null || issues.Count == 0) return "";

            int lineLimit = maxLines;
            if (lineLimit < 1) lineLimit = 1;

            var sb = new StringBuilder();
            int written = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                if (written >= lineLimit) break;

                if (written > 0) sb.AppendLine();
                sb.Append("• ");
                sb.Append(issues[i].Message);
                written++;
            }

            if (issues.Count > lineLimit)
            {
                sb.AppendLine();
                sb.Append("• ... and ");
                sb.Append(issues.Count - lineLimit);
            }

            return sb.ToString();
        }

        private static List<DialogEntry> CollectCategoryDialogs(IReadOnlyList<DialogEntry> dialogsInDatabaseOrder, string category)
        {
            var result = new List<DialogEntry>();

            for (int i = 0; i < dialogsInDatabaseOrder.Count; i++)
            {
                DialogEntry dialog = dialogsInDatabaseOrder[i];
                if (dialog == null) continue;

                string dialogCategory = dialog.initiator;
                if (string.IsNullOrEmpty(dialogCategory)) dialogCategory = "Unnamed";
                if (dialogCategory != category) continue;
                if (dialog.nodes == null || dialog.nodes.Count == 0) continue;

                result.Add(dialog);
            }

            return result;
        }

        private static void AppendMultipleDefaultIssues(string category, List<DialogEntry> categoryDialogs, List<ValidationIssue> issues)
        {
            var defaultDialogs = new List<string>();

            for (int i = 0; i < categoryDialogs.Count; i++)
            {
                DialogEntry dialog = categoryDialogs[i];
                if (DialogEntryUtility.IsExcludedFromInitiatorSelection(dialog)) continue;
                if (DialogEntryUtility.HasEvaluableStartConditions(dialog)) continue;
                if (string.IsNullOrEmpty(dialog.dialogName)) defaultDialogs.Add("Unnamed");
                else defaultDialogs.Add(dialog.dialogName);
            }

            if (defaultDialogs.Count < 2) return;

            string winner = defaultDialogs[0];
            string names = string.Join(", ", defaultDialogs);
            string message =
                $"Category \"{category}\": {defaultDialogs.Count} dialogs without start conditions ({names}). " +
                $"If no conditional dialog matches, \"{winner}\" will run - the first in the Dialog Database list.";

            issues.Add(new ValidationIssue
            {
                Kind = IssueKind.MultipleDefaultDialogs,
                Message = message,
                InvolvedDialogNames = new List<string>(defaultDialogs)
            });
        }

        private static void AppendOverlapIssues(string category, List<DialogEntry> categoryDialogs, List<ValidationIssue> issues)
        {
            var conditionalDialogs = new List<DialogEntry>();

            for (int i = 0; i < categoryDialogs.Count; i++)
            {
                DialogEntry dialog = categoryDialogs[i];
                if (DialogEntryUtility.HasEvaluableStartConditions(dialog)) conditionalDialogs.Add(dialog);
            }

            if (conditionalDialogs.Count < 2) return;

            Dictionary<string, string> overlapExamples = new Dictionary<string, string>();
            List<TestContext> scenarios = BuildScenarios(conditionalDialogs, out bool truncated);

            if (truncated) UnityEngine.Debug.LogWarning($"ENDS condition diagnostics for category \"{category}\" stopped at {MAX_SCENARIO_COUNT} scenarios. Additional combinations were not checked, absence of reported overlaps does not prove the category is conflict-free.");

            for (int i = 0; i < scenarios.Count; i++)
            {
                TestContext scenario = scenarios[i];
                var matchers = new List<string>();

                for (int j = 0; j < conditionalDialogs.Count; j++)
                {
                    DialogEntry dialog = conditionalDialogs[j];
                    if (!DialogMatchesStartConditions(dialog, scenario)) continue;

                    string dialogName;
                    if (string.IsNullOrEmpty(dialog.dialogName)) dialogName = "Unnamed";
                    else dialogName = dialog.dialogName;

                    matchers.Add(dialogName);
                }

                if (matchers.Count < 2) continue;

                string overlapKey = string.Join(">", matchers);
                if (overlapExamples.ContainsKey(overlapKey)) continue;

                overlapExamples.Add(overlapKey, scenario.FormatAssignment());
            }

            foreach (KeyValuePair<string, string> overlap in overlapExamples)
            {
                string[] matcherNames = overlap.Key.Split('>');
                string winner = matcherNames[0];
                string allNames = string.Join(", ", matcherNames);
                string example = overlap.Value;

                string message =
                    $"Category \"{category}\": with {example}, dialogs {allNames} match. " +
                    $"\"{winner}\" will run - the first match in the Dialog Database list. " +
                    "Make conditions mutually exclusive or change the dialog order.";

                issues.Add(new ValidationIssue
                {
                    Kind = IssueKind.OverlappingConditionalDialogs,
                    Message = message,
                    InvolvedDialogNames = new List<string>(matcherNames)
                });
            }
        }

        private static List<TestContext> BuildScenarios(IReadOnlyList<DialogEntry> conditionalDialogs, out bool truncated)
        {
            truncated = false;
            var valuesByKey = new Dictionary<VariableKey, HashSet<object>>();

            for (int i = 0; i < conditionalDialogs.Count; i++)
            {
                CollectVariableValues(conditionalDialogs[i], valuesByKey);
            }

            var keys = new List<VariableKey>(valuesByKey.Keys);
            var scenarios = new List<TestContext>();
            if (keys.Count == 0) return scenarios;

            BuildScenarioRecursive(keys, 0, valuesByKey, new TestContext(), scenarios, ref truncated);
            return scenarios;
        }

        private static void BuildScenarioRecursive(
            List<VariableKey> keys,
            int keyIndex,
            Dictionary<VariableKey, HashSet<object>> valuesByKey,
            TestContext current,
            List<TestContext> scenarios, ref bool truncated)
        {
            if (scenarios.Count >= MAX_SCENARIO_COUNT)
            {
                truncated = true;

                return;
            }

            if (keyIndex >= keys.Count)
            {
                scenarios.Add(current.Clone());
                return;
            }

            VariableKey key = keys[keyIndex];
            if (!valuesByKey.TryGetValue(key, out HashSet<object> values) || values.Count == 0)
            {
                BuildScenarioRecursive(keys, keyIndex + 1, valuesByKey, current, scenarios, ref truncated);
                return;
            }

            foreach (object value in values)
            {
                if (truncated) return;

                TestContext next = current.Clone();
                next.Set(key, value);
                BuildScenarioRecursive(keys, keyIndex + 1, valuesByKey, next, scenarios, ref truncated);
            }
        }

        private static void CollectVariableValues(DialogEntry dialog, Dictionary<VariableKey, HashSet<object>> valuesByKey)
        {
            if (dialog?.conditions == null) return;

            for (int i = 0; i < dialog.conditions.Count; i++)
            {
                DialogCondition condition = dialog.conditions[i];
                if (condition == null) continue;
                if (DialogReferenceUtility.IsMissing(condition.ReferenceState)) continue;
                if (string.IsNullOrEmpty(condition.VariableName)) continue;

                VariableKey key = new VariableKey(condition.VariableName, condition.VariableType);
                if (!valuesByKey.TryGetValue(key, out HashSet<object> values))
                {
                    values = new HashSet<object>();
                    valuesByKey[key] = values;
                }

                AddConditionTestValues(values, condition);
            }
        }

        private static void AddConditionTestValues(HashSet<object> values, DialogCondition condition)
        {
            if (condition.VariableType == ConditionVariableType.Bool)
            {
                values.Add(true);
                values.Add(false);
                return;
            }

            if (condition.VariableType == ConditionVariableType.Int)
            {
                AddIntTestValues(values, condition.ConditionOperator, condition.IntValue);
                return;
            }

            if (condition.VariableType == ConditionVariableType.Float)
            {
                AddFloatTestValues(values, condition.ConditionOperator, condition.FloatValue);
                return;
            }

            if (condition.VariableType == ConditionVariableType.String)
            {
                AddStringTestValues(values, condition.ConditionOperator, condition.StringValue);
            }
        }

        private static void AddIntTestValues(HashSet<object> values, ConditionOperator op, int threshold)
        {
            values.Add(0);
            values.Add(threshold);
            values.Add(threshold - 1);
            values.Add(threshold + 1);
        }

        private static void AddFloatTestValues(HashSet<object> values, ConditionOperator op, float threshold)
        {
            values.Add(0f);
            values.Add(threshold);
            values.Add(threshold - 1f);
            values.Add(threshold + 1f);
            values.Add(threshold + 0.5f);
        }

        private static void AddStringTestValues(HashSet<object> values, ConditionOperator op, string threshold)
        {
            if (!string.IsNullOrEmpty(threshold)) values.Add(threshold);

            if (op == ConditionOperator.NotEquals)
            {
                if (string.IsNullOrEmpty(threshold)) values.Add("__other__");
                else values.Add(threshold + "_other");
                return;
            }

            values.Add("");
            values.Add("__other__");
        }

        private static bool DialogMatchesStartConditions(DialogEntry dialog, TestContext context)
        {
            if (dialog?.conditions == null) return false;

            return DialogConditionUtility.EvaluateWithAndOrLogic(
                dialog.conditions,
                dialog.isAndLogic,
                IsEvaluableDialogCondition,
                condition => EvaluateDialogCondition(condition, context));
        }

        private static bool IsEvaluableDialogCondition(DialogCondition condition)
        {
            if (condition == null) return false;
            if (DialogReferenceUtility.IsMissing(condition.ReferenceState)) return false;
            return !string.IsNullOrEmpty(condition.VariableName);
        }

        private static bool EvaluateDialogCondition(DialogCondition condition, TestContext context)
        {
            VariableKey key = new VariableKey(condition.VariableName, condition.VariableType);

            if (condition.VariableType == ConditionVariableType.Bool)
            {
                if (!context.TryGetBool(key, out bool value)) return false;
                return DialogConditionUtility.CompareConditionValues(value, condition.BoolValue, condition.ConditionOperator);
            }

            if (condition.VariableType == ConditionVariableType.Int)
            {
                if (!context.TryGetInt(key, out int value)) return false;
                return DialogConditionUtility.CompareConditionValues(value, condition.IntValue, condition.ConditionOperator);
            }

            if (condition.VariableType == ConditionVariableType.Float)
            {
                if (!context.TryGetFloat(key, out float value)) return false;
                return DialogConditionUtility.CompareConditionValues(value, condition.FloatValue, condition.ConditionOperator);
            }

            if (!context.TryGetString(key, out string stringValue)) return false;
            return DialogConditionUtility.CompareConditionValues(stringValue, condition.StringValue ?? "", condition.ConditionOperator);
        }
    }
}
#endif