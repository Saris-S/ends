#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        #region FIELDS

        /// <summary> ENDS. Adapts stored model access to snapshots and the MCP contract, node members originate from EndsNodeStructureED. </summary>
        internal sealed class ModelField
        {
            private readonly Func<object, object> _read;
            private readonly Action<object, object> _write;

            /// <summary> ENDS. Names the serialized field for storage coverage, property access can use a different public member. </summary>
            internal readonly string Source;
            /// <summary> ENDS. Names the existing contract field, independent of display captions and logical groups. </summary>
            internal readonly string Name;
            /// <summary> ENDS. Supplies the physical member type to contract conversion and recursive snapshots. </summary>
            internal readonly Type ValueType;
            /// <summary> ENDS. Requires a dedicated operation to change this contract identity, row keys can remain editable. </summary>
            internal readonly bool Identity;
            /// <summary> ENDS. Includes private storage in snapshots while omitting it from the external contract. </summary>
            internal readonly bool Internal;
            /// <summary> ENDS. Identifies derived reference diagnostics using the node structure's field role. </summary>
            internal readonly bool Diagnostic;

            /// <summary> ENDS. Binds cached member access and backend policy once, does not create or mutate a model. </summary>
            internal ModelField(string source, string name, Type valueType, Func<object, object> read, Action<object, object> write, bool identity = false, bool internalField = false, bool diagnostic = false)
            {
                Source = source;
                Name = name;
                ValueType = valueType;
                _read = read;
                _write = write;
                Identity = identity;
                Internal = internalField;
                Diagnostic = diagnostic;
            }

            /// <summary> ENDS. Reads storage through the registered accessor without creating a snapshot. </summary>
            internal object Read(object model)
            {
                return _read(model);
            }

            /// <summary> ENDS. Writes storage for copying or an already authorized operation, callers own validation and persistence. </summary>
            internal void Write(object model, object value)
            {
                _write(model, value);
            }
        }

        private static readonly Dictionary<Type, ModelField[]> _models = BuildModels();

        /// <summary> ENDS. Shares the adapted storage registry across contracts, Rules and Session, node fields are defined in EndsNodeStructureED. </summary>
        internal static IReadOnlyDictionary<Type, ModelField[]> Models => _models;

        #endregion FIELDS

        #region MODEL REGISTRATION

        private static Dictionary<Type, ModelField[]> BuildModels()
        {
            Dictionary<Type, ModelField[]> models = new Dictionary<Type, ModelField[]>();
            Register<DialogDatabase>(models, "_currentLanguage:CurrentLanguage", "_categories:Categories", "_dialogs:Dialogs", "_availableLanguages:AvailableLanguages",
                "_characterNameColors:CharacterNameColors", "_variables:Variables", "_characterGroups:CharacterGroups", "_voiceClipsEnabled:VoiceClipsEnabled", "_nextNodeNumber:NextNodeNumber!~", "_nodeIdentityVersion:NodeIdentityVersion!");
            RegisterStructureTree(models, EndsNodeStructureED.DialogSchema);
            Register<VariableDefinition>(models, nameof(VariableDefinition.variableName) + "!", nameof(VariableDefinition.variableType), nameof(VariableDefinition.boolDefault), nameof(VariableDefinition.intDefault), nameof(VariableDefinition.floatDefault), nameof(VariableDefinition.stringDefault));
            Register<CharacterGroup>(models, nameof(CharacterGroup.characterName) + "!", nameof(CharacterGroup.sprites));
            Register<CharacterSprite>(models, nameof(CharacterSprite.spriteName) + "!", nameof(CharacterSprite.sprite));
            Register<CharacterNameColor>(models, nameof(CharacterNameColor.Name) + "!", "_color:Color", "_legacyColorHex:LegacyColorHex!~", nameof(CharacterNameColor.Translations));
            Register<NameTranslation>(models, nameof(NameTranslation.languageCode), nameof(NameTranslation.displayName));

            return models;
        }

        /// <summary> ENDS. Registers reachable schemas without model instances, private storage and contract identity restrictions remain backend policies. </summary>
        internal static void RegisterStructureTree<TModel>(Dictionary<Type, ModelField[]> models, EndsNodeStructureED.NodeModelDefinition<TModel> structure) where TModel : class
        {
            structure.VisitDefinitions(new StorageRegistrationVisitor(models));
        }

        private sealed class StorageRegistrationVisitor : EndsNodeStructureED.NodeDefinitionVisitor
        {
            private readonly Dictionary<Type, ModelField[]> _target;

            /// <summary> ENDS. Binds one registration walk to its storage registry without changing the shared schema definitions. </summary>
            internal StorageRegistrationVisitor(Dictionary<Type, ModelField[]> target)
            {
                _target = target;
            }

            /// <summary> ENDS. Adapts every reachable model, only private dialog storage and editable translation keys need backend-specific policy. </summary>
            internal override void VisitModel<TModel>(EndsNodeStructureED.NodeModelDefinition<TModel> structure)
            {
                if (typeof(TModel) == typeof(DialogEntry)) RegisterStructure(_target, structure, true, "_nodeIdentityKey:NodeIdentityKey!~", "_nextNodeNumbers:NextNodeNumbers!");
                else RegisterStructure(_target, structure, typeof(TModel) != typeof(LineText) && typeof(TModel) != typeof(ChoiceText));
            }
        }

        private static void RegisterStructure<TModel>(Dictionary<Type, ModelField[]> models, EndsNodeStructureED.NodeModelDefinition<TModel> structure, bool identityRequiresDedicatedOperation = true, params string[] additionalStorage) where TModel : class
        {
            List<ModelField> fields = new List<ModelField>();
            HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsNodeStructureED.NodeElementDefinition<TModel> element in structure.ElementsDepthFirst())
            {
                if (element.SourceMember == null) continue;

                var storage = (EndsNodeStructureED.INodeStoredField<TModel>)element;
                FieldInfo serialized = element.SourceMember as FieldInfo;

                if (serialized == null)
                {
                    for (Type owner = typeof(TModel); owner != null; owner = owner.BaseType)
                    {
                        foreach (FieldInfo candidate in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            foreach (FormerlySerializedAsAttribute alias in candidate.GetCustomAttributes<FormerlySerializedAsAttribute>())
                            {
                                if (alias.oldName != element.Name || !candidate.IsDefined(typeof(SerializeField), false)) continue;
                                if (serialized != null) throw new InvalidOperationException("Ambiguous serialized alias: " + typeof(TModel).Name + "." + element.Name);

                                serialized = candidate;
                            }
                        }
                    }
                }

                if (serialized == null || serialized.FieldType != storage.ValueType || storage.IsReadOnly) throw new InvalidOperationException("Invalid snapshot storage: " + typeof(TModel).Name + "." + element.Name);
                if (!sources.Add(serialized.Name)) throw new InvalidOperationException("Duplicate snapshot storage: " + typeof(TModel).Name + "." + serialized.Name);

                fields.Add(new ModelField(serialized.Name, element.Name, storage.ValueType, model => storage.ReadStoredValue((TModel)model), (model, value) => storage.WriteStoredValue((TModel)model, value),
                    identityRequiresDedicatedOperation && element.Role == EndsNodeStructureED.FieldRole.Identity, diagnostic: element.Role == EndsNodeStructureED.FieldRole.Diagnostic));
            }

            Register<TModel>(models, additionalStorage);

            foreach (ModelField field in models[typeof(TModel)])
            {
                if (!sources.Add(field.Source)) throw new InvalidOperationException("Duplicate snapshot storage: " + typeof(TModel).Name + "." + field.Source);

                fields.Add(field);
            }

            models[typeof(TModel)] = fields.ToArray();
        }

        private static void Register<T>(Dictionary<Type, ModelField[]> models, params string[] definitions)
        {
            Type modelType = typeof(T);
            ModelField[] fields = new ModelField[definitions.Length];

            for (int i = 0; i < definitions.Length; i++)
            {
                string definition = definitions[i];
                bool internalField = definition.EndsWith("~", StringComparison.Ordinal);

                if (internalField) definition = definition.Substring(0, definition.Length - 1);

                bool identity = definition.EndsWith("!", StringComparison.Ordinal);

                if (identity) definition = definition.Substring(0, definition.Length - 1);

                string[] parts = definition.Split(':');
                string source = parts[0];
                string name = source;

                if (parts.Length > 1) name = parts[1];

                FieldInfo field = modelType.GetField(source, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null) throw new InvalidOperationException("Contract field does not exist: " + modelType.Name + "." + source);

                fields[i] = new ModelField(source, name, field.FieldType, field.GetValue, field.SetValue, identity, internalField);
            }

            models.Add(modelType, fields);
        }

        #endregion MODEL REGISTRATION

        #region MODEL SNAPSHOTS

        /// <summary> ENDS. Compares all registered storage without JSON, includes identities, diagnostics, hidden branches, list order and Unity object references. </summary>
        internal static bool HasSameStoredValue(object original, object current)
        {
            if (ReferenceEquals(original, current)) return true;
            if (original == null || current == null || original.GetType() != current.GetType()) return false;

            if (original is System.Collections.IList originalItems && current is System.Collections.IList currentItems)
            {
                if (originalItems.Count != currentItems.Count) return false;

                for (int i = 0; i < originalItems.Count; i++)
                {
                    if (!HasSameStoredValue(originalItems[i], currentItems[i])) return false;
                }

                return true;
            }

            if (!Models.TryGetValue(original.GetType(), out ModelField[] fields)) return original.Equals(current);

            foreach (ModelField field in fields)
            {
                if (!HasSameStoredValue(field.Read(original), field.Read(current))) return false;
            }

            return true;
        }

        /// <summary> ENDS. Deep-copies all registered storage, including inactive payloads, hidden value alternatives and private identity state. </summary>
        internal static T Snapshot<T>(T model) where T : class
        {
            return (T)CopyModelValue(model);
        }

        /// <summary> ENDS. Restores a deep copy into an existing model of the same type, operation permissions do not restrict snapshot restoration. </summary>
        internal static void CopyInto<T>(T source, T target) where T : class
        {
            if (source == null || target == null) throw new InvalidOperationException("Copy source and target are required.");
            if (source.GetType() != target.GetType()) throw new InvalidOperationException("Copy source and target must use the same model type.");

            T copy = Snapshot(source);

            foreach (ModelField field in Models[copy.GetType()])
            {
                field.Write(target, field.Read(copy));
            }
        }

        private static object CopyModelValue(object value)
        {
            if (value == null) return null;

            Type type = value.GetType();

            if (value is System.Collections.IList list)
            {
                System.Collections.IList copy = (System.Collections.IList)Activator.CreateInstance(type);

                foreach (object item in list)
                {
                    copy.Add(CopyModelValue(item));
                }

                return copy;
            }

            if (!Models.TryGetValue(type, out ModelField[] fields)) return value;

            object model;

            if (type == typeof(DialogDatabase)) model = ScriptableObject.CreateInstance<DialogDatabase>();
            else model = Activator.CreateInstance(type);

            foreach (ModelField field in fields)
            {
                field.Write(model, CopyModelValue(field.Read(value)));
            }

            return model;
        }

        #endregion MODEL SNAPSHOTS

    }
}
#endif
