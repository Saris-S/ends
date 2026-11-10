#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed partial class EndsSessionED
    {
        #region FIELDS

        private const int PENDING_ARRAY_MATCH_LIMIT = 262144;

        #endregion FIELDS

        #region PENDING REPORT PROJECTION

        private static EndsMcpJsonED ProjectPendingValue(EndsMcpJsonED value, Type modelType)
        {
            if (value == null) return null;

            if (value.Type == EndsMcpJsonED.Kind.Null)
            {
                if (modelType == typeof(string)) return EndsMcpJsonED.Str("");
                if (modelType?.IsGenericType == true && modelType.GetGenericTypeDefinition() == typeof(List<>)) return EndsMcpJsonED.Arr();

                return value;
            }

            if (value.Type == EndsMcpJsonED.Kind.Array)
            {
                Type elementType = null;

                if (modelType?.IsGenericType == true) elementType = modelType.GetGenericArguments()[0];

                EndsMcpJsonED array = EndsMcpJsonED.Arr();

                foreach (EndsMcpJsonED item in value.Items)
                {
                    array.Add(ProjectPendingValue(item, elementType));
                }

                return array;
            }

            if (value.Type != EndsMcpJsonED.Kind.Object || modelType == null || !EndsDialogRulesED.Models.TryGetValue(modelType, out EndsDialogRulesED.ModelField[] definitions)) return value;

            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            string variableType = value.Get("VariableType")?.Text ?? value.Get("variableType")?.Text;
            string activeValue = null;

            if (int.TryParse(variableType, out int variableKind)) activeValue = ((ConditionVariableType)variableKind) + "Default";

            foreach (EndsDialogRulesED.ModelField field in definitions)
            {
                if (field.Internal || field.Source == "_nextNodeNumbers" || field.Source == "_nodeIdentityVersion") continue;
                if (activeValue != null && field.Source.EndsWith("Default", StringComparison.OrdinalIgnoreCase) && !field.Source.Equals(activeValue, StringComparison.OrdinalIgnoreCase)) continue;

                EndsMcpJsonED child = value.Get(field.Source);

                if (child == null) continue;

                string name = field.Source;

                if (name.EndsWith("Default", StringComparison.OrdinalIgnoreCase)) name = "DefaultValue";

                result.Set(name, ProjectPendingValue(child, field.ValueType));
            }

            return result;
        }

        private static bool IsPendingAtomicValue(EndsMcpJsonED value)
        {
            if (value?.Type != EndsMcpJsonED.Kind.Object) return false;

            return value.Get("instanceID") != null || value.Get("x") != null && value.Get("y") != null || value.Get("r") != null && value.Get("g") != null && value.Get("b") != null;
        }

        private static bool IsPendingEmptyValue(EndsMcpJsonED value)
        {
            if (value == null || value.Type == EndsMcpJsonED.Kind.Null) return true;
            if (value.Type == EndsMcpJsonED.Kind.String) return string.IsNullOrEmpty(value.Text);
            if (value.Type == EndsMcpJsonED.Kind.Array) return value.Items.Count == 0;

            return value.Type == EndsMcpJsonED.Kind.Object && value.Get("instanceID")?.Text == "0";
        }

        private static void AddPendingField(EndsMcpJsonED before, EndsMcpJsonED after, string path, EndsMcpJsonED fields, string language = null)
        {
            string status = "Changed";

            if (before == null) status = "Added";
            else if (after == null) status = "Removed";

            EndsMcpJsonED field = EndsMcpJsonED.Obj();
            field.Set("path", EndsMcpJsonED.Str(path));
            field.Set("status", EndsMcpJsonED.Str(status));
            field.Set("before", before ?? EndsMcpJsonED.Null());
            field.Set("after", after ?? EndsMcpJsonED.Null());
            field.Set("language", EndsMcpJsonED.Str(language));
            fields.Add(field);
        }

        #endregion PENDING REPORT PROJECTION

        #region COLLECTION DIFFERENCES

        private static EndsMcpJsonED ReadPendingOrder(EndsMcpJsonED settings, string scope)
        {
            if (scope == SHARED_SCOPE) return settings.Get(ORDER_FIELD) ?? EndsMcpJsonED.Arr();

            EndsMcpJsonED order = EndsMcpJsonED.Arr();

            foreach (EndsMcpJsonED node in settings.Get("nodes")?.Items ?? new List<EndsMcpJsonED>())
            {
                order.Add(node.Get("nodeId") ?? EndsMcpJsonED.Null());
            }

            return order;
        }

        private static EndsMcpJsonED PendingKeys(Dictionary<string, EndsMcpJsonED> entries)
        {
            EndsMcpJsonED keys = EndsMcpJsonED.Arr();

            foreach (string key in entries.Keys)
            {
                keys.Add(EndsMcpJsonED.Str(key));
            }

            return keys;
        }

        private static void AppendPendingOrder(EndsMcpJsonED before, EndsMcpJsonED after, string path, EndsMcpJsonED fields)
        {
            HashSet<string> oldKeys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> newKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsMcpJsonED key in before.Items)
            {
                oldKeys.Add(key.Text);
            }

            foreach (EndsMcpJsonED key in after.Items)
            {
                newKeys.Add(key.Text);
            }

            EndsMcpJsonED oldOrder = EndsMcpJsonED.Arr();
            EndsMcpJsonED newOrder = EndsMcpJsonED.Arr();

            foreach (EndsMcpJsonED key in before.Items)
            {
                if (newKeys.Contains(key.Text)) oldOrder.Add(key);
            }

            foreach (EndsMcpJsonED key in after.Items)
            {
                if (oldKeys.Contains(key.Text)) newOrder.Add(key);
            }

            if (oldOrder.Write() != newOrder.Write()) AddPendingField(oldOrder, newOrder, path, fields);
        }

        private static void AppendPendingArray(EndsMcpJsonED before, EndsMcpJsonED after, string path, EndsMcpJsonED fields, string language)
        {
            List<EndsMcpJsonED> previous = before?.Items ?? new List<EndsMcpJsonED>();
            List<EndsMcpJsonED> current = after?.Items ?? new List<EndsMcpJsonED>();
            List<string> oldKeys = new List<string>();
            List<string> newKeys = new List<string>();

            foreach (EndsMcpJsonED item in previous)
            {
                oldKeys.Add(item.Write());
            }

            foreach (EndsMcpJsonED item in current)
            {
                newKeys.Add(item.Write());
            }

            if (TryAppendPendingReorder(oldKeys, newKeys, path, fields)) return;

            int prefix = 0;

            while (prefix < Math.Min(previous.Count, current.Count) && oldKeys[prefix] == newKeys[prefix]) prefix++;

            int oldEnd = previous.Count;
            int newEnd = current.Count;

            while (oldEnd > prefix && newEnd > prefix && oldKeys[oldEnd - 1] == newKeys[newEnd - 1])
            {
                oldEnd--;
                newEnd--;
            }

            int oldCount = oldEnd - prefix;
            int newCount = newEnd - prefix;

            if (oldCount == 0 || newCount == 0 || (long)oldCount * newCount > PENDING_ARRAY_MATCH_LIMIT)
            {
                AppendPendingArrayRange(previous, current, prefix, oldEnd, prefix, newEnd, path, fields, language);

                return;
            }

            int[,] matches = new int[oldCount + 1, newCount + 1];

            for (int i = oldCount - 1; i >= 0; i--)
            {
                for (int j = newCount - 1; j >= 0; j--)
                {
                    if (oldKeys[prefix + i] == newKeys[prefix + j]) matches[i, j] = matches[i + 1, j + 1] + 1;
                    else matches[i, j] = Math.Max(matches[i + 1, j], matches[i, j + 1]);
                }
            }

            int oldStart = prefix;
            int newStart = prefix;
            int oldIndex = prefix;
            int newIndex = prefix;

            while (oldIndex < oldEnd && newIndex < newEnd)
            {
                if (oldKeys[oldIndex] == newKeys[newIndex])
                {
                    AppendPendingArrayRange(previous, current, oldStart, oldIndex, newStart, newIndex, path, fields, language);
                    oldStart = ++oldIndex;
                    newStart = ++newIndex;
                }
                else if (matches[oldIndex - prefix + 1, newIndex - prefix] >= matches[oldIndex - prefix, newIndex - prefix + 1]) oldIndex++;
                else newIndex++;
            }

            AppendPendingArrayRange(previous, current, oldStart, oldEnd, newStart, newEnd, path, fields, language);
        }

        private static bool TryAppendPendingReorder(List<string> oldKeys, List<string> newKeys, string path, EndsMcpJsonED fields)
        {
            if (oldKeys.Count != newKeys.Count || oldKeys.Count == 0) return false;

            Dictionary<string, Queue<int>> indices = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

            for (int i = 0; i < oldKeys.Count; i++)
            {
                if (!indices.TryGetValue(oldKeys[i], out Queue<int> positions)) indices.Add(oldKeys[i], positions = new Queue<int>());

                positions.Enqueue(i);
            }

            EndsMcpJsonED oldOrder = EndsMcpJsonED.Arr();
            EndsMcpJsonED newOrder = EndsMcpJsonED.Arr();

            for (int i = 0; i < newKeys.Count; i++)
            {
                if (!indices.TryGetValue(newKeys[i], out Queue<int> positions) || positions.Count == 0) return false;

                oldOrder.Add(EndsMcpJsonED.Str("#" + (i + 1)));
                newOrder.Add(EndsMcpJsonED.Str("#" + (positions.Dequeue() + 1)));
            }

            if (oldOrder.Write() != newOrder.Write()) AddPendingField(oldOrder, newOrder, path + "/$order", fields);

            return true;
        }

        private static void AppendPendingArrayRange(List<EndsMcpJsonED> before, List<EndsMcpJsonED> after, int oldStart, int oldEnd, int newStart, int newEnd, string path, EndsMcpJsonED fields, string language)
        {
            int count = Math.Max(oldEnd - oldStart, newEnd - newStart);

            for (int offset = 0; offset < count; offset++)
            {
                EndsMcpJsonED previous = null;
                EndsMcpJsonED current = null;
                int index = newStart + offset;

                if (oldStart + offset < oldEnd) previous = before[oldStart + offset];
                if (newStart + offset < newEnd) current = after[newStart + offset];
                else index = oldStart + offset;

                AppendPendingFields(previous, current, path + "/" + index.ToString(CultureInfo.InvariantCulture), fields, language);
            }
        }

        #endregion COLLECTION DIFFERENCES
    }
}
#endif
