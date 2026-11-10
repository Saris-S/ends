#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region FIELDS

        private sealed class OperationArgument
        {
            internal string Name;
            internal string ContractType;
            internal Type ValueType;
            internal bool Required;
            internal string Description;
            internal string Pattern;
            internal int? Minimum;
        }

        internal sealed class OperationFailure : InvalidOperationException
        {
            internal readonly int Index;
            internal readonly string Operation;
            internal readonly string Path;
            internal EndsMcpJsonED Command;

            internal OperationFailure(int index, string operation, string path, Exception cause) : base(cause.Message, cause)
            {
                Index = index;
                Operation = operation;
                Path = path;
            }

            internal EndsMcpJsonED ReadDetails()
            {
                EndsMcpJsonED details = EndsMcpJsonED.Obj();
                details.Set("operationIndex", EndsMcpJsonED.Num(Index));
                details.Set("operation", EndsMcpJsonED.Str(Operation));
                details.Set("path", EndsMcpJsonED.Str(Path));
                details.Set("code", EndsMcpJsonED.Str("invalid_operation"));

                foreach (string name in new[] { "dialog", "node", "sourceNode", "index", "sourceIndex", "port" })
                {
                    EndsMcpJsonED value = Command?.Get(name);

                    if (value != null) details.Set(name, value);
                }

                return details;
            }
        }

        #endregion FIELDS

        #region METADATA

        private static OperationArgument DescribeArgument(string operation, string definition)
        {
            string[] parts = definition.Split(':');
            OperationArgument argument = new OperationArgument { Name = parts[0], ContractType = parts[1].TrimEnd('?'), Required = !parts[1].EndsWith("?", StringComparison.Ordinal) };
            argument.Description = "Value for " + argument.Name + ".";

            switch (argument.ContractType)
            {
                case "string": argument.ValueType = typeof(string); break;
                case "boolean": argument.ValueType = typeof(bool); break;
                case "integer": argument.ValueType = typeof(int); break;
                case "array": argument.ValueType = typeof(List<string>); break;
                case nameof(NodeType): argument.ValueType = typeof(NodeType); break;
                case nameof(Vector2): argument.ValueType = typeof(Vector2); break;
                default:
                    foreach (Type model in _models.Keys)
                    {
                        if (model.Name == argument.ContractType) argument.ValueType = model;
                    }

                    break;
            }

            if (argument.ValueType == null) throw new InvalidOperationException("Unregistered operation argument type: " + argument.ContractType);

            switch (argument.Name)
            {
                case "dialog": argument.Description = "Exact dialog name in the selected database."; break;
                case "node": case "sourceNode": case "to":
                    argument.Description = "Exact full node id in the selected dialog, for example LNN.5. An empty to disconnects the port.";
                    break;
                case "id":
                    argument.Description = "Optional new permanent node id with its type prefix and an unused number for this node type within the selected dialog. Omit to allocate it. Existing ids cannot be renamed.";
                    argument.Pattern = EndsNodeIdentityED.IdPattern;
                    break;
                case "ids":
                    argument.Description = "Optional replacement ids in source node order, matching each source node type. Omit to preserve the ids and links in the copied dialog.";
                    break;
                case "port":
                    argument.Description = "Outgoing port: next for Start or Line, true or false for Condition, choice:N for Choice with zero-based N, redirect for Redirect.";
                    argument.Pattern = "^(next|true|false|choice:(0|[1-9][0-9]*)|redirect)$";
                    break;
                case "index": case "sourceIndex":
                    argument.Description = "Zero-based position in the current list. Removing an entry shifts later indices.";

                    if (operation == "dialog.move") argument.Description = "Zero-based position in the complete database dialog list, across all categories. Runtime initiators evaluate candidates in this order.";

                    argument.Minimum = 0;
                    break;
                case "patch": argument.Description = "Registered fields to change. Nested objects are patched, supplied lists replace the entire list. Preserve unchanged read-only diagnostics in list entries."; break;
                case "key": argument.Description = "Localization key: {dialogName}.{fullNodeId}.line or {dialogName}.{fullNodeId}.{optionId}, for example Test.CHN.2.OPT.3. The dialog name is literal and cannot contain dots. Unscoped node addresses must resolve uniquely."; break;
                case "language": argument.Description = "Existing language code from the database catalog."; break;
                case "asset": argument.Description = "Existing imported AudioClip asset path, empty to clear the line voice clip."; break;
                case "copyFrom": argument.Description = "Existing character whose sprite slot names should be copied."; break;
                case "newName": argument.Description = "New name. Shared rules normalize and validate names and update references."; break;
                case "slot": argument.Description = "Exact sprite slot name belonging to the selected character."; break;
                case "category": argument.Description = "Existing category. For a Redirect use category with targetDialog rather than a node edge."; break;
                case "targetDialog": argument.Description = "Exact eligible target in the selected category: a locked dialog or one without evaluable start conditions. An empty value clears the explicit target."; break;
            }

            if ((operation == "dialog.add" && argument.Name == "name") || ((operation == "dialog.rename" || operation == "dialog.duplicate") && argument.Name == "newName"))
            {
                argument.Pattern = "^[^.]*$";
                argument.Description = "Dialog name without dots. Dots are reserved for localization key separators.";
            }

            return argument;
        }

        private static EndsMcpJsonED ArgumentSchema(OperationArgument argument, bool input = true)
        {
            EndsMcpJsonED schema;

            if (input) schema = InputValueSchema(argument.ValueType, true);
            else schema = ValueSchema(argument.ValueType);

            if (schema.Get("anyOf") != null) schema = schema.Get("anyOf").Items[0];

            schema.Remove("nullable");

            schema.Set("contractType", EndsMcpJsonED.Str(argument.ContractType));
            schema.Set("description", EndsMcpJsonED.Str(argument.Description));

            if (argument.Pattern != null) schema.Set("pattern", EndsMcpJsonED.Str(argument.Pattern));
            if (argument.Minimum.HasValue) schema.Set("minimum", EndsMcpJsonED.Num(argument.Minimum.Value));

            return schema;
        }

        #endregion METADATA

        #region INPUT VALIDATION

        private static void ValidateArguments(Operation operation, EndsMcpJsonED command, int index)
        {
            HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal) { "op" };

            foreach (OperationArgument argument in operation.Arguments)
            {
                allowed.Add(argument.Name);
                EndsMcpJsonED value = command.Get(argument.Name);

                try
                {
                    if (value == null && argument.Required) throw new InvalidOperationException(operation.Name + " requires " + argument.Name + ".");
                    if (value == null) continue;
                    if (value.Type == EndsMcpJsonED.Kind.Null) throw new InvalidOperationException(argument.Name + " cannot be null. Omit an optional argument or use its explicit empty value.");

                    if (_models.ContainsKey(argument.ValueType)) RequireObject(value);
                    else ReadInput(argument.ValueType, value);

                    if (argument.Pattern != null && !Regex.IsMatch(String(value), argument.Pattern)) throw new InvalidOperationException(argument.Name + " has an invalid format. " + argument.Description);
                    if (argument.Minimum.HasValue && Integer(value) < argument.Minimum.Value) throw new InvalidOperationException(argument.Name + " must be at least " + argument.Minimum.Value + ".");
                }
                catch (Exception exception)
                {
                    throw new OperationFailure(index, operation.Name, "operations[" + index + "]." + argument.Name, exception);
                }
            }

            foreach (string key in command.Keys)
            {
                if (!allowed.Contains(key)) throw new OperationFailure(index, operation.Name, "operations[" + index + "]." + key, new InvalidOperationException(operation.Name + " does not accept " + key + "."));
            }
        }

        #endregion INPUT VALIDATION
    }
}
#endif