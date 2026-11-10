#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region MODEL COVERAGE

        internal static List<string> FindUnclassifiedFields(Type modelType)
        {
            Type registered = modelType;

            while (registered != null && !_models.ContainsKey(registered)) registered = registered.BaseType;

            if (registered == null) throw new ArgumentException("The type is outside the graph contract.", nameof(modelType));

            HashSet<string> classified = new HashSet<string>();

            foreach (EndsDialogRulesED.ModelField field in _models[registered])
            {
                classified.Add(field.Source);
            }

            List<string> missing = new List<string>();

            for (Type current = modelType; current != null && current != typeof(object) && current != typeof(ScriptableObject); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false)) continue;
                    if (classified.Contains(field.Name)) continue;

                    missing.Add(field.Name);
                }
            }

            return missing;
        }

        internal static void EnsureCoverage()
        {
            if (_coverageChecked) return;

            foreach (Type model in _models.Keys)
            {
                List<string> missing = FindUnclassifiedFields(model);

                if (missing.Count > 0) throw new InvalidOperationException("Unregistered graph fields in " + model.Name + ": " + string.Join(", ", missing));
            }

            _coverageChecked = true;
        }

        #endregion MODEL COVERAGE

        #region READ AND PATCH

        internal static EndsMcpJsonED ReadModel(object model)
        {
            EnsureCoverage();

            if (model == null) return EndsMcpJsonED.Null();

            return ReadValue(model.GetType(), model);
        }

        internal static T Snapshot<T>(T model) where T : class
        {
            if (model == null) return null;

            EnsureCoverage();

            return EndsDialogRulesED.Snapshot(model);
        }

        internal static EndsMcpJsonED ReadEditableModel(object model)
        {
            EndsMcpJsonED input = ReadModel(model);

            foreach (EndsDialogRulesED.ModelField field in _models[model.GetType()])
            {
                if (field.Identity) input.Remove(field.Name);
            }

            return input;
        }

        /// <summary> ENDS. Builds a stored field patch, an optional root field filter selects an operation's scope before reading values, nested differences retain their complete existing semantics. </summary>
        internal static EndsMcpJsonED Difference(object before, object after, Func<EndsDialogRulesED.ModelField, bool> includeField = null)
        {
            if (before == null || after == null) throw new InvalidOperationException("A field patch requires existing before and after models.");
            if (before.GetType() != after.GetType()) throw new InvalidOperationException("A patch cannot change model type.");

            EndsMcpJsonED patch = EndsMcpJsonED.Obj();

            foreach (EndsDialogRulesED.ModelField field in _models[before.GetType()])
            {
                if (field.Internal || includeField != null && !includeField(field)) continue;

                object oldValue = field.Read(before);
                object newValue = field.Read(after);
                EndsMcpJsonED oldJson = ReadValue(field.ValueType, oldValue);
                EndsMcpJsonED newJson = ReadValue(field.ValueType, newValue);

                if (oldJson.Write() == newJson.Write()) continue;
                if (field.Identity) throw new InvalidOperationException("Use a dedicated operation to change " + field.Name + ".");

                if (oldValue != null && newValue != null && _models.ContainsKey(field.ValueType)) patch.Set(field.Name, Difference(oldValue, newValue));
                else patch.Set(field.Name, newJson);
            }

            return patch;
        }

        internal static void ApplyModelPatch(object model, EndsMcpJsonED patch, DialogDatabase database = null)
        {
            RequireObject(patch);
            EndsDialogRulesED.ModelField[] fields = _models[model.GetType()];
            object before = null;

            if (EndsDialogRulesED.GetReferenceIdentity(model) != null) before = EndsDialogRulesED.Snapshot(model);
            HashSet<string> supplied = new HashSet<string>(patch.Keys);

            foreach (string name in patch.Keys)
            {
                EndsDialogRulesED.ModelField selected = null;

                foreach (EndsDialogRulesED.ModelField field in fields)
                {
                    if (field.Name == name) selected = field;
                }

                if (selected == null || selected.Internal) throw new InvalidOperationException("Unknown contract field: " + model.GetType().Name + "." + name);
                if (selected.Identity) throw new InvalidOperationException("Use a dedicated operation to change " + name + ".");
                if (selected.Diagnostic) continue;

                EndsMcpJsonED input = patch.Get(name);
                object current = selected.Read(model);

                if (input.Type == EndsMcpJsonED.Kind.Object && current != null && _models.ContainsKey(selected.ValueType)) ApplyModelPatch(current, input, database);
                else selected.Write(model, ReadInput(selected.ValueType, input, current, database, false, selected.Name));
            }

            EndsDialogRulesED.PrepareModelChange(before, model, supplied, database);

            foreach (EndsDialogRulesED.ModelField field in fields)
            {
                if (!field.Diagnostic || !supplied.Contains(field.Name)) continue;
                EndsDialogRulesED.RequireDiagnosticValue(field, field.Read(model), ReadInput(field.ValueType, patch.Get(field.Name), null, null, true));
            }
        }

        private static EndsMcpJsonED ReadValue(Type type, object value)
        {
            if (value == null) return EndsMcpJsonED.Null();
            if (type == typeof(string)) return EndsMcpJsonED.Str((string)value);
            if (type == typeof(bool)) return EndsMcpJsonED.Boolean((bool)value);
            if (type == typeof(int)) return EndsMcpJsonED.Num((int)value);
            if (type == typeof(float)) return EndsMcpJsonED.Num((float)value);
            if (type.IsEnum) return EndsMcpJsonED.Str(value.ToString());

            if (type == typeof(Color))
            {
                Color color = (Color)value;
                EndsMcpJsonED components = EndsMcpJsonED.Obj();
                components.Set("r", EndsMcpJsonED.Num(color.r));
                components.Set("g", EndsMcpJsonED.Num(color.g));
                components.Set("b", EndsMcpJsonED.Num(color.b));
                components.Set("a", EndsMcpJsonED.Num(color.a));

                return components;
            }

            if (type == typeof(Vector2))
            {
                Vector2 position = (Vector2)value;
                EndsMcpJsonED point = EndsMcpJsonED.Obj();
                point.Set("x", EndsMcpJsonED.Num(position.x));
                point.Set("y", EndsMcpJsonED.Num(position.y));

                return point;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type) && type != typeof(DialogDatabase)) return ReadAsset((UnityEngine.Object)value);

            if (typeof(IList).IsAssignableFrom(type))
            {
                EndsMcpJsonED array = EndsMcpJsonED.Arr();
                Type itemType = type.GetGenericArguments()[0];

                foreach (object item in (IList)value)
                {
                    array.Add(ReadValue(itemType, item));
                }

                return array;
            }

            if (!_models.TryGetValue(type, out EndsDialogRulesED.ModelField[] fields)) throw new InvalidOperationException("No graph contract for " + type.Name + ".");

            EndsMcpJsonED result = EndsMcpJsonED.Obj();

            foreach (EndsDialogRulesED.ModelField field in fields)
            {
                if (field.Internal) continue;

                result.Set(field.Name, ReadValue(field.ValueType, field.Read(value)));
            }

            return result;
        }

        private static object ReadInput(Type type, EndsMcpJsonED input, object previous = null, DialogDatabase database = null, bool snapshot = false, string fieldPath = "")
        {
            if (input == null || input.Type == EndsMcpJsonED.Kind.Null)
            {
                if (type.IsValueType) throw new InvalidOperationException(type.Name + " cannot be null.");

                return null;
            }

            if (type == typeof(string)) return String(input);
            if (type == typeof(bool)) return Boolean(input);
            if (type == typeof(int)) return Integer(input);
            if (type == typeof(float)) return Number(input);

            if (type == typeof(Color))
            {
                if (input.Type == EndsMcpJsonED.Kind.String) return EndsDialogRulesED.ParseNameColor(String(input));

                RequireObject(input);

                if (input.Keys.Count != EndsDialogRulesED.COLOR_COMPONENT_COUNT || input.Get("r") == null || input.Get("g") == null || input.Get("b") == null || input.Get("a") == null) throw new InvalidOperationException("Color requires r, g, b and a.");

                Color color = new Color(Number(input.Get("r")), Number(input.Get("g")), Number(input.Get("b")), Number(input.Get("a")));
                EndsDialogRulesED.RequireNameColor(color);

                return color;
            }

            if (type.IsEnum)
            {
                string name = String(input);

                if (!Enum.TryParse(type, name, true, out object value) || !Enum.IsDefined(type, value)) throw new InvalidOperationException("Unknown " + type.Name + ": " + name + ".");

                return value;
            }

            if (type == typeof(Vector2))
            {
                RequireObject(input);

                if (input.Keys.Count != 2 || input.Get("x") == null || input.Get("y") == null) throw new InvalidOperationException("Position requires x and y.");

                return new Vector2(Number(input.Get("x")), Number(input.Get("y")));
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return ReadAssetInput(type, input);

            if (typeof(IList).IsAssignableFrom(type))
            {
                RequireArray(input);
                IList list = (IList)Activator.CreateInstance(type);
                Type itemType = type.GetGenericArguments()[0];

                IList oldList = previous as IList;
                bool identifiedBlocks = false;

                if (oldList != null)
                {
                    foreach (object existing in oldList)
                    {
                        if (!(existing is DialogBlockData block) || string.IsNullOrEmpty(block.BlockId)) continue;

                        identifiedBlocks = true;

                        break;
                    }
                }

                for (int i = 0; i < input.Items.Count; i++)
                {
                    EndsMcpJsonED item = input.Items[i];
                    object oldItem = null;
                    bool hadEntry = oldList != null && i < oldList.Count;

                    if (hadEntry) oldItem = oldList[i];

                    if (!snapshot) EndsDialogRulesED.RequireListEntry(item == null || item.Type == EndsMcpJsonED.Kind.Null, oldItem, hadEntry, fieldPath + "[" + i + "]");
                    if (!snapshot && item?.Type == EndsMcpJsonED.Kind.Object && oldList != null && _models.ContainsKey(itemType))
                    {
                        object supplied = ReadInput(itemType, item, null, database, true);
                        string identity = EndsDialogRulesED.GetReferenceIdentity(supplied);

                        if (supplied is DialogBlockData block && (identifiedBlocks || !string.IsNullOrEmpty(block.BlockId)))
                        {
                            oldItem = null;

                            foreach (object candidate in oldList)
                            {
                                if (!(candidate is DialogBlockData existing) || string.IsNullOrEmpty(block.BlockId) || existing.BlockId != block.BlockId) continue;

                                oldItem = candidate;

                                break;
                            }
                        }
                        else if (identity != null && (oldItem == null || identity != EndsDialogRulesED.GetReferenceIdentity(oldItem)))
                        {
                            foreach (object candidate in oldList)
                            {
                                if (candidate == null || identity != EndsDialogRulesED.GetReferenceIdentity(candidate)) continue;

                                oldItem = candidate;

                                break;
                            }
                        }
                    }

                    list.Add(ReadInput(itemType, item, oldItem, database, snapshot, fieldPath + "[" + i + "]"));
                }

                return list;
            }

            RequireObject(input);
            EndsDialogRulesED.ModelField[] fields = _models[type];
            object result;

            if (snapshot) result = Activator.CreateInstance(type);
            else result = EndsDialogRulesED.CreateModel(type, database);

            foreach (string name in input.Keys)
            {
                EndsDialogRulesED.ModelField selected = null;

                foreach (EndsDialogRulesED.ModelField field in fields)
                {
                    if (field.Name == name) selected = field;
                }

                if (selected == null || selected.Internal) throw new InvalidOperationException("Unknown contract field: " + type.Name + "." + name);

                if (!snapshot && selected.Diagnostic) continue;

                object oldValue = null;

                if (previous != null) oldValue = selected.Read(previous);

                selected.Write(result, ReadInput(selected.ValueType, input.Get(name), oldValue, database, snapshot, fieldPath + "." + name));
            }

            if (!snapshot)
            {
                if (result is DialogBlockData block)
                {
                    string identity = (previous as DialogBlockData)?.BlockId;

                    if (previous is DialogBlockData && !string.IsNullOrEmpty(block.BlockId) && block.BlockId != identity) throw new InvalidOperationException("Block identity is read-only. Preserve the existing BlockId or omit it when creating a block.");

                    block.BlockId = identity;
                }

                EndsDialogRulesED.PrepareModelChange(previous, result, new HashSet<string>(input.Keys), database);

                foreach (EndsDialogRulesED.ModelField field in fields)
                {
                    if (!field.Diagnostic || input.Get(field.Name) == null) continue;
                    EndsDialogRulesED.RequireDiagnosticValue(field, field.Read(result), ReadInput(field.ValueType, input.Get(field.Name), null, null, true));
                }
            }

            return result;
        }

        internal static T ReadInputModel<T>(EndsMcpJsonED input, T previous = null, DialogDatabase database = null) where T : class
        {
            return (T)ReadInput(typeof(T), input, previous, database);
        }

        internal static EndsMcpJsonED Point(float x, float y)
        {
            return ReadValue(typeof(Vector2), new Vector2(x, y));
        }

        internal static bool Boolean(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Bool) throw new InvalidOperationException("Expected a boolean.");

            return input.Bool;
        }

        internal static string String(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.String) throw new InvalidOperationException("Expected a string.");

            return input.Text;
        }

        internal static int Integer(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Number || !int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) throw new InvalidOperationException("Expected an integer.");

            return value;
        }

        internal static float Number(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Number || !float.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Expected a finite number.");

            return value;
        }

        private static void RequireObject(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Object || input.Keys == null) throw new InvalidOperationException("Expected an object.");
        }

        private static void RequireArray(EndsMcpJsonED input)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Array || input.Items == null) throw new InvalidOperationException("Expected an array.");
        }

        #endregion READ AND PATCH

        #region ASSET REFERENCES

        private static EndsMcpJsonED ReadAsset(UnityEngine.Object asset)
        {
            if (asset == null) return EndsMcpJsonED.Null();

            EndsMcpJsonED reference = EndsMcpJsonED.Obj();
            string path = AssetDatabase.GetAssetPath(asset);
            reference.Set("asset", EndsMcpJsonED.Str(path));

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
            {
                reference.Set("guid", EndsMcpJsonED.Str(guid));
                reference.Set("localId", EndsMcpJsonED.Str(localId.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                reference.Set("instanceId", EndsUnityObjectReferenceED.EncodeTransientId(asset));
            }

            return reference;
        }

        private static UnityEngine.Object ReadAssetInput(Type type, EndsMcpJsonED input)
        {
            string path;
            string localId = null;

            if (input.Type == EndsMcpJsonED.Kind.String) path = String(input);
            else
            {
                RequireObject(input);
                path = String(input.Get("asset"));

                foreach (string key in input.Keys)
                {
                    if (key != "asset" && key != "guid" && key != "localId" && key != "instanceId") throw new InvalidOperationException("Unknown asset reference field: " + key + ".");
                }

                if (input.Get("guid") != null)
                {
                    string guidPath = AssetDatabase.GUIDToAssetPath(String(input.Get("guid")));

                    if (string.IsNullOrEmpty(guidPath)) throw new InvalidOperationException("Asset guid is unavailable: " + String(input.Get("guid")) + ". Expected " + type.Name + ".");
                    if (!string.IsNullOrEmpty(path) && path != guidPath) throw new InvalidOperationException("Asset path and guid refer to different assets.");

                    path = guidPath;
                }

                if (input.Get("localId") != null) localId = String(input.Get("localId"));

                if (string.IsNullOrEmpty(path) && input.Get("instanceId") != null)
                {
                    if (!EndsUnityObjectReferenceED.TryResolveTransientId(input.Get("instanceId"), out UnityEngine.Object transient)) throw new InvalidOperationException("Invalid transient asset identifier. Expected " + type.Name + ".");

                    if (transient == null) throw new InvalidOperationException("Transient asset is unavailable. Expected " + type.Name + ".");
                    if (!type.IsInstanceOfType(transient)) throw new InvalidOperationException("Asset type mismatch. Expected " + type.Name + ", found " + transient.GetType().Name + ".");

                    return transient;
                }
            }

            if (string.IsNullOrEmpty(path)) return null;

            UnityEngine.Object[] imported = AssetDatabase.LoadAllAssetsAtPath(path);

            if (imported.Length == 0) throw new InvalidOperationException("Asset path is unavailable: " + path + ". Expected " + type.Name + ". Import an asset before assigning it.");

            if (localId != null)
            {
                foreach (UnityEngine.Object candidate in imported)
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string guid, out long candidateId)) continue;
                    if (candidateId.ToString(CultureInfo.InvariantCulture) != localId) continue;
                    if (!type.IsInstanceOfType(candidate)) throw new InvalidOperationException("Asset type mismatch at " + path + "/" + localId + ". Expected " + type.Name + ", found " + candidate.GetType().Name + ".");

                    return candidate;
                }

                throw new InvalidOperationException("Asset local id no longer exists: " + path + "/" + localId + ". Expected " + type.Name + ". Reread the imported asset identity.");
            }

            UnityEngine.Object loaded = AssetDatabase.LoadAssetAtPath(path, type);

            if (loaded == null)
            {
                HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);

                foreach (UnityEngine.Object candidate in imported)
                {
                    if (candidate != null) types.Add(candidate.GetType().Name);
                }

                throw new InvalidOperationException("Asset type mismatch at " + path + ". Expected " + type.Name + ", imported types: " + string.Join(", ", types) + ".");
            }

            return loaded;
        }

        #endregion ASSET REFERENCES
    }
}
#endif