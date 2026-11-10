#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsDialogAiWriteED
    {


        #region TOOLS

        internal static void AddTools(EndsMcpJsonED tools)
        {
            EndsDialogContractAdapterED.AddTools(tools);
            tools.Add(Tool("ends_session_status", "ENDS. Reads working and saved revisions and dirty state. database selects an asset explicitly.", SessionSchema()));
            tools.Add(Tool("ends_session_save", "ENDS. Saves selected dialogs and optionally shared data. Omit both selectors for the entire database. Unsaved dependencies and foreign ownership reject saving, working edits remain pending.", SessionSchema()));
            tools.Add(Tool("ends_session_discard", "ENDS. Restores selected dialogs and optionally shared data. Omit both selectors for the entire database. Requires ownership, retains allocated id numbers and pending legacy id migration. Can be undone.", SessionSchema()));
            EndsMcpJsonED leaseSchema = SessionSchema();
            leaseSchema.Set("action", Field("string", "acquire, renew or release. Claim dialogs or shared=true before writing. Leases expire after 120 seconds without ENDS activity."));
            leaseSchema.Set("task", Field("string", "Short human-readable editing task shown in the editor."));
            EndsMcpJsonED leaseTool = Tool("ends_edit_lease", "ENDS. Acquires, renews or releases exclusive editing ownership for this MCP session. Different dialogs can have different owners. Acquire shared=true for database-wide changes. Reread revision after ownership changes.", leaseSchema);
            EndsMcpJsonED requiredLease = EndsMcpJsonED.Arr();
            requiredLease.Add(EndsMcpJsonED.Str("action"));
            leaseTool.Get("inputSchema").Set("required", requiredLease);
            tools.Add(leaseTool);

            #region DATABASE

            tools.Add(Tool("ends_write_database_set", "ENDS. Switches the Dialog Database slot in the open ENDS window. Does not create a database. If a dialog graph is open, tell the user to use Back to list first.", DatabaseSwitchSchema()));

            #endregion DATABASE

            #region GRAPH

            tools.Add(Tool("ends_write_graph", "ENDS. Adds nodes and sets edges in one dialog. Omit coordinates and submit edges in the same batch for left-to-right placement. Review placement for final positions, expanded sizes and conflicts.", GraphSchema()));
            tools.Add(Tool("ends_write_node_duplicate", "ENDS. Duplicates one node. The copy gets a new id and no outgoing links.", NodeDuplicateSchema()));

            #endregion GRAPH


            #region DIALOG

            tools.Add(Tool("ends_write_dialog_create", "ENDS. Creates a dialog and returns startNodeId. Same path as Add Dialog.", DialogCreateSchema()));
            tools.Add(Tool("ends_write_dialog_rename", "ENDS. Renames a dialog and updates redirect targets.", DialogRenameSchema()));
            tools.Add(Tool("ends_write_dialog_delete", "ENDS. Deletes a dialog and marks redirect references missing.", DialogNameSchema()));
            tools.Add(Tool("ends_write_dialog_duplicate", "ENDS. Copies a dialog with its local node ids and links. An omitted newName receives a free (N) suffix.", DialogRenameSchema()));
            tools.Add(Tool("ends_write_dialog_set", "ENDS. Sets category, locked, and replaces start conditions.", DialogSetSchema()));

            #endregion DIALOG


            #region CHARACTER

            tools.Add(Tool("ends_write_character_create", "ENDS. Creates a character. copyFrom copies sprite slot names.", CharacterCreateSchema()));
            tools.Add(Tool("ends_write_character_rename", "ENDS. Renames a character and updates sprite actions through the editor rename.", CharacterRenameSchema()));
            tools.Add(Tool("ends_write_character_delete", "ENDS. Deletes a character and marks sprite actions missing.", DialogNameSchema()));
            tools.Add(Tool("ends_write_sprite_add", "ENDS. Adds or fills sprite slots. file is a Sprite asset path.", SpriteAddSchema()));
            tools.Add(Tool("ends_write_sprite_rename", "ENDS. Renames a sprite slot and updates emotion names on actions.", SpriteRenameSchema()));
            tools.Add(Tool("ends_write_sprite_remove", "ENDS. Removes a sprite slot and marks those actions missing.", SpriteRenameSchema()));

            #endregion CHARACTER


            #region VARIABLE

            tools.Add(Tool("ends_write_variable_add", "ENDS. Creates a variable. type is bool, int, float, or string.", VariableAddSchema()));
            tools.Add(Tool("ends_write_variable_rename", "ENDS. Renames a variable and updates dialog references. Name keeps letters, digits, and underscore.", VariableRenameSchema()));
            tools.Add(Tool("ends_write_variable_set", "ENDS. Changes the default value of an existing variable.", VariableAddSchema()));
            tools.Add(Tool("ends_write_variable_delete", "ENDS. Deletes a variable and marks references missing.", DialogNameSchema()));
            tools.Add(Tool("ends_write_variable_load", "ENDS. Loads current variable values from save.json. Same button as in the Variables tab.", EndsMcpJsonED.Obj()));
            tools.Add(Tool("ends_write_variable_save", "ENDS. Writes current variable values to save.json. Same button as in the Variables tab.", EndsMcpJsonED.Obj()));
            tools.Add(Tool("ends_write_variable_open", "ENDS. Opens save.json. Fails with the editor message if the file is missing.", EndsMcpJsonED.Obj()));
            tools.Add(Tool("ends_write_variable_current", "ENDS. Sets the current runtime value of a variable. Does not change the database default.", VariableCurrentSchema()));

            #endregion VARIABLE


            #region LANGUAGE

            tools.Add(Tool("ends_write_language_add", "ENDS. Adds a language code.", LanguageSchema()));
            tools.Add(Tool("ends_write_language_remove", "ENDS. Removes a language and its line, choice, and name texts. One language must remain.", LanguageSchema()));
            tools.Add(Tool("ends_write_language_rename", "ENDS. Renames a language code on the list and on existing texts.", LanguageRenameSchema()));
            tools.Add(Tool("ends_write_language_delete_texts", "ENDS. Deletes texts that use a code which is not in the language list. The list stays.", LanguageSchema()));
            tools.Add(Tool("ends_write_language_set", "ENDS. Sets the current language.", LanguageSchema()));

            #endregion LANGUAGE


            #region CATEGORY

            tools.Add(Tool("ends_write_category_add", "ENDS. Adds an empty category.", CategoryNameSchema()));
            tools.Add(Tool("ends_write_category_rename", "ENDS. Renames a category, its dialogs, and redirect targets.", CategoryRenameSchema()));
            tools.Add(Tool("ends_write_category_delete", "ENDS. Removes a category and moves its dialogs to Unnamed.", CategoryNameSchema()));

            #endregion CATEGORY


            #region NAME

            tools.Add(Tool("ends_write_name_set", "ENDS. Sets a display name, color, and translations.", NameSchema()));
            tools.Add(Tool("ends_write_name_rename", "ENDS. Renames a name-color key and line speaker fields that use it.", NameRenameSchema()));
            tools.Add(Tool("ends_write_name_delete", "ENDS. Deletes a name color and marks line character references missing.", DialogNameSchema()));

            #endregion NAME


            #region LINE

            tools.Add(Tool("ends_write_line_action_add", "ENDS. Adds a show or hide action on a line.", LineActionSchema()));
            tools.Add(Tool("ends_write_line_action_set", "ENDS. Changes one action on a line by index.", LineActionSchema()));
            tools.Add(Tool("ends_write_line_action_remove", "ENDS. Removes one action on a line by index.", LineActionRemoveSchema()));
            tools.Add(Tool("ends_write_line_action_copy", "ENDS. Copies character, emotion, show or hide, position, and zoom from one action onto another.", LineActionCopySchema()));

            #endregion LINE


            #region CHOICE

            tools.Add(Tool("ends_write_choice_remove", "ENDS. Removes one choice option by index. Later options shift down.", ChoiceIndexSchema()));
            tools.Add(Tool("ends_write_choice_condition_set", "ENDS. Replaces conditions on one choice option.", ChoiceConditionSchema()));
            tools.Add(Tool("ends_write_choice_consequence_set", "ENDS. Replaces consequences on one choice option.", ChoiceConsequenceSchema()));

            #endregion CHOICE
        }

        internal static bool TryDispatch(string tool, EndsMcpJsonED args, out string json, out string error)
        {
            if (EndsDialogContractAdapterED.Handles(tool)) return EndsDialogContractAdapterED.TryDispatch(tool, args, out json, out error);
            if (tool == "ends_write_database_set") return TryDatabaseSet(args, out json, out error);
            if (tool == "ends_session_status" || tool == "ends_session_save" || tool == "ends_session_discard" || tool == "ends_edit_lease") return TrySessionOperation(args, tool, out json, out error);
            if (tool == "ends_write_variable_load") return TryVariableFile(args, "load", out json, out error);
            if (tool == "ends_write_variable_save") return TryVariableFile(args, "save", out json, out error);
            if (tool == "ends_write_variable_open") return TryVariableFile(args, "open", out json, out error);
            if (tool == "ends_write_variable_current") return TryVariableFile(args, "current", out json, out error);

            json = null;
            error = "Unknown tool " + tool + ".";

            return false;
        }

        internal static DialogDatabase RequestDatabase(EndsMcpJsonED args, out string error)
        {
            error = null;
            EndsMcpJsonED requested = args?.Get("database");

            if (requested == null)
            {
                DialogDatabase current = Database(out error);

                if (current != null)
                {
                    EndsSessionED session = EndsSessionED.ForDatabase(current);
                    session.RenewActorLeases();
                    current = session.WorkingDatabase;
                }

                return current;
            }
            if (requested.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(requested.Text))
            {
                error = "database must be an existing Dialog Database asset path.";

                return null;
            }

            DialogDatabase database = AssetDatabase.LoadAssetAtPath<DialogDatabase>(requested.Text);

            if (database == null) error = "Dialog Database asset not found: " + requested.Text + ".";
            else
            {
                EndsSessionED session = EndsSessionED.ForDatabase(database);
                session.RenewActorLeases();
                database = session.WorkingDatabase;
            }

            return database;
        }

        private static int? ExpectedRevision(EndsMcpJsonED args)
        {
            EndsMcpJsonED value = args?.Get("expectedRevision");

            if (value == null) return null;
            if (value.Type != EndsMcpJsonED.Kind.Number || !int.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int revision) || revision < 0) throw new InvalidOperationException("expectedRevision must be a non-negative integer.");

            return revision;
        }

        private static void AddSessionSchema(EndsMcpJsonED properties)
        {
            properties.Set("database", Field("string", "Existing Dialog Database asset path. Omit for the current database."));
            properties.Set("expectedRevision", Field("integer", "Working database revision from the latest read, required for database writes, Save and Discard. A mismatch returns a conflict without changes."));
            properties.Set("save", Field("boolean", "Save only areas changed by this operation. Unrelated pending edits remain unsaved."));
        }

        private static EndsMcpJsonED SessionSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            AddSessionSchema(properties);
            properties.Remove("save");
            properties.Set("dialogs", StringArrayField("Dialog names to save, discard or claim, including deleted dialogs present in the saved baseline. Omit with shared for a whole-database save or discard."));
            properties.Set("shared", Field("boolean", "Include shared database settings, catalogs and dialog ordering."));
            properties.Set("includeChanges", Field("boolean", "Include the recent applied-change report in session status."));

            return properties;
        }

        private static bool TrySessionOperation(EndsMcpJsonED args, string operation, out string json, out string error)
        {
            json = null;
            DialogDatabase database = RequestDatabase(args, out error);

            if (database == null) return false;

            try
            {
                EndsSessionED session = EndsSessionED.ForDatabase(database);

                if (operation == "ends_session_status")
                {
                    EndsMcpJsonED state = session.ReadState();

                    if (args.Get("includeChanges")?.Bool == true) state.Set("collaboration", session.ReadCollaboration(true));

                    json = state.Write();

                    return true;
                }

                int? expectedRevision = ExpectedRevision(args);
                List<string> dialogs = null;

                if (args.Get("dialogs") != null)
                {
                    dialogs = new List<string>();

                    foreach (EndsMcpJsonED dialog in args.Get("dialogs").Items)
                    {
                        dialogs.Add(dialog.Text);
                    }
                }

                bool shared = args.Get("shared")?.Bool == true;

                if (operation == "ends_edit_lease")
                {
                    if (expectedRevision.HasValue && expectedRevision.Value != session.Revision)
                    {
                        error = "Database revision changed, reread before claiming ownership.";

                        return false;
                    }

                    json = session.ChangeLease(Text(args, "action"), dialogs, shared, Text(args, "task")).Write();

                    return true;
                }

                EndsSessionED.Result result;

                if (operation == "ends_session_save")
                {
                    if (dialogs != null || args.Get("shared") != null) result = session.SaveSelection(dialogs, shared, expectedRevision);
                    else result = session.Save(expectedRevision);
                }
                else
                {
                    if (dialogs != null || args.Get("shared") != null) result = session.DiscardSelection(dialogs, shared, expectedRevision);
                    else result = session.Discard(expectedRevision);
                }

                json = EndsSessionED.WriteResult(result).Write();
                error = result.Error;

                return result.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;

                return false;
            }
        }

        internal static bool TryWriteGraph(EndsMcpJsonED args, out string json, out string error)
        {
            return EndsDialogContractAdapterED.TryDispatch("ends_write_graph", args, out json, out error);
        }

        internal static bool TryName(EndsMcpJsonED args, out string json, out string error)
        {
            return EndsDialogContractAdapterED.TryDispatch("ends_write_name_set", args, out json, out error);
        }

        #endregion TOOLS

        #region GRAPH





















        #endregion GRAPH

        #region DIALOG





        #endregion DIALOG

        #region CHARACTER





        #endregion CHARACTER

        #region VARIABLE AND LANGUAGE







        #endregion VARIABLE AND LANGUAGE

        #region CONDITIONS







        #endregion CONDITIONS

        #region LINE AND CHOICE











        private static bool TryVariableFile(EndsMcpJsonED args, string op, out string json, out string error)
        {
            json = null;
            DialogDatabase database = Database(out error);

            if (database == null) return false;

            string path = G.GetSavePath();
            EndsMcpJsonED root = EndsMcpJsonED.Obj();
            root.Set("path", EndsMcpJsonED.Str(path));

            if (op == "save" || op == "load")
            {
                try
                {
                    G.FileResult result = op == "save" ? EndsVariableRuntimeED.SaveCurrent(database) : EndsVariableRuntimeED.LoadCurrent(database);
                    root.Set("missing", EndsMcpJsonED.Boolean(result.Missing));

                    if (!result.Success)
                    {
                        error = result.Error;

                        return false;
                    }

                    if (result.Missing) root.Set("note", EndsMcpJsonED.Str("File not found. Current values were kept."));
                }
                catch (InvalidOperationException exception)
                {
                    error = exception.Message;

                    return false;
                }

                json = root.Write();
                return true;
            }

            if (op == "open")
            {
                try
                {
                    EndsVariableRuntimeED.OpenSaveFile();
                }
                catch (InvalidOperationException ex)
                {
                    error = ex.Message;
                    return false;
                }

                json = root.Write();
                return true;
            }

            int index = VariableIndex(database, Text(args, "name"));

            if (index < 0)
            {
                error = "Variable not found.";
                return false;
            }

            VariableDefinition def = database.Variables[index];

            if (def == null)
            {
                error = "Variable not found.";
                return false;
            }

            try
            {
                SetCurrentVariable(database, def, args.Get("value"));
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
                return false;
            }

            root.Set("variable", EndsMcpJsonED.Str(def.variableName ?? ""));
            json = root.Write();
            return true;
        }

        private static void SetCurrentVariable(DialogDatabase database, VariableDefinition def, EndsMcpJsonED value)
        {
            if (value == null) throw new InvalidOperationException("value is missing.");

            if (def.variableType == ConditionVariableType.Bool)
            {
                if (value.Type != EndsMcpJsonED.Kind.Bool) throw new InvalidOperationException("Bool value must be true or false.");

                EndsVariableRuntimeED.SetCurrent(database, def.variableName, value.Bool);
                return;
            }

            if (def.variableType == ConditionVariableType.String)
            {
                if (value.Type != EndsMcpJsonED.Kind.String) throw new InvalidOperationException("String value must be a string.");

                EndsVariableRuntimeED.SetCurrent(database, def.variableName, value.Text ?? "");
                return;
            }

            if (value.Type != EndsMcpJsonED.Kind.Number) throw new InvalidOperationException("Numeric value must be a number.");

            if (def.variableType == ConditionVariableType.Int)
            {
                int number;

                if (!int.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) throw new InvalidOperationException("Int value is not an integer.");

                EndsVariableRuntimeED.SetCurrent(database, def.variableName, number);
                return;
            }

            float numberFloat;

            if (!float.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out numberFloat)) throw new InvalidOperationException("Float value is not a number.");

            EndsVariableRuntimeED.SetCurrent(database, def.variableName, numberFloat);
        }

























        #endregion LINE AND CHOICE

        #region LOOKUP

        private static bool TryDatabaseSet(EndsMcpJsonED args, out string json, out string error)
        {
            json = null;
            error = null;
            string path = (Text(args, "path") ?? "").Trim().Replace('\\', '/');

            if (string.IsNullOrEmpty(path))
            {
                error = "path is empty.";

                return false;
            }

            DialogDatabase asset = AssetDatabase.LoadAssetAtPath<DialogDatabase>(path);

            if (asset == null)
            {
                error = "Dialog Database not found at " + path + ".";

                return false;
            }

            DialogEditorWindowED window = DialogEditorWindowED.WindowForDatabaseSlot();

            if (window == null)
            {
                error = "Open the ENDS window before switching Dialog Database.";

                return false;
            }

            try
            {
                if (!window.TrySwitchDialogDatabase(asset, out error)) return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;

                return false;
            }

            EndsMcpJsonED root = EndsMcpJsonED.Obj();
            root.Set("asset", EndsMcpJsonED.Str(AssetDatabase.GetAssetPath(asset)));
            json = root.Write();

            return true;
        }

        private static DialogDatabase Database(out string error)
        {
            error = null;
            DialogDatabase database = DialogEditorWindowED.ActiveDatabase();

            if (database != null) return database;

            error = "Assign a Dialog Database in the ENDS window.";

            return null;
        }













        private static int VariableIndex(DialogDatabase database, string name)
        {
            return EndsVariableCommandsED.FindIndex(database, name);
        }

        private static string Text(EndsMcpJsonED args, string key)
        {
            if (args == null) return "";

            EndsMcpJsonED node = args.Get(key);

            if (node == null || node.Type != EndsMcpJsonED.Kind.String || node.Text == null) return "";

            return node.Text;
        }









        private static EndsMcpJsonED DatabaseSwitchSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("path", Field("string", "Project path of an existing Dialog Database. ends_read_catalog returns the current asset."));
            return properties;
        }

        private static EndsMcpJsonED GraphSchema()
        {
            EndsMcpJsonED node = EndsMcpJsonED.Obj();
            node.Set("id", Field("string", "Full node id such as LNN.5. Omit on addNodes to allocate a new id. Required on setNodes."));
            node.Set("type", Field("string", "addNodes only. start, line, choice, condition, or redirect."));
            node.Set("speaker", Field("string", "Line character name."));
            node.Set("instant", Field("boolean", "Line advances without a click."));
            node.Set("x", Field("number", "Proposed graph x. Flow advances right, keep 180 units between expanded node edges."));
            node.Set("y", Field("number", "Proposed graph y. Stack branches with at least 120 units between expanded node edges."));
            EndsDialogConditionContractED.AddPatchSchema(node);
            EndsMcpJsonED edge = EndsMcpJsonED.Obj();
            edge.Set("from", Field("string", "Exact full source node id."));
            edge.Set("port", Field("string", "next, choice:N, true, false, or redirect."));
            edge.Set("to", Field("string", "Exact full target node id. Empty clears that port."));
            edge.Set("targetDialog", Field("string", "redirect only. Dialog name, not a nodeId."));
            edge.Set("targetCategory", Field("string", "redirect only. Category of targetDialog."));
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name in the database."));
            AddSessionSchema(properties);
            properties.Set("save", Field("boolean", "Default false. True saves only areas changed by this operation, including earlier pending edits within those areas. Unrelated pending edits remain unsaved."));
            properties.Set("addNodes", ArrayOf(node, "New nodes. id may be omitted."));
            properties.Set("setNodes", ArrayOf(node, "Update speaker, instant, position, or conditions on existing nodes."));
            properties.Set("setEdges", ArrayOf(edge, "One edge is from and to. Other ports stay."));
            properties.Set("removeNodes", StringArrayField("nodeId values to delete. Links that pointed at them are cleared."));
            return properties;
        }

        private static EndsMcpJsonED DialogCreateSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "New dialog name without dots. Dots are reserved for localization key separators."));
            properties.Set("category", Field("string", "Category. Added if missing. Default Unnamed."));
            return properties;
        }

        private static EndsMcpJsonED DialogRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Existing dialog name."));
            properties.Set("newName", Field("string", "Name after rename or duplicate, without dots. Dots are reserved for localization key separators."));
            return properties;
        }

        private static EndsMcpJsonED DialogNameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Name to delete."));
            return properties;
        }

        private static EndsMcpJsonED DialogSetSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Dialog name."));
            properties.Set("category", Field("string", "New category. Omit to leave it."));
            properties.Set("locked", Field("boolean", "Lock the dialog."));
            properties.Set("and", Field("boolean", "true means all start conditions."));
            properties.Set("conditions", ConditionArrayField("Replaces start conditions. Omit to leave them."));
            return properties;
        }

        private static EndsMcpJsonED CharacterCreateSchema()
        {
            EndsMcpJsonED sprite = SpriteItemSchema();
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "New character name."));
            properties.Set("copyFrom", Field("string", "Copy sprite slot names from this character."));
            properties.Set("sprites", ArrayOf(sprite, "Slots to create. file is an optional Sprite asset path."));
            return properties;
        }

        private static EndsMcpJsonED CharacterRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Current character name."));
            properties.Set("newName", Field("string", "New character name. Sprite actions that use it are updated."));
            return properties;
        }

        private static EndsMcpJsonED SpriteAddSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Character name."));
            properties.Set("sprites", ArrayOf(SpriteItemSchema(), "Slots to add or fill."));
            return properties;
        }

        private static EndsMcpJsonED SpriteRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Character name."));
            properties.Set("slot", Field("string", "Current sprite slot name."));
            properties.Set("newName", Field("string", "rename only. Emotion name on actions is updated."));
            return properties;
        }

        private static EndsMcpJsonED SpriteItemSchema()
        {
            EndsMcpJsonED sprite = EndsMcpJsonED.Obj();
            sprite.Set("slot", Field("string", "Sprite name, such as talk or idle."));
            sprite.Set("file", Field("string", "Asset path of a Sprite. Omit to create an empty slot."));
            return sprite;
        }

        private static EndsMcpJsonED VariableAddSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Variable name. Letters, digits, and underscore."));
            properties.Set("type", Field("string", "bool, int, float, or string. set can change it."));
            properties.Set("value", Untyped("true, false, a number, or a string. Match type."));
            return properties;
        }

        private static EndsMcpJsonED VariableRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Current variable name."));
            properties.Set("newName", Field("string", "New name. Dialog references are updated."));
            return properties;
        }

        private static EndsMcpJsonED VariableCurrentSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Variable name."));
            properties.Set("value", Untyped("Current value in G. true, false, a number, or a string. Match the variable type."));
            return properties;
        }

        private static EndsMcpJsonED CategoryNameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Category name. Delete moves its dialogs to Unnamed."));
            return properties;
        }

        private static EndsMcpJsonED CategoryRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Current category name."));
            properties.Set("newName", Field("string", "New category name. Dialogs and redirect targets are updated."));
            return properties;
        }

        private static EndsMcpJsonED NameRenameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Current name-color key."));
            properties.Set("newName", Field("string", "New key. Line speaker fields that use the old key are updated."));
            return properties;
        }

        private static EndsMcpJsonED LanguageSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("language", Field("string", "Language code, such as ru or en."));
            return properties;
        }

        private static EndsMcpJsonED LanguageRenameSchema()
        {
            EndsMcpJsonED properties = LanguageSchema();
            properties.Set("newName", Field("string", "New language code. Texts that used the old code are renamed."));
            return properties;
        }

        private static EndsMcpJsonED NameSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("name", Field("string", "Character name key in the name registry."));
            EndsMcpJsonED color = Field("string", "#RRGGBB or #RRGGBBAA, including alpha. Canonical Color also accepts exact RGBA components.");
            color.Set("pattern", EndsMcpJsonED.Str(EndsDialogRulesED.NAME_COLOR_HEX_PATTERN));
            properties.Set("color", color);
            properties.Set("texts", Field("object", "Language code to display name."));
            return properties;
        }

        private static EndsMcpJsonED LineActionSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name."));
            properties.Set("nodeId", Field("string", "Exact full Line node id."));
            properties.Set("index", Field("number", "set and remove only. Action index on that line."));
            properties.Set("character", Field("string", "Character name for this action."));
            properties.Set("emotion", Field("string", "Sprite slot name."));
            properties.Set("action", Field("string", "show or hide."));
            properties.Set("x", Field("number", "Portrait x."));
            properties.Set("y", Field("number", "Portrait y."));
            properties.Set("zoom", Field("number", "Portrait zoom."));
            return properties;
        }

        private static EndsMcpJsonED LineActionRemoveSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name."));
            properties.Set("nodeId", Field("string", "Exact full Line node id."));
            properties.Set("index", Field("number", "Action index to remove."));
            return properties;
        }

        private static EndsMcpJsonED LineActionCopySchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name."));
            properties.Set("fromNodeId", Field("string", "Exact full source Line node id."));
            properties.Set("fromIndex", Field("number", "Action index to copy."));
            properties.Set("nodeId", Field("string", "Exact full target Line node id."));
            properties.Set("index", Field("number", "Action index to overwrite."));
            return properties;
        }

        private static EndsMcpJsonED NodeDuplicateSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name."));
            properties.Set("nodeId", Field("string", "Node to duplicate."));
            return properties;
        }

        private static EndsMcpJsonED ChoiceIndexSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("dialog", Field("string", "Dialog name."));
            properties.Set("nodeId", Field("string", "Choice nodeId."));
            properties.Set("index", Field("number", "Choice option index. Later options shift down."));
            return properties;
        }

        private static EndsMcpJsonED ChoiceConditionSchema()
        {
            EndsMcpJsonED properties = ChoiceIndexSchema();
            properties.Set("and", Field("boolean", "true means all conditions on this option."));
            properties.Set("conditions", ConditionArrayField("Replaces conditions on this option."));
            return properties;
        }

        private static EndsMcpJsonED ChoiceConsequenceSchema()
        {
            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("variable", Field("string", "Variable name."));
            item.Set("type", Field("string", "bool, int, float, or string."));
            item.Set("value", Untyped("true, false, a number, or a string. Match type."));
            EndsMcpJsonED properties = ChoiceIndexSchema();
            properties.Set("consequences", ArrayOf(item, "Replaces consequences on this option."));
            return properties;
        }

        private static EndsMcpJsonED ConditionArrayField(string description)
        {
            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("variable", Field("string", "Variable name."));
            item.Set("type", Field("string", "bool, int, float, or string."));
            item.Set("op", Field("string", "equals, notEquals, greater, less, greaterOrEquals, or lessOrEquals."));
            item.Set("value", Untyped("true, false, a number, or a string. Match type."));
            return ArrayOf(item, description);
        }

        private static EndsMcpJsonED Field(string type, string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("type", EndsMcpJsonED.Str(type));
            property.Set("description", EndsMcpJsonED.Str(description));
            return property;
        }

        private static EndsMcpJsonED Untyped(string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("description", EndsMcpJsonED.Str(description));
            return property;
        }

        private static EndsMcpJsonED ArrayOf(EndsMcpJsonED itemProperties, string description)
        {
            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("type", EndsMcpJsonED.Str("object"));
            item.Set("properties", itemProperties);
            EndsMcpJsonED array = EndsMcpJsonED.Obj();
            array.Set("type", EndsMcpJsonED.Str("array"));
            array.Set("description", EndsMcpJsonED.Str(description));
            array.Set("items", item);
            return array;
        }

        private static EndsMcpJsonED StringArrayField(string description)
        {
            EndsMcpJsonED items = EndsMcpJsonED.Obj();
            items.Set("type", EndsMcpJsonED.Str("string"));
            EndsMcpJsonED array = EndsMcpJsonED.Obj();
            array.Set("type", EndsMcpJsonED.Str("array"));
            array.Set("description", EndsMcpJsonED.Str(description));
            array.Set("items", items);
            return array;
        }

        private static EndsMcpJsonED Tool(string name, string description, EndsMcpJsonED properties)
        {
            if (name.StartsWith("ends_write_", StringComparison.Ordinal) && name != "ends_write_database_set" && name != "ends_write_variable_open" && name != "ends_write_variable_save" && name != "ends_write_variable_current")
                description += " Database edits remain pending until ends_session_save or the editor Save.";

            if (EndsDialogContractAdapterED.Handles(name)) AddSessionSchema(properties);

            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            EndsMcpJsonED tool = EndsMcpJsonED.Obj();
            tool.Set("name", EndsMcpJsonED.Str(name));
            tool.Set("description", EndsMcpJsonED.Str(description));
            tool.Set("inputSchema", schema);

            return tool;
        }

        #endregion LOOKUP
    }
}
#endif