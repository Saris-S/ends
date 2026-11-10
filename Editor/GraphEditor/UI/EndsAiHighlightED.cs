#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [InitializeOnLoad]
    internal static partial class EndsAiHighlightED
    {
        internal const string PREFS_KEY = "Ends.HighlightAiEdits";
        private const int SHADE_COUNT = 3;

        private static readonly HashSet<string> _keys = new HashSet<string>();
        private static readonly HashSet<string> _savedKeys = new HashSet<string>();
        private static DialogDatabase _boundDatabase;
        private static DialogDatabase _viewDatabase;
        private static EndsAiEditMemoryED _boundMemory;
        private static bool _boundSearched;
        private static bool _enabled = EditorPrefs.GetBool(PREFS_KEY, false);

        internal static readonly Color TINT = new Color(0.62f, 1f, 0.7f, 1f);
        private static readonly Color PENDING_TINT = new Color(1f, 0.85f, 0.38f, 1f);
        private static readonly Color PENDING_FRAME = new Color(0.85f, 0.62f, 0.12f, 1f);
        private static readonly Color PENDING_LINE = new Color(1f, 0.8f, 0.25f, 1f);
        internal static readonly Color FRAME = new Color(0.2f, 0.55f, 0.3f, 1f);
        internal static readonly Color FRAME_HOT = new Color(0.32f, 0.72f, 0.4f, 1f);
        internal static readonly Color LINE = new Color(0.48f, 0.8f, 0.54f, 1f);

        internal enum Shade
        {
            Rest,
            Bright,
            Clear
        }

        private static Texture2D _clear;
        private static readonly GUIStyle[] _nodeStyles = new GUIStyle[SHADE_COUNT];
        private static readonly GUIStyle[] _plateStyles = new GUIStyle[SHADE_COUNT];
        private static GUISkin _styleSkin;
        private static GUIStyle _windowSource;
        private static bool _styleProSkin;

        internal static GUIStyle PaintNode(string key, Shade shade)
        {
            GUIStyle style = GetChromeStyle(shade, true);
            GUI.backgroundColor = Color.white;

            if (shade != Shade.Clear && IsMarked(key)) GUI.backgroundColor = Tint(key);

            return style;
        }

        internal static void PaintNode(GUIStyle style, bool green, Shade shade)
        {
            if (style == null || GUI.skin == null || GUI.skin.window == null) return;

            if (shade == Shade.Rest)
            {
                Texture2D normal = GUI.skin.window.normal.background;
                Assign(style, normal, normal, normal, normal);
            }
            else Bake(style, shade);

            GUI.backgroundColor = Tone(green, shade);
        }

        internal static void PaintPlate(Rect rect, string key, Shade shade)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (GUI.skin == null || GUI.skin.window == null) return;

            GUIStyle plate = GetChromeStyle(shade, false);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            if (shade != Shade.Clear && IsMarked(key)) GUI.backgroundColor = Tint(key);

            bool bright = shade == Shade.Bright;
            plate.Draw(rect, GUIContent.none, false, false, bright, false);
            GUI.backgroundColor = previous;
        }

        internal static Color BeginField(string key)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            if (IsMarked(key)) GUI.backgroundColor = Tint(key);

            return previous;
        }

        internal static void EndField(string key, Rect rect, Color previous)
        {
            GUI.backgroundColor = previous;
            ReleaseIfAltHovered(key, rect);
        }

        private static GUIStyle GetChromeStyle(Shade shade, bool node)
        {
            if (_styleSkin != GUI.skin || _windowSource != GUI.skin.window || _styleProSkin != EditorGUIUtility.isProSkin)
            {
                Array.Clear(_nodeStyles, 0, _nodeStyles.Length);
                Array.Clear(_plateStyles, 0, _plateStyles.Length);
                _styleSkin = GUI.skin;
                _windowSource = GUI.skin.window;
                _styleProSkin = EditorGUIUtility.isProSkin;
            }

            GUIStyle[] styles = _plateStyles;

            if (node) styles = _nodeStyles;

            int index = (int)shade;

            if (styles[index] != null) return styles[index];

            GUIStyle style = new GUIStyle(_windowSource);

            if (node) PaintNode(style, false, shade);
            else if (shade != Shade.Rest) Bake(style, shade);

            styles[index] = style;

            return style;
        }

        private static Color Tone(bool green, Shade shade)
        {
            if (shade == Shade.Clear || !green) return Color.white;

            return TINT;
        }

        private static void Bake(GUIStyle style, Shade shade)
        {
            if (shade == Shade.Clear)
            {
                Texture2D clear = ClearTexture();
                Assign(style, clear, clear, clear, clear);
                return;
            }

            GUIStyle skin = GUI.skin.window;
            bool bright = shade == Shade.Bright;
            Texture2D normal = bright ? Prefer(skin.onNormal.background, skin.normal.background) : skin.normal.background;
            Texture2D hover = bright ? Prefer(skin.onHover.background, skin.hover.background) : skin.hover.background;
            Texture2D active = bright ? Prefer(skin.onActive.background, skin.active.background) : skin.active.background;
            Texture2D focused = bright ? Prefer(skin.onFocused.background, skin.focused.background) : skin.focused.background;

            if (hover == null) hover = normal;
            if (active == null) active = normal;
            if (focused == null) focused = normal;

            Assign(style, normal, hover, active, focused);
        }

        private static Texture2D Prefer(Texture2D on, Texture2D off)
        {
            if (on != null) return on;

            return off;
        }

        private static void Assign(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Texture2D focused)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = focused;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.onFocused.background = focused;
        }

        private static Texture2D ClearTexture()
        {
            if (_clear != null) return _clear;

            _clear = new Texture2D(2, 2);
            _clear.hideFlags = HideFlags.HideAndDontSave;
            Color color = new Color(0.4f, 0.6f, 0.9f, 0.25f);
            _clear.SetPixel(0, 0, color);
            _clear.SetPixel(1, 0, color);
            _clear.SetPixel(0, 1, color);
            _clear.SetPixel(1, 1, color);
            _clear.Apply();
            return _clear;
        }

        internal static void Frame(Rect rect, string key)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            bool hot = EditorGUIUtility.editingTextField && rect.Contains(Event.current.mousePosition);
            Color color = hot ? FRAME_HOT : FRAME;

            if (IsPending(key)) color = PENDING_FRAME;

            const float thickness = 2f;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        internal static bool Enabled => _enabled;

        internal static void SetEnabled(bool value)
        {
            EditorPrefs.SetBool(PREFS_KEY, value);
            _enabled = value;
        }

        static EndsAiHighlightED()
        {
            EndsSessionED.ScopeContentApplied += NoteScopeContent;
            EndsAiEditMemoryED.KeysRemapped += database =>
            {
                if (_boundDatabase == EndsSessionED.SourceAsset(database)) _boundSearched = false;
            };
            EndsSessionED.ScopesResolved += (database, scopes, saved) =>
            {
                EndsAiEditMemoryED memory = EndsAiEditMemoryED.Find(database);

                if (memory == null || !memory.ResolvePending(scopes, saved)) return;

                EditorUtility.SetDirty(memory);

                if (saved) AssetDatabase.SaveAssetIfDirty(memory);
                if (_boundDatabase == EndsSessionED.SourceAsset(database)) _boundSearched = false;
            };
        }

        /// <summary> ENDS. Binds highlight lookups to the window being drawn, nested views restore their previous database without searching all editor windows per control. </summary>
        internal static ViewScope OpenView(DialogDatabase database) => new ViewScope(database);

        /// <summary> ENDS. Retains the current drawing database for one view, disposal restores an enclosing view without changing highlight ownership or saved state. </summary>
        internal readonly struct ViewScope : IDisposable
        {
            private readonly DialogDatabase _previous;

            /// <summary> ENDS. Selects one view's database and loads its existing highlight memory. </summary>
            internal ViewScope(DialogDatabase database)
            {
                _previous = _viewDatabase;
                _viewDatabase = database;
                Bind(database);
            }

            /// <summary> ENDS. Restores the enclosing view, later top-level windows bind their own database when entering. </summary>
            public void Dispose()
            {
                _viewDatabase = _previous;

                if (_previous != null) Bind(_previous);
            }
        }

        internal static void Bind(DialogDatabase database)
        {
            database = EndsSessionED.SourceAsset(database);

            if (_boundMemory == null) _keys.Clear();
            if (_boundDatabase == database && _boundSearched) return;

            _boundDatabase = EndsSessionED.SourceAsset(database);
            _boundSearched = true;
            _keys.Clear();
            _savedKeys.Clear();
            _boundMemory = EndsAiEditMemoryED.Find(database);

            if (_boundMemory == null || _boundMemory.Keys == null) return;

            foreach (string key in _boundMemory.Keys)
            {
                _keys.Add(key);
                _savedKeys.Add(key);
            }

            foreach (string key in _boundMemory.PendingScopes.Keys)
            {
                _keys.Add(key);
            }
        }

        internal static void Mark(string key)
        {
            Mark(_boundDatabase ?? DialogEditorWindowED.ActiveDatabase(), key);
        }

        internal static void NoteResult(DialogDatabase database, EndsSessionED.Result result)
        {
            Bind(database);

            foreach (string change in result.Changes)
            {
                if (change.StartsWith("node/", StringComparison.Ordinal) || change.StartsWith("dialog/", StringComparison.Ordinal)) continue;

                Mark(database, change);

            }

            DialogDatabase working = EndsSessionED.ForDatabase(database).WorkingDatabase;

            foreach (DialogEntry dialog in working.Dialogs)
            {
                if (dialog != null && result.ScopeKeys.Contains(dialog.NodeIdentityKey)) Mark(database, EndsNodeDrawersED.Root<DialogEntry>(dialog.dialogName).Key);
            }

        }

        internal static void Mark(DialogDatabase database, string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            Bind(database);
            EndsAiEditMemoryED memory = EndsAiEditMemoryED.GetOrCreate(database);

            if (memory == null) return;

            _boundDatabase = EndsSessionED.SourceAsset(database);
            _boundMemory = memory;
            _boundSearched = true;
            _keys.Add(key);
            EndsSessionED session = EndsSessionED.ForDatabase(database);
            string scope = ScopeForKey(session, key);

            if (session.IsScopeDirty(scope)) memory.RememberPending(key, scope);
            else
            {
                memory.Remember(key);
                _savedKeys.Add(key);
            }

            EditorUtility.SetDirty(memory);
        }

        internal static bool IsMarked(string key)
        {
            if (!Enabled) return false;

            return IsMarked(_viewDatabase ?? DialogEditorWindowED.ActiveDatabase(), key);
        }

        internal static bool IsMarked(DialogDatabase database, string key)
        {
            if (!Enabled || string.IsNullOrEmpty(key)) return false;

            Bind(database);

            if (!_keys.Contains(key)) return false;
            if (_savedKeys.Contains(key)) return true;

            return IsPending(database, key);
        }

        internal static bool IsPending(string key)
        {
            if (!Enabled) return false;

            return IsPending(_viewDatabase ?? DialogEditorWindowED.ActiveDatabase(), key);
        }

        internal static bool IsPending(DialogDatabase database, string key)
        {
            if (!Enabled || string.IsNullOrEmpty(key)) return false;

            Bind(database);

            if (_boundMemory == null) return false;
            if (!_boundMemory.PendingScopes.TryGetValue(key, out string scope)) return false;

            return EndsSessionED.ForDatabase(_boundDatabase).IsScopeDirty(scope);
        }

        internal static Color Tint(string key)
        {
            if (IsPending(key)) return PENDING_TINT;

            return TINT;
        }

        internal static Color LineTint(string key)
        {
            if (IsPending(key)) return PENDING_LINE;

            return LINE;
        }

        private static string ScopeForKey(EndsSessionED session, string key)
        {
            int prefixEnd = key.IndexOf('/');

            if (prefixEnd < 0) return EndsSessionED.SHARED_SCOPE;

            string prefix = key.Substring(0, prefixEnd);

            if (prefix != "schema") return EndsSessionED.SHARED_SCOPE;

            string address = key.Substring(prefixEnd + 1);
            string scope = EndsSessionED.SHARED_SCOPE;
            int matchedLength = -1;

            foreach (DialogEntry dialog in session.WorkingDatabase.Dialogs)
            {
                if (dialog == null || dialog.dialogName == null || dialog.dialogName.Length <= matchedLength) continue;
                if (address != dialog.dialogName && !address.StartsWith(dialog.dialogName + "/", StringComparison.Ordinal)) continue;

                scope = dialog.NodeIdentityKey;
                matchedLength = dialog.dialogName.Length;
            }

            if (matchedLength >= 0) return scope;

            foreach (DialogEntry dialog in session.ReadSavedDialogs())
            {
                if (dialog == null || dialog.dialogName == null || dialog.dialogName.Length <= matchedLength) continue;
                if (address != dialog.dialogName && !address.StartsWith(dialog.dialogName + "/", StringComparison.Ordinal)) continue;

                scope = dialog.NodeIdentityKey;
                matchedLength = dialog.dialogName.Length;
            }

            return scope;
        }

        internal static void SaveBoundMemory()
        {
            if (_boundMemory == null) return;

            EditorUtility.SetDirty(_boundMemory);
            AssetDatabase.SaveAssetIfDirty(_boundMemory);
        }

        internal static void Forget(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            _keys.Remove(key);
            _savedKeys.Remove(key);

            if (_boundMemory == null || !_boundMemory.Forget(key)) return;

            EditorUtility.SetDirty(_boundMemory);
        }

        internal static void ReleaseIfAltHovered(string key, Rect rect)
        {
            Event current = Event.current;

            if (!Enabled || current == null || !current.alt || !rect.Contains(current.mousePosition)) return;

            bool move = current.type == EventType.MouseMove || current.rawType == EventType.MouseMove;
            bool keyDown = current.type == EventType.KeyDown || current.rawType == EventType.KeyDown;
            bool paint = current.type == EventType.Repaint;

            if (!move && !keyDown && !paint) return;

            AcknowledgeSaved(_viewDatabase ?? DialogEditorWindowED.ActiveDatabase(), key);
        }

        internal static bool AcknowledgeSaved(DialogDatabase database, string key)
        {
            if (!IsMarked(database, key) || IsPending(database, key)) return false;

            Forget(key);

            return true;
        }

        internal static void Note(string tool, EndsMcpJsonED args)
        {
            if (args == null || string.IsNullOrEmpty(tool)) return;
            if (_boundDatabase == null) return;

            if (tool.StartsWith("ends_write_dialog_"))
            {
                string name = Arg(args, "name");

                if (tool == "ends_write_dialog_rename" || tool == "ends_write_dialog_duplicate")
                {
                    string newer = Arg(args, "newName");

                    if (!string.IsNullOrEmpty(newer)) name = newer;
                }

                Mark(EndsNodeDrawersED.Root<DialogEntry>(name).Key);

                return;
            }

            // Node fields are owned by the committed-content tracker.
            if (tool == "ends_write_graph" || tool == "ends_write_node_duplicate" || tool == "ends_write_text" || tool.StartsWith("ends_write_line_") || tool.StartsWith("ends_write_choice_")) return;

            if (tool.StartsWith("ends_write_variable_"))
            {
                string name = Arg(args, "name");

                if (tool == "ends_write_variable_rename")
                {
                    string newer = Arg(args, "newName");

                    if (!string.IsNullOrEmpty(newer)) name = newer;
                }

                Mark("variable/" + name);

                return;
            }

            if (tool.StartsWith("ends_write_character_") || tool.StartsWith("ends_write_sprite_"))
            {
                MarkCharacter(args);

                return;
            }

            if (tool.StartsWith("ends_write_name_"))
            {
                string name = Arg(args, "name");

                if (tool == "ends_write_name_rename")
                {
                    string newer = Arg(args, "newName");

                    if (!string.IsNullOrEmpty(newer)) name = newer;
                }

                Mark("name/" + name);

                return;
            }

            if (tool.StartsWith("ends_write_category_"))
            {
                string name = Arg(args, "name");

                if (tool == "ends_write_category_rename")
                {
                    string newer = Arg(args, "newName");

                    if (!string.IsNullOrEmpty(newer)) name = newer;
                }

                Mark("category/" + name);

                return;
            }

            if (tool.StartsWith("ends_write_language_"))
            {
                string name = Arg(args, "language");

                if (tool == "ends_write_language_rename")
                {
                    string newer = Arg(args, "newName");

                    if (!string.IsNullOrEmpty(newer)) name = newer;
                }

                Mark("language/" + name);
            }
        }

        private static void MarkCharacter(EndsMcpJsonED args)
        {
            string name = Arg(args, "name");

            Mark("character/" + name);

            string slot = Arg(args, "slot");
            string newer = Arg(args, "newName");

            if (!string.IsNullOrEmpty(newer)) Mark("sprite/" + name + "/" + newer);
            else if (!string.IsNullOrEmpty(slot)) Mark("sprite/" + name + "/" + slot);

            EndsMcpJsonED sprites = args.Get("sprites");

            if (sprites == null || sprites.Items == null) return;

            for (int i = 0; i < sprites.Items.Count; i++)
            {
                EndsMcpJsonED item = sprites.Items[i];

                if (item == null) continue;

                string spriteName = Arg(item, "slot");

                if (string.IsNullOrEmpty(spriteName)) spriteName = Arg(item, "spriteName");
                if (string.IsNullOrEmpty(spriteName)) continue;

                Mark("sprite/" + name + "/" + spriteName);
            }
        }

        private static string Arg(EndsMcpJsonED args, string key)
        {
            if (args == null) return "";

            EndsMcpJsonED value = args.Get(key);

            if (value == null || value.Type != EndsMcpJsonED.Kind.String) return "";

            return value.Text ?? "";
        }
    }
}
#endif