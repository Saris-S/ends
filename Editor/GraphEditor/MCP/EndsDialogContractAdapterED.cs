#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsDialogContractAdapterED
    {
        #region TOOLS

        internal static void AddTools(EndsMcpJsonED tools)
        {
            tools.Add(Tool("ends_contract_schema", "Reads registered models, operations and shared field constraints. Use the current schema for ends_contract_apply, diagnostics are read-only.", false));
            tools.Add(Tool("ends_contract_read", "Reads complete registered fields and session revision, including full node ids, diagnostic references and asset identities. Prefer this over filtered legacy reads before editing.", false));
            tools.Add(Tool("ends_contract_validate", "Reads existing graph integrity, missing references and start condition conflicts without changing or saving data. Writes are validated independently.", false));
            tools.Add(Tool("ends_contract_apply", "Applies an atomic batch through the shared UI session. For readable graph placement follow ends_describe topic layout. The placement report returns expanded sizes, conflicts and final positions. Changes remain pending by default.", true));
        }

        private static EndsMcpJsonED Tool(string name, string description, bool write)
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("database", Property("string", "Existing Dialog Database asset path."));

            if (write)
            {
                EndsMcpJsonED operations = Property("array", "Operations from ends_contract_schema.");
                EndsMcpJsonED row = EndsMcpJsonED.Obj();
                row.Set("type", EndsMcpJsonED.Str("object"));
                operations.Set("items", row);
                properties.Set("operations", operations);
                properties.Set("expectedRevision", Property("integer", "Reject the batch if the working revision changed."));
                properties.Set("save", Property("boolean", "Save all working edits after applying the batch."));
            }
            else
            {
                properties.Set("dialog", Property("string", "Optional dialog name."));

                if (name == "ends_contract_read")
                {
                    properties.Set("nodeId", Property("string", "Optional exact full node id within the selected dialog, for example LNN.5."));
                    properties.Set("includeEditorState", Property("boolean", "Include read-only state of editor windows using this database."));
                }
            }

            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            if (write) schema = EndsDialogGraphContractED.ReadApplyInputSchema();
            EndsMcpJsonED tool = EndsMcpJsonED.Obj();
            tool.Set("name", EndsMcpJsonED.Str(name));
            tool.Set("description", EndsMcpJsonED.Str(description));
            tool.Set("inputSchema", schema);

            return tool;
        }

        private static EndsMcpJsonED Property(string type, string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("type", EndsMcpJsonED.Str(type));
            property.Set("description", EndsMcpJsonED.Str(description));

            return property;
        }

        internal static bool Handles(string tool)
        {
            if (tool.StartsWith("ends_contract_", StringComparison.Ordinal)) return true;
            if (!tool.StartsWith("ends_write_", StringComparison.Ordinal)) return false;
            if (tool == "ends_write_database_set" || tool == "ends_write_text") return false;
            if (tool == "ends_write_variable_current" || tool == "ends_write_variable_load" || tool == "ends_write_variable_save" || tool == "ends_write_variable_open") return false;

            return true;
        }

        internal static bool TryDispatch(string tool, EndsMcpJsonED args, out string json, out string error)
        {
            json = null;
            error = null;
            DialogDatabase database = null;

            try
            {
                if (tool == "ends_contract_schema")
                {
                    json = EndsDialogGraphContractED.ReadSchema().Write();

                    return true;
                }

                database = EndsDialogAiWriteED.RequestDatabase(args, out error);

                if (database == null) return false;

                if (tool == "ends_contract_validate")
                {
                    EndsMcpJsonED report = EndsMcpJsonED.Obj();
                    report.Set("session", EndsSessionED.ForDatabase(database).ReadState());
                    report.Set("validation", EndsDialogGraphContractED.ReadValidationReport(database, Optional(args, "dialog")));
                    json = report.Write();

                    return true;
                }

                if (tool == "ends_contract_read")
                {
                    object model = database;
                    string dialogName = Optional(args, "dialog");

                    if (!string.IsNullOrEmpty(dialogName))
                    {
                        DialogEntry dialog = EndsDialogConditionContractED.FindDialog(database, dialogName);
                        model = dialog;
                        string nodeId = Optional(args, "nodeId");

                        if (!string.IsNullOrEmpty(nodeId))
                        {
                            if (!EndsNodeRefED.TryResolveInDialog(dialog, nodeId, out DialogNode node, out error)) return false;

                            model = node;
                        }
                    }
                    else if (args.Get("nodeId") != null) throw new InvalidOperationException("nodeId requires dialog.");

                    EndsMcpJsonED root = EndsMcpJsonED.Obj();
                    root.Set("session", EndsSessionED.ForDatabase(database).ReadState());
                    root.Set("model", EndsDialogGraphContractED.ReadModel(model));

                    if (args.Get("includeEditorState") != null && EndsDialogGraphContractED.Boolean(args.Get("includeEditorState"))) root.Set("editorWindows", DialogEditorWindowED.ReadEditorState(database));

                    json = root.Write();

                    return true;
                }

                EndsMcpJsonED commands;

                if (tool == "ends_contract_apply") commands = args.Get("operations");
                else commands = TranslateLegacy(database, tool, args);

                EndsSessionED.Result result = EndsDialogGraphContractED.ExecuteBatch(database, commands, ExpectedRevision(args), SaveRequested(args), out EndsMcpJsonED output, solvePlacement: true);
                EndsMcpJsonED reply = EndsSessionED.WriteResult(result);
                reply.Set("results", output.Get("results"));

                if (output.Get("placement") != null) reply.Set("placement", output.Get("placement"));
                if (output.Get("runtimeWarning") != null) reply.Set("runtimeWarning", output.Get("runtimeWarning"));
                if (result.Changed) EndsAiHighlightED.NoteResult(database, result);

                if (result.Success)
                {
                    AddLegacyResult(reply, tool, args, commands, output.Get("results"));

                    if (result.Changed) EndsAiHighlightED.Note(tool, args);
                }

                if (result.Status == "saved" && result.Changed) EndsAiHighlightED.SaveBoundMemory();

                json = reply.Write();
                error = result.Error;

                return result.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                EndsMcpJsonED rejected = EndsMcpJsonED.Obj();
                rejected.Set("status", EndsMcpJsonED.Str("rejected"));
                rejected.Set("error", EndsMcpJsonED.Str(error));
                rejected.Set("changed", EndsMcpJsonED.Boolean(false));

                if (database != null) rejected = EndsSessionED.WriteResult(EndsSessionED.ForDatabase(database).Reject(exception));
                json = rejected.Write();

                return false;
            }
        }

        internal static int? ExpectedRevision(EndsMcpJsonED args)
        {
            EndsMcpJsonED value = args.Get("expectedRevision");

            if (value == null) return null;

            int revision = EndsDialogGraphContractED.Integer(value);

            if (revision < 0) throw new InvalidOperationException("expectedRevision must be non-negative.");

            return revision;
        }

        internal static bool SaveRequested(EndsMcpJsonED args)
        {
            if (args.Get("save") == null) return false;

            return EndsDialogGraphContractED.Boolean(args.Get("save"));
        }

        #endregion TOOLS

        #region LEGACY REQUESTS

        private static EndsMcpJsonED TranslateLegacy(DialogDatabase database, string tool, EndsMcpJsonED args)
        {
            EndsMcpJsonED commands = EndsMcpJsonED.Arr();

            if (tool == "ends_write_graph")
            {
                TranslateGraph(database, args, commands);

                return commands;
            }

            string operation = "";

            switch (tool)
            {
                case "ends_write_node_duplicate": operation = "node.duplicate"; break;
                case "ends_write_dialog_create": operation = "dialog.add"; break;
                case "ends_write_dialog_rename": operation = "dialog.rename"; break;
                case "ends_write_dialog_delete": operation = "dialog.remove"; break;
                case "ends_write_dialog_duplicate": operation = "dialog.duplicate"; break;
                case "ends_write_dialog_set": operation = "dialog.patch"; break;
                case "ends_write_character_create": operation = "character.add"; break;
                case "ends_write_character_rename": operation = "character.rename"; break;
                case "ends_write_character_delete": operation = "character.remove"; break;
                case "ends_write_sprite_add": operation = "sprite.add"; break;
                case "ends_write_sprite_rename": operation = "sprite.rename"; break;
                case "ends_write_sprite_remove": operation = "sprite.remove"; break;
                case "ends_write_variable_add": operation = "variable.add"; break;
                case "ends_write_variable_rename": operation = "variable.rename"; break;
                case "ends_write_variable_delete": operation = "variable.remove"; break;
                case "ends_write_variable_set": operation = "variable.patch"; break;
                case "ends_write_language_add": operation = "language.add"; break;
                case "ends_write_language_rename": operation = "language.rename"; break;
                case "ends_write_language_remove": operation = "language.remove"; break;
                case "ends_write_language_delete_texts": operation = "language.deleteTexts"; break;
                case "ends_write_language_set": operation = "database.patch"; break;
                case "ends_write_category_add": operation = "category.add"; break;
                case "ends_write_category_rename": operation = "category.rename"; break;
                case "ends_write_category_delete": operation = "category.remove"; break;
                case "ends_write_name_set": operation = "name.patch"; break;
                case "ends_write_name_rename": operation = "name.rename"; break;
                case "ends_write_name_delete": operation = "name.remove"; break;
                case "ends_write_line_action_add": operation = "action.add"; break;
                case "ends_write_line_action_set": operation = "action.patch"; break;
                case "ends_write_line_action_remove": operation = "action.remove"; break;
                case "ends_write_line_action_copy": operation = "action.copy"; break;
                case "ends_write_choice_remove": operation = "choice.remove"; break;
                case "ends_write_choice_condition_set": operation = "choice.patch"; break;
                case "ends_write_choice_consequence_set": operation = "choice.patch"; break;
                default: throw new InvalidOperationException("Unknown tool " + tool + ".");
            }

            EndsMcpJsonED command = EndsDialogGraphContractED.Command(operation);
            string name = Optional(args, "name");

            if (operation.StartsWith("node.", StringComparison.Ordinal) || operation.StartsWith("action.", StringComparison.Ordinal) || operation.StartsWith("choice.", StringComparison.Ordinal))
            {
                command.Set("dialog", Required(args, "dialog"));
                command.Set("node", Required(args, "nodeId"));
            }
            else if (operation.StartsWith("dialog.", StringComparison.Ordinal) && operation != "dialog.add") command.Set("dialog", Required(args, "name"));
            else if (operation != "database.patch") command.Set("name", EndsMcpJsonED.Str(name));

            if (operation.StartsWith("language.", StringComparison.Ordinal)) command.Set("name", Required(args, "language"));
            if (operation.EndsWith(".rename", StringComparison.Ordinal) && (operation != "sprite.rename" || args.Get("newName") != null)) command.Set("newName", Required(args, "newName"));

            if (operation == "dialog.add")
            {
                command.Set("category", EndsMcpJsonED.Str(Optional(args, "category")));
                command.Set("position", Point(120f, 120f));
            }

            if (operation == "dialog.duplicate")
            {
                string newName = Optional(args, "newName");

                if (!string.IsNullOrEmpty(newName)) command.Set("newName", EndsMcpJsonED.Str(newName));
            }

            if (operation == "dialog.patch")
            {
                EndsMcpJsonED patch = EndsMcpJsonED.Obj();
                Copy(args, "category", patch, nameof(DialogEntry.initiator));
                Copy(args, "locked", patch, nameof(DialogEntry.isLocked));
                Copy(args, "and", patch, nameof(DialogEntry.isAndLogic));

                if (args.Get("conditions") != null) patch.Set(nameof(DialogEntry.conditions), EndsDialogConditionContractED.TranslateConditions(args.Get("conditions")));

                command.Set("patch", patch);
            }

            if (operation == "character.add")
            {
                if (args.Get("copyFrom") != null) command.Set("copyFrom", Required(args, "copyFrom"));
            }

            if (operation.StartsWith("sprite.", StringComparison.Ordinal))
            {
                if (operation != "sprite.add") command.Set("slot", Required(args, "slot"));
                if (operation == "sprite.rename" && args.Get("newName") == null) command.Set("newName", Required(args, "newSlot"));
            }

            if (operation == "variable.add" || operation == "variable.patch")
            {
                EndsMcpJsonED patch = VariablePatch(database, args, name);
                command.Set("patch", patch);
            }

            if (operation == "database.patch")
            {
                EndsMcpJsonED patch = EndsMcpJsonED.Obj();
                patch.Set(nameof(DialogDatabase.CurrentLanguage), Required(args, "language"));
                command.Set("patch", patch);
            }

            if (operation == "name.patch")
            {
                if (EndsNameCommandsED.Find(database, name) == null)
                {
                    command = EndsDialogGraphContractED.Command("name.add");
                    command.Set("name", EndsMcpJsonED.Str(name));
                }

                EndsMcpJsonED patch = EndsMcpJsonED.Obj();
                Copy(args, "color", patch, nameof(CharacterNameColor.Color));

                if (args.Get("texts") != null) patch.Set(nameof(CharacterNameColor.Translations), NameTranslations(EndsNameCommandsED.Find(database, name), args.Get("texts")));

                command.Set("patch", patch);
            }

            if (operation.StartsWith("action.", StringComparison.Ordinal))
            {
                if (operation != "action.add") command.Set("index", Required(args, "index"));
                if (operation == "action.add" || operation == "action.patch") command.Set("patch", ActionPatch(args));

                if (operation == "action.copy")
                {
                    command.Set("sourceNode", Required(args, "fromNodeId"));
                    command.Set("sourceIndex", Required(args, "fromIndex"));
                }
            }

            if (operation.StartsWith("choice.", StringComparison.Ordinal))
            {
                command.Set("index", Required(args, "index"));

                if (operation == "choice.patch")
                {
                    EndsMcpJsonED patch = EndsMcpJsonED.Obj();
                    Copy(args, "and", patch, nameof(ChoiceNode.IsAndLogic));

                    if (args.Get("conditions") != null) patch.Set(nameof(ChoiceNode.ChoiceCondition), ChoiceValues(args.Get("conditions"), false));
                    if (args.Get("consequences") != null) patch.Set(nameof(ChoiceNode.ChoiceConsequence), ChoiceValues(args.Get("consequences"), true));

                    command.Set("patch", patch);
                }
            }

            if (operation != "sprite.add") commands.Add(command);

            if (operation == "character.add" || operation == "sprite.add") AddSpriteCommands(database, args, commands, name, operation == "character.add");

            return commands;
        }

        #endregion LEGACY REQUESTS

        #region GRAPH REQUESTS

        private static void TranslateGraph(DialogDatabase database, EndsMcpJsonED args, EndsMcpJsonED commands)
        {
            string dialogName = EndsDialogGraphContractED.String(Required(args, "dialog"));
            DialogEntry dialog = EndsDialogConditionContractED.FindDialog(database, dialogName);
            EndsMcpJsonED added = args.Get("addNodes");

            foreach (EndsMcpJsonED item in ArrayItems(added))
            {
                string id = Optional(item, "id");
                string type = Optional(item, "type");

                if (!Enum.TryParse(type, true, out NodeType nodeType) || !Enum.IsDefined(typeof(NodeType), nodeType)) throw new InvalidOperationException("Unknown node type " + type + ".");

                if (string.IsNullOrEmpty(id))
                {
                    id = EndsNodeIdentityED.Allocate(database, dialog, nodeType);
                    item.Set("id", EndsMcpJsonED.Str(id));
                }

                EndsMcpJsonED command = EndsDialogGraphContractED.Command("node.add");
                command.Set("dialog", EndsMcpJsonED.Str(dialogName));
                command.Set("id", EndsMcpJsonED.Str(id));
                command.Set("type", EndsMcpJsonED.Str(nodeType.ToString()));

                if (item.Get("position") != null || item.Get("x") != null || item.Get("y") != null) command.Set("position", LegacyPosition(item, Vector2.zero));

                command.Set("patch", NodePatch(item, nodeType, Vector2.zero));
                commands.Add(command);
            }

            foreach (EndsMcpJsonED item in ArrayItems(args.Get("setNodes")))
            {
                string id = EndsDialogGraphContractED.String(Required(item, "id"));
                NodeType type = NodeType.Condition;
                Vector2 position = Vector2.zero;

                if (EndsNodeRefED.TryResolveInDialog(dialog, id, out DialogNode node, out _))
                {
                    type = node.NodeType;
                    position = node.Position;
                }

                if (added?.Items != null)
                {
                    foreach (EndsMcpJsonED add in added.Items)
                    {
                        if (Optional(add, "id") != id) continue;

                        Enum.TryParse(Optional(add, "type"), true, out type);
                    }
                }

                EndsMcpJsonED command = EndsDialogGraphContractED.NodeCommand("node.patch", dialogName, id);
                command.Set("patch", NodePatch(item, type, position));
                commands.Add(command);
            }

            foreach (EndsMcpJsonED item in ArrayItems(args.Get("setEdges")))
            {
                EndsMcpJsonED command = EndsDialogGraphContractED.NodeCommand("edge.set", dialogName, EndsDialogGraphContractED.String(Required(item, "from")));
                command.Set("port", Required(item, "port"));
                Copy(item, "to", command, "to");
                Copy(item, "targetCategory", command, "category");
                Copy(item, "targetDialog", command, "targetDialog");
                commands.Add(command);
            }

            foreach (EndsMcpJsonED id in ArrayItems(args.Get("removeNodes")))
            {
                commands.Add(EndsDialogGraphContractED.NodeCommand("node.remove", dialogName, EndsDialogGraphContractED.String(id)));
            }
        }

        private static EndsMcpJsonED NodePatch(EndsMcpJsonED item, NodeType type, Vector2 position)
        {
            EndsMcpJsonED patch = EndsMcpJsonED.Obj();

            if (item.Get("patch") != null) patch = EndsMcpJsonED.Parse(item.Get("patch").Write());
            if (item.Get("position") != null || item.Get("x") != null || item.Get("y") != null) patch.Set(nameof(DialogNode.Position), LegacyPosition(item, position));

            if (item.Get("speaker") != null || item.Get("instant") != null)
            {
                EndsMcpJsonED line = patch.Get(nameof(DialogNode.LineData)) ?? EndsMcpJsonED.Obj();
                Copy(item, "speaker", line, nameof(LineNode.CharacterName));
                Copy(item, "instant", line, nameof(LineNode.isInstant));
                patch.Set(nameof(DialogNode.LineData), line);
            }

            if (item.Get("conditions") != null || item.Get("and") != null)
            {
                EndsMcpJsonED condition = patch.Get(nameof(DialogNode.ConditionData)) ?? EndsMcpJsonED.Obj();
                Copy(item, "and", condition, nameof(ConditionNode.IsAndLogic));

                if (item.Get("conditions") != null) condition.Set(nameof(ConditionNode.DialogConditions), EndsDialogConditionContractED.TranslateConditions(item.Get("conditions")));

                patch.Set(nameof(DialogNode.ConditionData), condition);
            }

            if (item.Get("targetCategory") != null || item.Get("targetDialog") != null)
            {
                EndsMcpJsonED redirect = patch.Get(nameof(DialogNode.RedirectData)) ?? EndsMcpJsonED.Obj();
                Copy(item, "targetCategory", redirect, nameof(RedirectNode.TargetCategory));
                Copy(item, "targetDialog", redirect, nameof(RedirectNode.TargetDialogName));
                patch.Set(nameof(DialogNode.RedirectData), redirect);
            }

            return patch;
        }

        private static EndsMcpJsonED LegacyPosition(EndsMcpJsonED item, Vector2 fallback)
        {
            EndsMcpJsonED position = item.Get("position");

            if (position != null)
            {
                if (position.Type == EndsMcpJsonED.Kind.Object) return position;
                if (position.Type != EndsMcpJsonED.Kind.Array || position.Items == null || position.Items.Count != 2) throw new InvalidOperationException("position requires two numbers.");

                return Point(EndsDialogGraphContractED.Number(position.Items[0]), EndsDialogGraphContractED.Number(position.Items[1]));
            }

            if (item.Get("x") != null) fallback.x = EndsDialogGraphContractED.Number(item.Get("x"));
            if (item.Get("y") != null) fallback.y = EndsDialogGraphContractED.Number(item.Get("y"));

            return Point(fallback.x, fallback.y);
        }

        internal static EndsMcpJsonED Point(float x, float y)
        {
            return EndsDialogGraphContractED.Point(x, y);
        }

        #endregion GRAPH REQUESTS

        #region REGISTRY REQUESTS

        private static EndsMcpJsonED VariablePatch(DialogDatabase database, EndsMcpJsonED args, string name)
        {
            VariableDefinition existing = EndsVariableCommandsED.Find(database, name);
            ConditionVariableType type = ConditionVariableType.Bool;

            if (existing != null) type = existing.variableType;

            EndsMcpJsonED patch = EndsMcpJsonED.Obj();

            if (args.Get("type") != null)
            {
                string typeName = EndsDialogGraphContractED.String(args.Get("type"));

                if (!Enum.TryParse(typeName, true, out type) || !Enum.IsDefined(typeof(ConditionVariableType), type)) throw new InvalidOperationException("Unknown variable type.");

                patch.Set(nameof(VariableDefinition.variableType), EndsMcpJsonED.Str(type.ToString()));
            }

            if (args.Get("value") != null)
            {
                if (type == ConditionVariableType.Bool) patch.Set(nameof(VariableDefinition.boolDefault), args.Get("value"));
                if (type == ConditionVariableType.Int) patch.Set(nameof(VariableDefinition.intDefault), args.Get("value"));
                if (type == ConditionVariableType.Float) patch.Set(nameof(VariableDefinition.floatDefault), args.Get("value"));
                if (type == ConditionVariableType.String) patch.Set(nameof(VariableDefinition.stringDefault), args.Get("value"));
            }

            return patch;
        }

        private static EndsMcpJsonED ActionPatch(EndsMcpJsonED args)
        {
            EndsMcpJsonED patch = EndsMcpJsonED.Obj();
            Copy(args, "character", patch, nameof(CharacterActionData.CharacterName));
            Copy(args, "emotion", patch, nameof(CharacterActionData.EmotionName));
            Copy(args, "zoom", patch, nameof(CharacterActionData.Zoom));

            if (args.Get("action") != null) patch.Set(nameof(CharacterActionData.Action), args.Get("action"));

            if (args.Get("position") != null || args.Get("x") != null || args.Get("y") != null)
            {
                EndsMcpJsonED point = LegacyPosition(args, Vector2.zero);
                patch.Set(nameof(CharacterActionData.PosX), point.Get("x"));
                patch.Set(nameof(CharacterActionData.PosY), point.Get("y"));
            }

            return patch;
        }

        private static EndsMcpJsonED ChoiceValues(EndsMcpJsonED values, bool consequences)
        {
            EndsMcpJsonED result = EndsDialogConditionContractED.TranslateConditions(values);

            if (consequences)
            {
                foreach (EndsMcpJsonED item in result.Items)
                {
                    if (item.Type != EndsMcpJsonED.Kind.Null) item.Remove(nameof(DialogCondition.ConditionOperator));
                }
            }

            return result;
        }

        private static EndsMcpJsonED NameTranslations(CharacterNameColor source, EndsMcpJsonED texts)
        {
            if (texts.Type != EndsMcpJsonED.Kind.Object || texts.Keys == null) throw new InvalidOperationException("texts must be an object.");

            CharacterNameColor copy = EndsDialogGraphContractED.Snapshot(source);
            List<NameTranslation> translations = copy?.Translations ?? new List<NameTranslation>();

            foreach (string language in texts.Keys)
            {
                NameTranslation existing = translations.Find(item => item != null && item.languageCode == language);

                if (existing == null)
                {
                    existing = new NameTranslation { languageCode = language };
                    translations.Add(existing);
                }

                existing.displayName = EndsDialogGraphContractED.String(texts.Get(language));
            }

            return EndsDialogGraphContractED.ReadModel(translations);
        }

        private static void AddSpriteCommands(DialogDatabase database, EndsMcpJsonED args, EndsMcpJsonED commands, string name, bool newCharacter)
        {
            foreach (EndsMcpJsonED item in ArrayItems(args.Get("sprites")))
            {
                string slot = Optional(item, "slot");

                if (string.IsNullOrEmpty(slot)) slot = Optional(item, "spriteName");
                if (string.IsNullOrEmpty(slot)) throw new InvalidOperationException("Sprite slot is empty.");

                CharacterGroup group = EndsCharacterCommands.Find(database, name);

                if (newCharacter) group = EndsCharacterCommands.Find(database, Optional(args, "copyFrom"));

                bool exists = EndsCharacterCommands.FindSprite(group, slot) != null;

                foreach (EndsMcpJsonED previous in commands.Items)
                {
                    if (previous.Get("op")?.Text == "sprite.add" && previous.Get("slot")?.Text == slot) exists = true;
                }
                string operation = "sprite.add";

                if (exists) operation = "sprite.patch";

                EndsMcpJsonED command = EndsDialogGraphContractED.Command(operation);
                command.Set("name", EndsMcpJsonED.Str(name));
                command.Set("slot", EndsMcpJsonED.Str(slot));
                EndsMcpJsonED patch = EndsMcpJsonED.Obj();

                if (item.Get("file") != null) patch.Set(nameof(CharacterSprite.sprite), item.Get("file"));

                command.Set("patch", patch);
                commands.Add(command);
            }
        }

        #endregion REGISTRY REQUESTS

        #region RESPONSES AND VALUES

        private static void AddLegacyResult(EndsMcpJsonED reply, string tool, EndsMcpJsonED args, EndsMcpJsonED commands, EndsMcpJsonED results)
        {
            if (tool == "ends_write_graph")
            {
                EndsMcpJsonED added = EndsMcpJsonED.Arr();

                foreach (EndsMcpJsonED item in ArrayItems(args.Get("addNodes")))
                {
                    added.Add(EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(Optional(item, "id"))));
                }

                reply.Set("added", added);
                reply.Set("edges", args.Get("setEdges") ?? EndsMcpJsonED.Arr());
            }

            if (results?.Items != null && results.Items.Count > 0)
            {
                EndsMcpJsonED first = results.Items[0];

                if (first.Get("nodeId") != null)
                {
                    string nodeId = EndsDialogGraphContractED.String(first.Get("nodeId"));
                    reply.Set("nodeId", EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(nodeId)));

                    if (tool == "ends_write_dialog_create" || tool == "ends_write_dialog_duplicate") reply.Set("startNodeId", EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(nodeId)));
                }

                if (first.Get("index") != null) reply.Set("index", first.Get("index"));
            }

            string name = Optional(args, "name");

            if (tool.EndsWith("_rename", StringComparison.Ordinal) || tool == "ends_write_dialog_duplicate") name = Optional(commands.Items[0], "newName");
            if (results?.Items?.Count > 0 && results.Items[0].Get("dialog") != null) name = results.Items[0].Get("dialog").Text;
            if (tool.StartsWith("ends_write_dialog_", StringComparison.Ordinal)) reply.Set("dialog", EndsMcpJsonED.Str(name));
            if (tool.StartsWith("ends_write_character_", StringComparison.Ordinal)) reply.Set("character", EndsMcpJsonED.Str(name));
            if (tool.StartsWith("ends_write_variable_", StringComparison.Ordinal)) reply.Set("variable", EndsMcpJsonED.Str(name));
        }

        private static IEnumerable<EndsMcpJsonED> ArrayItems(EndsMcpJsonED array)
        {
            if (array == null) return Array.Empty<EndsMcpJsonED>();
            if (array.Type != EndsMcpJsonED.Kind.Array || array.Items == null) throw new InvalidOperationException("Expected an array.");

            return array.Items;
        }

        private static string Optional(EndsMcpJsonED input, string name)
        {
            EndsMcpJsonED value = input.Get(name);

            if (value == null) return "";

            return EndsDialogGraphContractED.String(value);
        }

        private static EndsMcpJsonED Required(EndsMcpJsonED input, string name)
        {
            if (input == null || input.Type != EndsMcpJsonED.Kind.Object || input.Get(name) == null) throw new InvalidOperationException("Missing " + name + ".");

            return input.Get(name);
        }

        private static void Copy(EndsMcpJsonED input, string source, EndsMcpJsonED output, string target)
        {
            if (input.Get(source) != null) output.Set(target, input.Get(source));
        }

        #endregion RESPONSES AND VALUES
    }
}
#endif