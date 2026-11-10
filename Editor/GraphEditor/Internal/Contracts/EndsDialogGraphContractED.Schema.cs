#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region SCHEMAS

        internal static EndsMcpJsonED ReadSchema()
        {
            EnsureCoverage();
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("contractVersion", EndsMcpJsonED.Num(CONTRACT_VERSION));
            EndsMcpJsonED definitions = EndsMcpJsonED.Obj();

            foreach (Type model in _models.Keys)
            {
                definitions.Set(model.Name, ModelSchema(model));
            }

            schema.Set("models", definitions);
            schema.Set("operations", ReadOperationSchema());

            return schema;
        }

        internal static EndsMcpJsonED ReadModelSchema(Type type)
        {
            EnsureCoverage();

            return ModelSchema(type);
        }

        private static EndsMcpJsonED ModelSchema(Type type)
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();

            foreach (EndsDialogRulesED.ModelField field in _models[type])
            {
                if (field.Internal) continue;

                EndsMcpJsonED property = ValueSchema(field.ValueType);
                property.Set("source", EndsMcpJsonED.Str(field.Source));
                property.Set("identity", EndsMcpJsonED.Boolean(field.Identity));
                ApplyFieldSchema(type, field, property);
                properties.Set(field.Name, property);
            }

            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            schema.Set("additionalProperties", EndsMcpJsonED.Boolean(false));

            return schema;
        }

        private static EndsMcpJsonED ValueSchema(Type type)
        {
            if (type == typeof(Color)) return NameColorSchema(false);

            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            string kind = "object";

            if (type == typeof(string) || type.IsEnum) kind = "string";
            if (type == typeof(bool)) kind = "boolean";
            if (type == typeof(int)) kind = "integer";
            if (type == typeof(float)) kind = "number";
            if (typeof(IList).IsAssignableFrom(type)) kind = "array";

            schema.Set("type", EndsMcpJsonED.Str(kind));
            schema.Set("nullable", EndsMcpJsonED.Boolean(!type.IsValueType));

            if (_models.ContainsKey(type)) schema.Set("model", EndsMcpJsonED.Str(type.Name));
            if (typeof(IList).IsAssignableFrom(type)) schema.Set("items", ValueSchema(type.GetGenericArguments()[0]));
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) && type != typeof(DialogDatabase)) schema.Set("assetType", EndsMcpJsonED.Str(type.Name));

            if (type.IsEnum)
            {
                EndsMcpJsonED values = EndsMcpJsonED.Arr();

                foreach (string name in Enum.GetNames(type))
                {
                    values.Add(EndsMcpJsonED.Str(name));
                }

                schema.Set("enum", values);
            }

            return schema;
        }

        #endregion SCHEMAS

        #region OPERATION SCHEMA

        internal static EndsMcpJsonED ReadOperationSchema()
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Arr();

            foreach (Operation operation in _operations.Values)
            {
                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                EndsMcpJsonED properties = EndsMcpJsonED.Obj();
                EndsMcpJsonED required = EndsMcpJsonED.Arr();
                item.Set("op", EndsMcpJsonED.Str(operation.Name));
                item.Set("description", EndsMcpJsonED.Str(operation.Description));

                foreach (OperationArgument argument in operation.Arguments)
                {
                    EndsMcpJsonED field = ArgumentSchema(argument, false);

                    if (argument.Required) required.Add(EndsMcpJsonED.Str(argument.Name));

                    properties.Set(argument.Name, field);
                }

                item.Set("properties", properties);
                item.Set("required", required);
                item.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
                schema.Add(item);
            }

            return schema;
        }

        #endregion OPERATION SCHEMA

        #region INPUT SCHEMA

        private static EndsMcpJsonED NameColorSchema(bool acceptHex)
        {
            EndsMcpJsonED rgba = InputType("object");
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            EndsMcpJsonED required = EndsMcpJsonED.Arr();

            foreach (string component in new[] { "r", "g", "b", "a" })
            {
                EndsMcpJsonED number = InputType("number");
                number.Set("minimum", EndsMcpJsonED.Num(EndsDialogRulesED.NAME_COLOR_MIN));
                number.Set("maximum", EndsMcpJsonED.Num(EndsDialogRulesED.NAME_COLOR_MAX));
                properties.Set(component, number);
                required.Add(EndsMcpJsonED.Str(component));
            }

            rgba.Set("properties", properties);
            rgba.Set("required", required);
            rgba.Set("additionalProperties", EndsMcpJsonED.Boolean(false));

            if (!acceptHex) return rgba;

            EndsMcpJsonED hex = InputType("string");
            hex.Set("pattern", EndsMcpJsonED.Str(EndsDialogRulesED.NAME_COLOR_HEX_PATTERN));
            EndsMcpJsonED variants = EndsMcpJsonED.Arr();
            variants.Add(rgba);
            variants.Add(hex);
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("oneOf", variants);

            return schema;
        }

        internal static EndsMcpJsonED ReadApplyInputSchema()
        {
            EnsureCoverage();
            EndsMcpJsonED definitions = EndsMcpJsonED.Obj();

            foreach (Type type in _models.Keys)
            {
                definitions.Set(type.Name + "Input", InputModelSchema(type, false));
                definitions.Set(type.Name + "Patch", InputModelSchema(type, true));
            }

            EndsMcpJsonED variants = EndsMcpJsonED.Arr();

            foreach (Operation operation in _operations.Values)
            {
                EndsMcpJsonED row = EndsMcpJsonED.Obj();
                EndsMcpJsonED properties = EndsMcpJsonED.Obj();
                EndsMcpJsonED required = EndsMcpJsonED.Arr();
                EndsMcpJsonED op = InputType("string");
                op.Set("const", EndsMcpJsonED.Str(operation.Name));
                properties.Set("op", op);
                required.Add(EndsMcpJsonED.Str("op"));

                foreach (OperationArgument argument in operation.Arguments)
                {
                    properties.Set(argument.Name, ArgumentSchema(argument));

                    if (argument.Required) required.Add(EndsMcpJsonED.Str(argument.Name));
                }

                row.Set("type", EndsMcpJsonED.Str("object"));
                row.Set("description", EndsMcpJsonED.Str(operation.Description));
                row.Set("properties", properties);
                row.Set("required", required);
                row.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
                variants.Add(row);
            }

            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("oneOf", variants);
            EndsMcpJsonED operations = InputType("array");
            operations.Set("items", item);
            EndsMcpJsonED rootProperties = EndsMcpJsonED.Obj();
            rootProperties.Set("operations", operations);
            rootProperties.Set("database", InputType("string"));
            EndsMcpJsonED revision = InputType("integer");
            revision.Set("minimum", EndsMcpJsonED.Num(0));
            rootProperties.Set("expectedRevision", revision);
            rootProperties.Set("save", InputType("boolean"));
            EndsMcpJsonED root = InputType("object");
            root.Set("properties", rootProperties);
            root.Set("$defs", definitions);
            EndsMcpJsonED rootRequired = EndsMcpJsonED.Arr();
            rootRequired.Add(EndsMcpJsonED.Str("operations"));
            rootRequired.Add(EndsMcpJsonED.Str("expectedRevision"));
            root.Set("required", rootRequired);
            root.Set("additionalProperties", EndsMcpJsonED.Boolean(false));

            return root;
        }

        private static EndsMcpJsonED InputModelSchema(Type type, bool patch)
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();

            foreach (EndsDialogRulesED.ModelField field in _models[type])
            {
                if (field.Internal) continue;
                if (patch && field.Identity) continue;
                if (patch && type == typeof(DialogDatabase) && field.Name != nameof(DialogDatabase.CurrentLanguage) && field.Name != nameof(DialogDatabase.VoiceClipsEnabled)) continue;
                if (patch && type == typeof(DialogEntry) && field.Name == nameof(DialogEntry.nodes)) continue;

                EndsMcpJsonED property = InputValueSchema(field.ValueType, patch);
                ApplyFieldSchema(type, field, property);
                properties.Set(field.Name, property);
            }

            EndsMcpJsonED schema = InputType("object");
            schema.Set("properties", properties);
            schema.Set("additionalProperties", EndsMcpJsonED.Boolean(false));

            return schema;
        }

        private static EndsMcpJsonED InputValueSchema(Type type, bool patch)
        {
            EndsMcpJsonED schema;

            if (_models.ContainsKey(type))
            {
                string suffix = "Input";

                if (patch) suffix = "Patch";

                schema = InputReference(type.Name + suffix);
            }
            else if (typeof(IList).IsAssignableFrom(type))
            {
                schema = InputType("array");
                schema.Set("items", InputValueSchema(type.GetGenericArguments()[0], false));
            }
            else if (type == typeof(Vector2))
            {
                schema = InputType("object");
                EndsMcpJsonED coordinates = EndsMcpJsonED.Obj();
                coordinates.Set("x", InputType("number"));
                coordinates.Set("y", InputType("number"));
                schema.Set("properties", coordinates);
                EndsMcpJsonED required = EndsMcpJsonED.Arr();
                required.Add(EndsMcpJsonED.Str("x"));
                required.Add(EndsMcpJsonED.Str("y"));
                schema.Set("required", required);
                schema.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
            }
            else if (type == typeof(Color)) schema = NameColorSchema(true);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                EndsMcpJsonED assetProperties = EndsMcpJsonED.Obj();
                assetProperties.Set("asset", InputType("string"));
                assetProperties.Set("guid", InputType("string"));
                assetProperties.Set("localId", InputType("string"));
                assetProperties.Set("instanceId", InputType("integer"));
                EndsMcpJsonED asset = InputType("object");
                asset.Set("properties", assetProperties);
                asset.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
                EndsMcpJsonED variants = EndsMcpJsonED.Arr();
                variants.Add(InputType("string"));
                variants.Add(asset);
                schema = EndsMcpJsonED.Obj();
                schema.Set("anyOf", variants);
            }
            else
            {
                schema = ValueSchema(type);
                schema.Remove("nullable");
            }

            if (type.IsValueType) return schema;

            EndsMcpJsonED nullable = EndsMcpJsonED.Obj();
            EndsMcpJsonED options = EndsMcpJsonED.Arr();
            options.Add(schema);
            options.Add(InputType("null"));
            nullable.Set("anyOf", options);

            return nullable;
        }

        private static void ApplyFieldSchema(Type owner, EndsDialogRulesED.ModelField field, EndsMcpJsonED schema)
        {
            EndsMcpJsonED constraints = schema;

            if (schema.Get("anyOf")?.Items != null) constraints = schema.Get("anyOf").Items[0];

            if (EndsDialogRulesED.IsRequiredCollection(owner, field.Name, field.ValueType))
            {
                schema.Remove("anyOf");

                if (constraints != schema)
                {
                    foreach (string key in constraints.Keys)
                    {
                        schema.Set(key, constraints.Get(key));
                    }
                }

                schema.Set("nullable", EndsMcpJsonED.Boolean(false));
                constraints = schema;
            }

            EndsDialogRulesED.NumberRange? range = EndsDialogRulesED.GetNumberRange(owner, field.Name);
            string pattern = EndsDialogRulesED.GetStringPattern(owner, field.Name);

            if (range.HasValue)
            {
                constraints.Set("minimum", EndsMcpJsonED.Num(range.Value.Minimum));
                constraints.Set("maximum", EndsMcpJsonED.Num(range.Value.Maximum));
            }

            if (pattern != null) constraints.Set("pattern", EndsMcpJsonED.Str(pattern));
            if (field.Identity || field.Diagnostic) schema.Set("readOnly", EndsMcpJsonED.Boolean(true));

            if (EndsDialogRulesED.RequiresListEntries(field.ValueType))
            {
                EndsMcpJsonED items = constraints.Get("items");

                if (items?.Get("anyOf")?.Items != null) constraints.Set("items", items.Get("anyOf").Items[0]);
                else if (items != null) items.Set("nullable", EndsMcpJsonED.Boolean(false));
            }
        }

        private static EndsMcpJsonED InputType(string type)
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str(type));

            return schema;
        }

        private static EndsMcpJsonED InputReference(string name)
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("$ref", EndsMcpJsonED.Str("#/$defs/" + name));

            return schema;
        }

        #endregion INPUT SCHEMA
    }
}
#endif
