#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsDialogConditionContractED
    {
        #region FIELDS

        internal const int CONTRACT_VERSION = EndsDialogGraphContractED.CONTRACT_VERSION;

        #endregion FIELDS

        #region MODEL

        internal static ConditionNode ReadSnapshot(ConditionNode condition)
        {
            return EndsDialogGraphContractED.Snapshot(condition);
        }

        internal static EndsSessionED.Result SetNodeConditions(DialogDatabase database, string dialogName, string nodeId, List<DialogCondition> conditions, bool replaceConditions, bool? andLogic, int? expectedRevision = null)
        {
            EndsMcpJsonED command = EndsDialogGraphContractED.NodeCommand("node.patch", dialogName, nodeId);
            EndsMcpJsonED data = EndsMcpJsonED.Obj();

            if (replaceConditions) data.Set(nameof(ConditionNode.DialogConditions), EndsDialogGraphContractED.ReadModel(conditions));
            if (andLogic.HasValue) data.Set(nameof(ConditionNode.IsAndLogic), EndsMcpJsonED.Boolean(andLogic.Value));

            EndsMcpJsonED patch = EndsMcpJsonED.Obj();
            patch.Set(nameof(DialogNode.ConditionData), data);
            command.Set("patch", patch);

            return EndsDialogGraphContractED.Execute(database, command, expectedRevision);
        }

        internal static DialogEntry FindDialog(DialogDatabase database, string dialogName)
        {
            return EndsDialogGraphContractED.FindDialog(database, dialogName);
        }

        #endregion MODEL

        #region MCP

        internal static void ReadNode(ConditionNode condition, EndsMcpJsonED output)
        {
            output.Set("and", EndsMcpJsonED.Boolean(condition.IsAndLogic));
            output.Set("conditions", ReadConditions(condition.DialogConditions));
            EndsNodeReferencesED.Visit(ConditionNodeSchema, condition, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Node) output.Set(reference.Port, EndsMcpJsonED.Str(reference.Value));
            });
        }

        internal static bool ApplyNodePatch(DialogNode node, EndsMcpJsonED patch)
        {
            if (patch.Get("and") == null && patch.Get("conditions") == null) return false;
            if (node.NodeType != NodeType.Condition) throw new InvalidOperationException("Conditions and AND/OR require a Condition node.");
            if (node.ConditionData == null) node.ConditionData = EndsDialogRulesED.CreateConditionNode();

            EndsMcpJsonED data = EndsMcpJsonED.Obj();

            if (patch.Get("and") != null) data.Set(nameof(ConditionNode.IsAndLogic), patch.Get("and"));
            if (patch.Get("conditions") != null) data.Set(nameof(ConditionNode.DialogConditions), TranslateConditions(patch.Get("conditions")));

            EndsDialogGraphContractED.ApplyModelPatch(node.ConditionData, data);

            return true;
        }

        internal static EndsMcpJsonED ReadConditions(List<DialogCondition> conditions)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Arr();

            if (conditions == null) return result;

            foreach (DialogCondition condition in conditions)
            {
                if (condition == null)
                {
                    result.Add(EndsMcpJsonED.Null());

                    continue;
                }

                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                item.Set("id", EndsMcpJsonED.Str(condition.BlockId));
                item.Set("variable", EndsMcpJsonED.Str(condition.VariableName));
                item.Set("type", EndsMcpJsonED.Str(TypeName(condition.VariableType)));
                item.Set("op", EndsMcpJsonED.Str(OperatorName(condition.ConditionOperator)));
                item.Set("reference", EndsMcpJsonED.Str(condition.ReferenceState.ToString()));
                item.Set("missingReference", EndsMcpJsonED.Str(condition.MissingReferenceLabel));
                EndsMcpJsonED value = EndsMcpJsonED.Boolean(condition.BoolValue);

                if (condition.VariableType == ConditionVariableType.Int) value = EndsMcpJsonED.Num(condition.IntValue);
                if (condition.VariableType == ConditionVariableType.Float) value = EndsMcpJsonED.Num(condition.FloatValue);
                if (condition.VariableType == ConditionVariableType.String) value = EndsMcpJsonED.Str(condition.StringValue);

                item.Set("value", value);
                result.Add(item);
            }

            return result;
        }

        internal static List<DialogCondition> ParseConditions(EndsMcpJsonED input, List<DialogCondition> previous = null)
        {
            return EndsDialogGraphContractED.ReadInputModel(TranslateConditions(input), previous);
        }

        internal static EndsMcpJsonED TranslateConditions(EndsMcpJsonED input)
        {
            if (input.Type != EndsMcpJsonED.Kind.Array || input.Items == null) throw new InvalidOperationException("conditions must be an array.");

            EndsMcpJsonED result = EndsMcpJsonED.Arr();

            foreach (EndsMcpJsonED item in input.Items)
            {
                if (item == null || item.Type == EndsMcpJsonED.Kind.Null)
                {
                    result.Add(EndsMcpJsonED.Null());

                    continue;
                }

                if (item.Type != EndsMcpJsonED.Kind.Object) throw new InvalidOperationException("Each condition must be an object.");

                EndsMcpJsonED data = EndsMcpJsonED.Obj();
                ConditionVariableType type = ConditionVariableType.Bool;

                if (item.Get("type") != null) type = ReadEnum<ConditionVariableType>(item.Get("type"));

                data.Set(nameof(DialogCondition.VariableType), EndsMcpJsonED.Str(type.ToString()));

                foreach (string key in item.Keys)
                {
                    string field;

                    if (key == "id") field = nameof(DialogCondition.BlockId);
                    else if (key == "variable") field = nameof(DialogCondition.VariableName);
                    else if (key == "type") continue;
                    else if (key == "op")
                    {
                        data.Set(nameof(DialogCondition.ConditionOperator), EndsMcpJsonED.Str(ReadOperator(item.Get(key)).ToString()));

                        continue;
                    }
                    else if (key == "reference") field = nameof(DialogCondition.ReferenceState);
                    else if (key == "missingReference") field = nameof(DialogCondition.MissingReferenceLabel);
                    else if (key == "value")
                    {
                        field = nameof(DialogCondition.BoolValue);

                        if (type == ConditionVariableType.Int) field = nameof(DialogCondition.IntValue);
                        if (type == ConditionVariableType.Float) field = nameof(DialogCondition.FloatValue);
                        if (type == ConditionVariableType.String) field = nameof(DialogCondition.StringValue);
                    }
                    else throw new InvalidOperationException("Unknown condition field: " + key);

                    data.Set(field, item.Get(key));
                }

                result.Add(data);
            }

            return result;
        }

        internal static void AddPatchSchema(EndsMcpJsonED properties)
        {
            EndsMcpJsonED and = EndsMcpJsonED.Obj();
            and.Set("type", EndsMcpJsonED.Str("boolean"));
            and.Set("description", EndsMcpJsonED.Str("True requires all conditions, false requires any."));
            properties.Set("and", and);
            EndsMcpJsonED conditions = EndsMcpJsonED.Obj();
            conditions.Set("type", EndsMcpJsonED.Str("array"));
            EndsMcpJsonED row = EndsMcpJsonED.Obj();
            EndsMcpJsonED fields = EndsMcpJsonED.Obj();

            foreach (string field in new[] { "variable", "type", "op", "reference", "missingReference" })
            {
                EndsMcpJsonED value = EndsMcpJsonED.Obj();
                value.Set("type", EndsMcpJsonED.Str("string"));
                fields.Set(field, value);
            }

            EndsMcpJsonED activeValue = EndsMcpJsonED.Obj();
            EndsMcpJsonED variants = EndsMcpJsonED.Arr();

            foreach (string type in new[] { "boolean", "integer", "number", "string" })
            {
                EndsMcpJsonED variant = EndsMcpJsonED.Obj();
                variant.Set("type", EndsMcpJsonED.Str(type));
                variants.Add(variant);
            }

            activeValue.Set("anyOf", variants);
            fields.Set("value", activeValue);
            row.Set("type", EndsMcpJsonED.Str("object"));
            row.Set("properties", fields);
            row.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
            conditions.Set("items", row);
            properties.Set("conditions", conditions);
        }

        #endregion MCP

        #region COVERAGE AND VALUES

        internal static List<string> FindUnclassifiedFields(Type modelType)
        {
            return EndsDialogGraphContractED.FindUnclassifiedFields(modelType);
        }

        private static string ReadString(EndsMcpJsonED input)
        {
            if (input.Type != EndsMcpJsonED.Kind.String) throw new InvalidOperationException("Expected a string.");

            return input.Text;
        }

        private static T ReadEnum<T>(EndsMcpJsonED input) where T : struct
        {
            string name = ReadString(input);

            if (!Enum.TryParse(name, true, out T value) || !Enum.IsDefined(typeof(T), value)) throw new InvalidOperationException("Unknown " + typeof(T).Name + ": " + name + ".");

            return value;
        }

        private static ConditionOperator ReadOperator(EndsMcpJsonED input)
        {
            string name = ReadString(input);

            if (string.IsNullOrEmpty(name)) return ConditionOperator.Equals;
            if (name == "greater") return ConditionOperator.GreaterThan;
            if (name == "less") return ConditionOperator.LessThan;
            if (name == "greaterOrEquals") return ConditionOperator.GreaterThanOrEquals;
            if (name == "lessOrEquals") return ConditionOperator.LessThanOrEquals;

            return ReadEnum<ConditionOperator>(input);
        }

        private static string TypeName(ConditionVariableType type)
        {
            return type.ToString().ToLowerInvariant();
        }

        private static string OperatorName(ConditionOperator value)
        {
            if (value == ConditionOperator.GreaterThan) return "greater";
            if (value == ConditionOperator.LessThan) return "less";
            if (value == ConditionOperator.GreaterThanOrEquals) return "greaterOrEquals";
            if (value == ConditionOperator.LessThanOrEquals) return "lessOrEquals";

            string name = value.ToString();

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        #endregion COVERAGE AND VALUES
    }
}
#endif