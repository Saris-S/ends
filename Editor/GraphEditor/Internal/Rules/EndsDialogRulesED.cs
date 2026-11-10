#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        #region FIELDS

        internal const string VARIABLE_NAME_PATTERN = "^[a-zA-Z0-9_]+$";

        internal readonly struct NumberRange
        {
            internal readonly float Minimum;
            internal readonly float Maximum;

            internal NumberRange(float minimum, float maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            internal bool Contains(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value) && value >= Minimum && value <= Maximum;
            }
        }

        internal static readonly NumberRange ActionZoom = new NumberRange(0.0001f, 10f);
        internal static readonly NumberRange ActionPosition = new NumberRange(-2000f, 2000f);

        #endregion FIELDS

        #region FIELD RULES

        internal static NumberRange? GetNumberRange(Type owner, string field)
        {
            if (owner != typeof(CharacterActionData)) return null;
            if (field == nameof(CharacterActionData.Zoom)) return ActionZoom;
            if (field == nameof(CharacterActionData.PosX) || field == nameof(CharacterActionData.PosY)) return ActionPosition;

            return null;
        }

        internal static string GetStringPattern(Type owner, string field)
        {
            if (owner == typeof(VariableDefinition) && field == nameof(VariableDefinition.variableName)) return VARIABLE_NAME_PATTERN;

            return null;
        }

        internal static bool IsRequiredCollection(Type owner, string field, Type type)
        {
            if (owner == typeof(DialogNode) && field == nameof(DialogNode.ChoiceData)) return false;

            return typeof(IList).IsAssignableFrom(type);
        }

        internal static bool RequiresListEntries(Type type)
        {
            return typeof(IList).IsAssignableFrom(type);
        }

        internal static void RequireListEntry(bool isNull, object previous, bool hadEntry, string field)
        {
            if (!isNull || hadEntry && previous == null) return;

            List<GraphValidationIssue> issues = new List<GraphValidationIssue>
            {
                new GraphValidationIssue { Code = "null_entry", Severity = "error", Dialog = "", Node = "", Field = field, Message = "List entries cannot be null.", Subject = "" }
            };

            throw new GraphValidationException(issues);
        }

        internal static bool IsValidVariableNameChar(char value)
        {
            return value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z' || value >= '0' && value <= '9' || value == '_';
        }

        internal static string NormalizeVariableName(string value)
        {
            StringBuilder normalized = new StringBuilder();

            foreach (char character in value ?? "")
            {
                if (IsValidVariableNameChar(character)) normalized.Append(character);
            }

            return normalized.ToString();
        }

        internal static List<string> CollectUsedActionCharacters(IReadOnlyList<CharacterActionData> actions, int excludeIndex)
        {
            HashSet<string> names = new HashSet<string>();

            if (actions == null) return new List<string>();

            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionData action = actions[i];

                if (i == excludeIndex || action == null || DialogReferenceUtility.IsMissing(action.ReferenceState) || string.IsNullOrEmpty(action.CharacterName)) continue;

                names.Add(action.CharacterName);
            }

            return new List<string>(names);
        }

        internal static string GetReferenceIdentity(object model)
        {
            if (model is DialogCondition condition) return condition.VariableType + "|" + condition.VariableName;
            if (model is ChoiceCondition choiceCondition) return choiceCondition.VariableType + "|" + choiceCondition.VariableName;
            if (model is ChoiceConsequence consequence) return consequence.VariableType + "|" + consequence.VariableName;
            if (model is LineNode line) return line.CharacterName ?? "";
            if (model is CharacterActionData action) return (action.CharacterName ?? "") + "|" + (action.EmotionName ?? "");
            if (model is RedirectNode redirect) return (redirect.TargetCategory ?? "") + "|" + (redirect.TargetDialogName ?? "");

            return null;
        }

        internal static void RequireDiagnosticValue(ModelField field, object expected, object provided)
        {
            if (field.ValueType == typeof(string) && string.IsNullOrEmpty(expected as string) && string.IsNullOrEmpty(provided as string)) return;
            if (!Equals(expected, provided)) throw new InvalidOperationException(field.Name + " is derived. Change the reference through its subject fields or a dedicated registry operation.");
        }

        internal static void ResetReferenceState(object model)
        {
            if (model == null || !Models.TryGetValue(model.GetType(), out ModelField[] fields)) return;

            foreach (ModelField field in fields)
            {
if (!field.Diagnostic) continue;

                if (field.ValueType == typeof(DialogReferenceState)) field.Write(model, DialogReferenceState.Valid);
                else field.Write(model, "");
            }
        }

        #endregion FIELD RULES
    }
}
#endif
