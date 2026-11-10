#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed class EndsAiEditMemoryED : ScriptableObject
    {
        private const string FOLDER = "Assets/ALTUSHKA/Easy Node Dialog System/Editor/AiEditMemory";
        private const string PENDING_SESSION_PREFIX = "ENDS.PendingAiHighlights.";

        [SerializeField] private DialogDatabase _database;
        [SerializeField] private List<string> _keys = new List<string>();
        [NonSerialized] private Dictionary<string, string> _pendingScopes;

        internal DialogDatabase Database => _database;
        internal IReadOnlyList<string> Keys => _keys;
        internal IReadOnlyDictionary<string, string> PendingScopes
        {
            get
            {
                if (_pendingScopes != null) return _pendingScopes;

                _pendingScopes = new Dictionary<string, string>(StringComparer.Ordinal);
                EndsMcpJsonED stored = EndsMcpJsonED.Parse(SessionState.GetString(PendingSessionKey(), "{}"));

                foreach (string key in stored.Keys)
                {
                    _pendingScopes[key] = stored.Get(key).Text;
                }

                return _pendingScopes;
            }
        }
        internal static event Action<DialogDatabase> KeysRemapped;
        
        internal static EndsAiEditMemoryED Find(DialogDatabase database)
        {
            if (database == null) return null;
            if (!AssetDatabase.IsValidFolder(FOLDER)) return null;

            database = EndsSessionED.SourceAsset(database);
            string databasePath = AssetDatabase.GetAssetPath(database);
            string memoryPath = FOLDER + "/AiEditMemory_" + AssetDatabase.AssetPathToGUID(databasePath) + ".asset";
            EndsAiEditMemoryED direct = AssetDatabase.LoadAssetAtPath<EndsAiEditMemoryED>(memoryPath);

            if (direct != null)
            {
                if (direct._database != database)
                {
                    direct._database = database;
                    EditorUtility.SetDirty(direct);
                }

                return direct;
            }

            string[] guids = AssetDatabase.FindAssets("t:" + nameof(EndsAiEditMemoryED), new[] { FOLDER });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EndsAiEditMemoryED memory = AssetDatabase.LoadAssetAtPath<EndsAiEditMemoryED>(path);

                if (memory != null && EndsSessionED.SourceAsset(memory._database) == database) return memory;
            }

            return null;
        }

        internal static EndsAiEditMemoryED GetOrCreate(DialogDatabase database)
        {
            EndsAiEditMemoryED existing = Find(database);

            if (existing != null) return existing;
            if (database == null) return null;

            string databasePath = EndsSessionED.AssetPath(database);

            if (string.IsNullOrEmpty(databasePath)) return null;

            EnsureFolder();
            string guid = AssetDatabase.AssetPathToGUID(databasePath);
            string assetPath = FOLDER + "/AiEditMemory_" + guid + ".asset";
            EndsAiEditMemoryED memory = CreateInstance<EndsAiEditMemoryED>();
            memory._database = EndsSessionED.SourceAsset(database);
            AssetDatabase.CreateAsset(memory, assetPath);

            return memory;
        }

        internal bool Remember(string key)
        {
            if (string.IsNullOrEmpty(key) || _keys.Contains(key)) return false;

            _keys.Add(key);

            return true;
        }

        internal bool RememberPending(string key, string scope)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (PendingScopes.TryGetValue(key, out string previous) && previous == scope) return false;

            _pendingScopes[key] = scope;
            StorePendingMarks();

            return true;
        }

        internal bool Forget(string key)
        {
            bool changed = _keys.Remove(key);
            bool pending = PendingScopes.ContainsKey(key);

            if (pending)
            {
                _pendingScopes.Remove(key);
                StorePendingMarks();
            }

            return changed || pending;
        }

        internal bool ResolvePending(IReadOnlyCollection<string> scopes, bool saved)
        {
            HashSet<string> selected = new HashSet<string>(scopes, StringComparer.Ordinal);
            List<string> resolved = new List<string>();

            foreach (KeyValuePair<string, string> pending in PendingScopes)
            {
                if (!selected.Contains(pending.Value)) continue;

                resolved.Add(pending.Key);

                if (saved) Remember(pending.Key);
            }

            if (resolved.Count == 0) return false;

            foreach (string key in resolved)
            {
                _pendingScopes.Remove(key);
            }

            StorePendingMarks();

            return true;
        }

        internal void RemapPrefixes(IReadOnlyDictionary<string, string> replacements)
        {
            if (replacements.Count == 0) return;

            bool changed = false;
            List<string> saved = new List<string>();

            foreach (string key in _keys)
            {
                string replacement = RemapPrefix(key, replacements);

                if (replacement != key) changed = true;
                if (replacement != null && !saved.Contains(replacement)) saved.Add(replacement);
            }

            Dictionary<string, string> pending = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> key in PendingScopes)
            {
                string replacement = RemapPrefix(key.Key, replacements);

                if (replacement != key.Key) changed = true;
                if (replacement != null) pending[replacement] = key.Value;
            }

            if (!changed) return;

            _keys = saved;
            _pendingScopes = pending;
            StorePendingMarks();
            EditorUtility.SetDirty(this);
            KeysRemapped?.Invoke(_database);
        }

        private static string RemapPrefix(string key, IReadOnlyDictionary<string, string> replacements)
        {
            string matched = null;
            string target = null;

            foreach (KeyValuePair<string, string> replacement in replacements)
            {
                if (key != replacement.Key && !key.StartsWith(replacement.Key + "/", StringComparison.Ordinal)) continue;
                if (matched != null && matched.Length >= replacement.Key.Length) continue;

                matched = replacement.Key;
                target = replacement.Value;
            }

            if (matched == null) return key;
            if (target == null) return null;

            return target + key.Substring(matched.Length);
        }

        private string PendingSessionKey()
        {
            string path = EndsSessionED.AssetPath(_database);
            string identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this).ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(path)) identity = AssetDatabase.AssetPathToGUID(path);

            return PENDING_SESSION_PREFIX + identity;
        }

        private void StorePendingMarks()
        {
            EndsMcpJsonED stored = EndsMcpJsonED.Obj();

            foreach (KeyValuePair<string, string> pending in _pendingScopes)
            {
                stored.Set(pending.Key, EndsMcpJsonED.Str(pending.Value));
            }

            SessionState.SetString(PendingSessionKey(), stored.Write());
        }

        internal void RemapNodeIds(EndsNodeIdentityED.MigrationPlan plan)
        {
            bool changed = false;
            Dictionary<string, string> pending = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> mark in PendingScopes)
            {
                string replacement = EndsNodeIdentityED.RemapEditorKey(mark.Key, plan);
                pending[replacement] = mark.Value;

                if (replacement != mark.Key) changed = true;
            }

            _pendingScopes = pending;
            StorePendingMarks();

            for (int i = 0; i < _keys.Count; i++)
            {
                string replacement = EndsNodeIdentityED.RemapEditorKey(_keys[i], plan);

                if (replacement == _keys[i]) continue;

                _keys[i] = replacement;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
                KeysRemapped?.Invoke(_database);
            }
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(FOLDER)) return;

            string parent = Path.GetDirectoryName(FOLDER)?.Replace('\\', '/');
            string leaf = Path.GetFileName(FOLDER);

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif