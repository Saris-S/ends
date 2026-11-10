#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsDialogAiReadED
    {
        #region FIELDS

        private struct TextPatch
        {
            public string Key;
            public string Language;
            public string Text;
        }

        private struct ReadRequest
        {
            public List<string> Languages;
            public bool Text;
            public bool Branches;
            public bool Animations;
            public bool Conditions;
            public bool Voice;
            public bool Layout;
            public string Find;
            public string Focus;
        }

        #endregion FIELDS

        #region TOOLS

        internal static EndsMcpJsonED ToolList()
        {
            EndsMcpJsonED tools = EndsMcpJsonED.Arr();
            tools.Add(EndsMcpDocumentationED.Tool());
            tools.Add(Tool(
                "ends_read_catalog",
                "ENDS. Lists languages, categories, dialog names, node counts, characters, variables, and display names. Does not return line text.",
                CatalogSchema()));
            tools.Add(Tool(
                "ends_read_dialog",
                "ENDS. Reads a filtered projection. Use ends_contract_read for complete registered fields. "
                + "Flags text, branches, animations, conditions, voice, and layout choose the slice. focus accepts a full node id such as LNN.5, find is a phrase.",
                ReadSchema()));
            tools.Add(Tool(
                "ends_write_text",
                "ENDS. Writes localized line and choice text. Each patch needs key {dialogName}.{nodeId}.line or {dialogName}.{nodeId}.{optionId}, plus language fields or a texts object. "
                + "Line-only sayers and voices maps are supported. Unknown keys and languages are rejected. Does not change edges. Changes remain pending until ends_session_save or the editor Save.",
                WriteSchema()));
            EndsDialogAiWriteED.AddTools(tools);
            EndsMcpHostED.AddTransportTools(tools);
            return tools;
        }

        private static EndsMcpJsonED CatalogSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            EndsMcpJsonED database = EndsMcpJsonED.Obj();
            database.Set("type", EndsMcpJsonED.Str("string"));
            database.Set("description", EndsMcpJsonED.Str("Existing Dialog Database asset path. Omit for the current database."));
            properties.Set("database", database);

            return properties;
        }

        internal static bool TryCatalog(EndsMcpJsonED args, out string json, out string error)
        {
            json = null;
            DialogDatabase database = EndsDialogAiWriteED.RequestDatabase(args, out error);

            if (database == null) return false;

            EndsMcpJsonED root = EndsMcpJsonED.Obj();
            root.Set("asset", EndsMcpJsonED.Str(EndsSessionED.AssetPath(database)));
            root.Set("session", EndsSessionED.ForDatabase(database).ReadState());
            root.Set("languages", StringArray(AvailableLanguages(database)));
            root.Set("currentLanguage", EndsMcpJsonED.Str(database.CurrentLanguage ?? ""));
            root.Set("categories", CategoryArray(database));
            root.Set("dialogs", CatalogDialogs(database));
            root.Set("characters", CatalogCharacters(database));
            root.Set("variables", CatalogVariables(database));
            root.Set("names", CatalogNames(database));
            json = root.Write();
            return true;
        }

        internal static bool TryRead(EndsMcpJsonED args, out string json, out string error)
        {
            json = null;

            if (!TryFlags(args, out ReadRequest request, out error)) return false;

            DialogDatabase database = EndsDialogAiWriteED.RequestDatabase(args, out error);

            if (database == null) return false;
            if (!TryLanguages(database, args, out List<string> languages, out error)) return false;

            request.Languages = languages;
            string dialogName = OptionalString(args, "dialog");
            string find = request.Find;

            if (string.IsNullOrEmpty(dialogName) && string.IsNullOrEmpty(find))
            {
                error = "Pass dialog or find.";
                return false;
            }

            if (!string.IsNullOrEmpty(dialogName))
            {
                if (!TryOneDialog(database, dialogName, out DialogEntry dialog, out error)) return false;

                if (!string.IsNullOrEmpty(request.Focus))
                {
                    if (!EndsNodeRefED.TryResolveInDialog(dialog, request.Focus, out DialogNode focusNode, out error)) return false;

                    request.Focus = focusNode.nodeId;
                }

                EndsMcpJsonED slice = Slice(dialog, request);
                slice.Set("session", EndsSessionED.ForDatabase(database).ReadState());
                if (!string.IsNullOrEmpty(find)) slice.Set("find", EndsMcpJsonED.Str(find));
                json = slice.Write();
                return true;
            }

            EndsMcpJsonED root = EndsMcpJsonED.Obj();
            root.Set("find", EndsMcpJsonED.Str(find));
            root.Set("session", EndsSessionED.ForDatabase(database).ReadState());
            EndsMcpJsonED matches = EndsMcpJsonED.Arr();

            if (database.Dialogs != null)
            {
                for (int i = 0; i < database.Dialogs.Count; i++)
                {
                    DialogEntry dialog = database.Dialogs[i];

                    if (dialog == null) continue;
                    if (!DialogHits(dialog, find)) continue;

                    matches.Add(Slice(dialog, request));
                }
            }

            root.Set("matches", matches);
            json = root.Write();

            return true;
        }

        internal static bool TryWriteText(EndsMcpJsonED args, out string json, out string error)
        {
            json = null;
            DialogDatabase database = EndsDialogAiWriteED.RequestDatabase(args, out error);

            if (database == null) return false;
            if (!TryCollectPatches(database, args, out List<TextPatch> patches, out error)) return false;

            try
            {
                EndsMcpJsonED commands = EndsMcpJsonED.Arr();

                foreach (TextPatch patch in patches)
                {
                    EndsMcpJsonED command = EndsDialogGraphContractED.Command("localization.text");
                    command.Set("key", EndsMcpJsonED.Str(patch.Key));
                    command.Set("language", EndsMcpJsonED.Str(patch.Language));
                    command.Set("text", EndsMcpJsonED.Str(patch.Text));
                    commands.Add(command);
                }

                CollectSlotDetails(args, commands);
                EndsSessionED.Result result = EndsDialogGraphContractED.ExecuteBatch(database, commands,
                    EndsDialogContractAdapterED.ExpectedRevision(args), EndsDialogContractAdapterED.SaveRequested(args), out EndsMcpJsonED output, solvePlacement: true);
                EndsMcpJsonED root = EndsSessionED.WriteResult(result);
                List<string> written = new List<string>();
                List<string> unchanged = new List<string>();

                if (result.Success)
                {
                    for (int i = 0; i < patches.Count; i++)
                    {
                        string label = patches[i].Key + " " + patches[i].Language;

                        if (output.Get("results").Items[i].Get("changed").Bool) written.Add(label);
                        else unchanged.Add(label);
                    }

                }

                if (result.Changed)
                {
                    EndsAiHighlightED.NoteResult(database, result);

                    if (result.Success) EndsAiHighlightED.Note("ends_write_text", args);

                    if (result.Status == "saved") EndsAiHighlightED.SaveBoundMemory();
                }

                root.Set("written", StringArray(written));
                root.Set("unchanged", StringArray(unchanged));

                if (output.Get("placement") != null) root.Set("placement", output.Get("placement"));

                json = root.Write();
                error = result.Error;

                return result.Success;
            }
            catch (Exception exception)
            {
                error = exception.Message;

                return false;
            }
        }

        #endregion TOOLS

        #region SLICE

        private static EndsMcpJsonED Slice(DialogEntry dialog, ReadRequest request)
        {
            EndsMcpJsonED root = EndsMcpJsonED.Obj();

            if (dialog.dialogName == null) root.Set("dialog", EndsMcpJsonED.Str(""));
            else root.Set("dialog", EndsMcpJsonED.Str(dialog.dialogName));

            if (dialog.initiator == null) root.Set("category", EndsMcpJsonED.Str(""));
            else root.Set("category", EndsMcpJsonED.Str(dialog.initiator));

            if (request.Conditions)
            {
                root.Set("and", EndsMcpJsonED.Boolean(dialog.isAndLogic));
                root.Set("locked", EndsMcpJsonED.Boolean(dialog.isLocked));
                root.Set("conditions", ConditionArray(dialog.conditions));
            }

            bool nameHit = !string.IsNullOrEmpty(request.Find) && dialog.dialogName != null && dialog.dialogName.IndexOf(request.Find, StringComparison.OrdinalIgnoreCase) >= 0;
            EndsMcpJsonED nodes = EndsMcpJsonED.Arr();
            HashSet<string> included = new HashSet<string>();

            if (dialog.nodes != null)
            {
                for (int i = 0; i < dialog.nodes.Count; i++)
                {
                    DialogNode node = dialog.nodes[i];

                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;
                    if (!string.IsNullOrEmpty(request.Focus) && node.nodeId != request.Focus) continue;
                    if (string.IsNullOrEmpty(request.Focus) && !string.IsNullOrEmpty(request.Find) && !nameHit && !NodeHits(node, request.Find)) continue;

                    included.Add(node.nodeId);
                    nodes.Add(NodeJson(node, request));
                }
            }

            root.Set("nodes", nodes);

            if (request.Branches) root.Set("edges", EdgeJson(dialog, included));

            return root;
        }

        private static EndsMcpJsonED NodeJson(DialogNode node, ReadRequest request)
        {
            EndsMcpJsonED json = EndsMcpJsonED.Obj();
            json.Set("nodeId", EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(node.nodeId)));
            json.Set("type", EndsMcpJsonED.Str(TypeName(node.NodeType)));

            if (request.Layout)
            {
                json.Set("x", EndsMcpJsonED.Num((int)node.Position.x));
                json.Set("y", EndsMcpJsonED.Num((int)node.Position.y));
            }

            if (request.Text && node.NodeType == NodeType.Line && node.LineData != null) json.Set("texts", TextObject(node.LineData.DialogsText, request.Languages));
            if (request.Voice && node.NodeType == NodeType.Line && node.LineData != null) json.Set("voices", VoiceObject(node.LineData.DialogsText, request.Languages));
            if (request.Animations && node.NodeType == NodeType.Line && node.LineData != null)
            {
                if (node.LineData.CharacterName == null) json.Set("speaker", EndsMcpJsonED.Str(""));
                else json.Set("speaker", EndsMcpJsonED.Str(node.LineData.CharacterName));
                json.Set("instant", EndsMcpJsonED.Boolean(node.LineData.isInstant));
                json.Set("actions", ActionArray(node.LineData.CharacterActions));
            }

            if ((request.Text || request.Branches || request.Conditions) && node.NodeType == NodeType.Choice) json.Set("choices", ChoiceArray(node, request));

            if (request.Conditions && node.NodeType == NodeType.Condition && node.ConditionData != null)
            {
                EndsDialogConditionContractED.ReadNode(node.ConditionData, json);
            }

            return json;
        }

        private static EndsMcpJsonED ChoiceArray(DialogNode node, ReadRequest request)
        {
            EndsMcpJsonED choices = EndsMcpJsonED.Arr();

            if (node.ChoiceData == null) return choices;

            for (int i = 0; i < node.ChoiceData.Count; i++)
            {
                ChoiceNode choice = node.ChoiceData[i];
                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                item.Set("index", EndsMcpJsonED.Num(i));

                if (choice == null)
                {
                    choices.Add(item);
                    continue;
                }

                if (request.Text) item.Set("texts", TextObject(choice.Translations, request.Languages));
                if (request.Branches) item.Set("to", NodeRef(ChoiceOptionSchema.NextNodeId.Read(choice)));
                if (request.Conditions) item.Set("conditions", ChoiceConditionArray(choice.ChoiceCondition));

                choices.Add(item);
            }

            return choices;
        }

        private static EndsMcpJsonED EdgeJson(DialogEntry dialog, HashSet<string> included)
        {
            EndsMcpJsonED edges = EndsMcpJsonED.Arr();

            if (dialog.nodes == null) return edges;

            foreach (DialogNode node in dialog.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId) || !included.Contains(node.nodeId)) continue;

                int nextChoiceIndex = 0;
                List<ChoiceNode> options = null;

                if (NodeSchema.Options.IsVisible(node)) options = NodeSchema.Options.Read(node);

                EndsNodeReferencesED.Visit(NodeSchema, node, reference =>
                {
                    if (reference.Kind == NodeReferenceKind.Node)
                    {
                        string label = null;

                        if (reference.Model is ChoiceNode choice)
                        {
                            // Preserve empty choice outputs in the public protocol for malformed rows awaiting repair.
                            while (nextChoiceIndex < reference.OwnerAddress.Index)
                            {
                                AddEdge(edges, node.nodeId, "choice:" + nextChoiceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null, null, null);
                                nextChoiceIndex++;
                            }

                            nextChoiceIndex++;
                            label = FirstText(choice.Translations);
                        }

                        AddEdge(edges, node.nodeId, reference.Port, reference.Value, label, null, null);
                    }
                    else if (reference.Kind == NodeReferenceKind.Dialog) AddEdge(edges, node.nodeId, "redirect", null, null, reference.Value, reference.Qualifier);
                });

                if (options == null) continue;

                while (nextChoiceIndex < options.Count)
                {
                    AddEdge(edges, node.nodeId, "choice:" + nextChoiceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null, null, null);
                    nextChoiceIndex++;
                }
            }

            return edges;
        }

        private static void AddEdge(EndsMcpJsonED edges, string from, string port, string to, string label, string targetDialog, string targetCategory)
        {
            EndsMcpJsonED edge = EndsMcpJsonED.Obj();
            edge.Set("from", EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(from)));
            edge.Set("port", EndsMcpJsonED.Str(port));
            edge.Set("to", NodeRef(to));

            if (label != null) edge.Set("label", EndsMcpJsonED.Str(label));
            if (targetDialog != null) edge.Set("targetDialog", EndsMcpJsonED.Str(targetDialog));
            if (targetCategory != null) edge.Set("targetCategory", EndsMcpJsonED.Str(targetCategory));

            edges.Add(edge);
        }

        #endregion SLICE

        #region CATALOG

        private static EndsMcpJsonED CatalogDialogs(DialogDatabase database)
        {
            EndsMcpJsonED dialogs = EndsMcpJsonED.Arr();

            if (database.Dialogs == null) return dialogs;

            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry dialog = database.Dialogs[i];

                if (dialog == null) continue;

                int lines = 0;
                int choices = 0;
                int choiceOptions = 0;
                int conditions = 0;
                int redirects = 0;

                if (dialog.nodes != null)
                {
                    for (int n = 0; n < dialog.nodes.Count; n++)
                    {
                        DialogNode node = dialog.nodes[n];

                        if (node == null) continue;
                        if (node.NodeType == NodeType.Line) lines++;
                        if (node.NodeType == NodeType.Choice)
                        {
                            choices++;
                            if (node.ChoiceData != null) choiceOptions += node.ChoiceData.Count;
                        }
                        if (node.NodeType == NodeType.Condition) conditions++;
                        if (node.NodeType == NodeType.Redirect) redirects++;
                    }
                }

                EndsMcpJsonED item = EndsMcpJsonED.Obj();

                if (dialog.dialogName == null) item.Set("name", EndsMcpJsonED.Str(""));
                else item.Set("name", EndsMcpJsonED.Str(dialog.dialogName));

                if (dialog.initiator == null) item.Set("category", EndsMcpJsonED.Str(""));
                else item.Set("category", EndsMcpJsonED.Str(dialog.initiator));

                item.Set("locked", EndsMcpJsonED.Boolean(dialog.isLocked));
                item.Set("lines", EndsMcpJsonED.Num(lines));
                item.Set("choices", EndsMcpJsonED.Num(choices));
                item.Set("choiceOptions", EndsMcpJsonED.Num(choiceOptions));
                item.Set("conditions", EndsMcpJsonED.Num(conditions));
                item.Set("redirects", EndsMcpJsonED.Num(redirects));
                dialogs.Add(item);
            }

            return dialogs;
        }

        private static EndsMcpJsonED CatalogCharacters(DialogDatabase database)
        {
            EndsMcpJsonED characters = EndsMcpJsonED.Arr();

            if (database.CharacterGroups == null) return characters;

            for (int i = 0; i < database.CharacterGroups.Count; i++)
            {
                CharacterGroup group = database.CharacterGroups[i];

                if (group == null) continue;

                EndsMcpJsonED sprites = EndsMcpJsonED.Arr();

                if (group.sprites != null)
                {
                    for (int s = 0; s < group.sprites.Count; s++)
                    {
                        CharacterSprite sprite = group.sprites[s];

                        if (sprite == null || string.IsNullOrEmpty(sprite.spriteName)) continue;

                        sprites.Add(EndsMcpJsonED.Str(sprite.spriteName));
                    }
                }

                EndsMcpJsonED item = EndsMcpJsonED.Obj();

                if (group.characterName == null) item.Set("name", EndsMcpJsonED.Str(""));
                else item.Set("name", EndsMcpJsonED.Str(group.characterName));

                item.Set("sprites", sprites);
                characters.Add(item);
            }

            return characters;
        }

        private static EndsMcpJsonED CategoryArray(DialogDatabase database)
        {
            List<string> categories = new List<string>();

            if (database.Categories != null)
            {
                for (int i = 0; i < database.Categories.Count; i++)
                {
                    if (!string.IsNullOrEmpty(database.Categories[i])) categories.Add(database.Categories[i]);
                }
            }

            return StringArray(categories);
        }

        private static EndsMcpJsonED CatalogVariables(DialogDatabase database)
        {
            EndsMcpJsonED list = EndsMcpJsonED.Arr();

            if (database.Variables == null) return list;

            for (int i = 0; i < database.Variables.Count; i++)
            {
                VariableDefinition def = database.Variables[i];

                if (def == null) continue;

                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                item.Set("name", EndsMcpJsonED.Str(def.variableName ?? ""));
                item.Set("type", EndsMcpJsonED.Str(VariableTypeName(def.variableType)));
                list.Add(item);
            }

            return list;
        }

        private static EndsMcpJsonED CatalogNames(DialogDatabase database)
        {
            EndsMcpJsonED list = EndsMcpJsonED.Arr();

            if (database.CharacterNameColors == null) return list;

            for (int i = 0; i < database.CharacterNameColors.Count; i++)
            {
                CharacterNameColor color = database.CharacterNameColors[i];

                if (color == null) continue;

                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                item.Set("name", EndsMcpJsonED.Str(color.Name ?? ""));
                item.Set("color", EndsMcpJsonED.Str("#" + ColorUtility.ToHtmlStringRGBA(color.Color)));
                list.Add(item);
            }

            return list;
        }

        #endregion CATALOG

        #region WRITE

        private static void CollectSlotDetails(EndsMcpJsonED args, EndsMcpJsonED commands)
        {
            foreach (EndsMcpJsonED patch in args.Get("patches").Items)
            {
                CollectDetailCommands(patch.Get("key").Text, patch.Get("sayers"), true, commands);
                CollectDetailCommands(patch.Get("key").Text, patch.Get("voices"), false, commands);
            }
        }

        private static void CollectDetailCommands(string key, EndsMcpJsonED map, bool sayer, EndsMcpJsonED commands)
        {
            if (map == null) return;
            if (map.Type != EndsMcpJsonED.Kind.Object || map.Keys == null) throw new InvalidOperationException("Slot details must be an object.");

            for (int i = 0; i < map.Keys.Count; i++)
            {
                string operation = "localization.voice";
                string field = "asset";

                if (sayer)
                {
                    operation = "localization.sayer";
                    field = "text";
                }

                EndsMcpJsonED command = EndsDialogGraphContractED.Command(operation);
                command.Set("key", EndsMcpJsonED.Str(key));
                command.Set("language", EndsMcpJsonED.Str(map.Keys[i]));
                command.Set(field, EndsMcpJsonED.Str(EndsDialogGraphContractED.String(map.Values[i])));
                commands.Add(command);
            }
        }

        private static bool TryCollectPatches(DialogDatabase database, EndsMcpJsonED args, out List<TextPatch> patches, out string error)
        {
            patches = new List<TextPatch>();
            error = null;
            EndsMcpJsonED list = null;

            if (args != null) list = args.Get("patches");

            if (list == null || list.Type != EndsMcpJsonED.Kind.Array || list.Items == null || list.Items.Count == 0)
            {
                error = "patches must be a non-empty array.";
                return false;
            }

            List<string> languages = AvailableLanguages(database);

            for (int i = 0; i < list.Items.Count; i++)
            {
                EndsMcpJsonED patch = list.Items[i];

                if (patch == null || patch.Type != EndsMcpJsonED.Kind.Object)
                {
                    error = "Each patch must be an object.";
                    return false;
                }

                EndsMcpJsonED keyNode = patch.Get("key");

                if (keyNode == null || keyNode.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(keyNode.Text))
                {
                    error = "Patch key is missing.";
                    return false;
                }

                if (!EndsDialogLocalizationCsvED.ContainsTextKey(database, keyNode.Text))
                {
                    error = "Unknown or malformed key " + keyNode.Text + ".";
                    return false;
                }

                int before = patches.Count;

                if (!TakePatchFields(patch, keyNode.Text, languages, patches, out error)) return false;

                bool hasDetail = patch.Get("sayers") != null || patch.Get("voices") != null;

                if (patches.Count == before && !hasDetail)
                {
                    error = "Patch " + keyNode.Text + " has no language text.";
                    return false;
                }
            }

            return true;
        }

        private static bool TakePatchFields(EndsMcpJsonED patch, string key, List<string> languages, List<TextPatch> patches, out string error)
        {
            error = null;
            EndsMcpJsonED texts = patch.Get("texts");

            if (texts != null && texts.Type != EndsMcpJsonED.Kind.Object)
            {
                error = "texts must be an object.";
                return false;
            }

            if (texts != null && texts.Keys != null)
            {
                for (int i = 0; i < texts.Keys.Count; i++)
                {
                    if (!TakeOne(key, texts.Keys[i], texts.Values[i], languages, patches, out error)) return false;
                }
            }

            if (patch.Keys == null) return true;

            for (int i = 0; i < patch.Keys.Count; i++)
            {
                string field = patch.Keys[i];

                if (field == "key" || field == "texts" || field == "sayers" || field == "voices") continue;
                if (!TakeOne(key, field, patch.Values[i], languages, patches, out error)) return false;
            }

            return true;
        }

        private static bool TakeOne(string key, string language, EndsMcpJsonED value, List<string> languages, List<TextPatch> patches, out string error)
        {
            error = null;

            if (value == null || value.Type != EndsMcpJsonED.Kind.String)
            {
                error = "Text for " + language + " must be a string.";
                return false;
            }

            if (!HasLanguage(languages, language))
            {
                error = "Unknown language " + language + ".";
                return false;
            }

            for (int i = 0; i < patches.Count; i++)
            {
                if (patches[i].Key != key || patches[i].Language != language) continue;

                error = "Duplicate patch for " + key + " " + language + ".";
                return false;
            }

            TextPatch patch = new TextPatch();
            patch.Key = key;
            patch.Language = language;
            patch.Text = value.Text;
            patches.Add(patch);
            return true;
        }

        #endregion WRITE

        #region SCHEMA

        private static EndsMcpJsonED Tool(string name, string description, EndsMcpJsonED properties)
        {
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            EndsMcpJsonED tool = EndsMcpJsonED.Obj();
            tool.Set("name", EndsMcpJsonED.Str(name));
            tool.Set("description", EndsMcpJsonED.Str(description));
            tool.Set("inputSchema", schema);
            return tool;
        }

        private static EndsMcpJsonED ReadSchema()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("database", StringProperty("Existing Dialog Database asset path. Omit for the current database."));
            properties.Set("dialog", StringProperty("Dialog name in the database."));
            properties.Set("find", StringProperty("Case-insensitive phrase. Limits the slice to matching nodes, or to the whole dialog when its name matches."));
            properties.Set("languages", LanguagesProperty());
            properties.Set("text", BoolProperty("Include line and choice text. Default true."));
            properties.Set("branches", BoolProperty("Include edges: next, choice:N, true, false, redirect. Default false."));
            properties.Set("animations", BoolProperty("Include speaker and show or hide actions. Default false."));
            properties.Set("conditions", BoolProperty("Include start, choice, and branch conditions. Default false."));
            properties.Set("voice", BoolProperty("Include voice clip asset paths. Default false."));
            properties.Set("layout", BoolProperty("Include node positions. Default false."));
            properties.Set("focus", StringProperty("One nodeId. Returns that node and, with branches, only its outgoing edges."));
            return properties;
        }

        private static EndsMcpJsonED WriteSchema()
        {
            EndsMcpJsonED patchProperties = EndsMcpJsonED.Obj();
            patchProperties.Set("key", StringProperty("Literal dialog name and full node id separated by dots, plus .line or .{optionId}, for example Test.LNN.5.line or Test.CHN.2.OPT.3. Dialog names cannot contain dots."));
            patchProperties.Set("texts", StringMapProperty("Language code to line or choice text."));
            patchProperties.Set("sayers", StringMapProperty("Line only. Language code to speaker display name."));
            patchProperties.Set("voices", StringMapProperty("Line only. Language code to an AudioClip asset path. Empty clears the clip."));
            EndsMcpJsonED patch = EndsMcpJsonED.Obj();
            patch.Set("type", EndsMcpJsonED.Str("object"));
            patch.Set("properties", patchProperties);
            patch.Set("additionalProperties", StringProperty("Line or choice text for this language code."));
            EndsMcpJsonED items = EndsMcpJsonED.Obj();
            items.Set("type", EndsMcpJsonED.Str("array"));
            items.Set("items", patch);
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            properties.Set("patches", items);
            properties.Set("database", StringProperty("Existing Dialog Database asset path."));
            EndsMcpJsonED revision = EndsMcpJsonED.Obj();
            revision.Set("type", EndsMcpJsonED.Str("integer"));
            revision.Set("minimum", EndsMcpJsonED.Num(0));
            properties.Set("expectedRevision", revision);
            properties.Set("save", BoolProperty("Save working edits after applying the batch."));
            return properties;
        }

        private static EndsMcpJsonED StringProperty(string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("type", EndsMcpJsonED.Str("string"));
            property.Set("description", EndsMcpJsonED.Str(description));

            return property;
        }

        private static EndsMcpJsonED StringMapProperty(string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("type", EndsMcpJsonED.Str("object"));
            property.Set("description", EndsMcpJsonED.Str(description));
            property.Set("additionalProperties", StringProperty("Value for this language code."));

            return property;
        }

        private static EndsMcpJsonED LanguagesProperty()
        {
            EndsMcpJsonED asString = EndsMcpJsonED.Obj();
            asString.Set("type", EndsMcpJsonED.Str("string"));
            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("type", EndsMcpJsonED.Str("string"));
            EndsMcpJsonED asArray = EndsMcpJsonED.Obj();
            asArray.Set("type", EndsMcpJsonED.Str("array"));
            asArray.Set("items", item);
            EndsMcpJsonED oneOf = EndsMcpJsonED.Arr();
            oneOf.Add(asString);
            oneOf.Add(asArray);
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("description", EndsMcpJsonED.Str("all, or an array of language codes. Default all."));
            property.Set("oneOf", oneOf);

            return property;
        }

        private static EndsMcpJsonED BoolProperty(string description)
        {
            EndsMcpJsonED property = EndsMcpJsonED.Obj();
            property.Set("type", EndsMcpJsonED.Str("boolean"));
            property.Set("description", EndsMcpJsonED.Str(description));

            return property;
        }

        #endregion SCHEMA

        #region ARGUMENTS

        private static bool TryOneDialog(DialogDatabase database, string dialogName, out DialogEntry dialog, out string error)
        {
            dialog = null;
            error = null;
            int count = 0;

            if (database.Dialogs != null)
            {
                for (int i = 0; i < database.Dialogs.Count; i++)
                {
                    DialogEntry entry = database.Dialogs[i];

                    if (entry == null || entry.dialogName != dialogName) continue;

                    count++;
                    dialog = entry;
                }
            }

            if (count == 1) return true;

            dialog = null;

            if (count == 0) error = "Dialog not found.";
            else error = "More than one dialog is named " + dialogName + ".";

            return false;
        }

        private static bool TryFlags(EndsMcpJsonED args, out ReadRequest request, out string error)
        {
            request = new ReadRequest();
            request.Find = OptionalString(args, "find");
            request.Focus = OptionalString(args, "focus");
            error = null;
            bool text;
            bool branches;
            bool animations;
            bool conditions;
            bool voice;
            bool layout;

            if (!TryFlag(args, "text", true, out text, out error)) return false;
            if (!TryFlag(args, "branches", false, out branches, out error)) return false;
            if (!TryFlag(args, "animations", false, out animations, out error)) return false;
            if (!TryFlag(args, "conditions", false, out conditions, out error)) return false;
            if (!TryFlag(args, "voice", false, out voice, out error)) return false;
            if (!TryFlag(args, "layout", false, out layout, out error)) return false;

            request.Text = text;
            request.Branches = branches;
            request.Animations = animations;
            request.Conditions = conditions;
            request.Voice = voice;
            request.Layout = layout;

            return true;
        }

        private static bool TryFlag(EndsMcpJsonED args, string key, bool fallback, out bool value, out string error)
        {
            value = fallback;
            error = null;
            EndsMcpJsonED node = null;

            if (args != null) node = args.Get(key);
            if (node == null || node.Type == EndsMcpJsonED.Kind.Null) return true;
            if (node.Type != EndsMcpJsonED.Kind.Bool)
            {
                error = key + " must be a boolean.";

                return false;
            }

            value = node.Bool;

            return true;
        }

        private static bool TryLanguages(DialogDatabase database, EndsMcpJsonED args, out List<string> languages, out string error)
        {
            languages = new List<string>();
            error = null;
            List<string> available = AvailableLanguages(database);
            EndsMcpJsonED node = null;

            if (args != null) node = args.Get("languages");

            if (node == null || node.Type == EndsMcpJsonED.Kind.Null || (node.Type == EndsMcpJsonED.Kind.String && node.Text == "all"))
            {
                for (int i = 0; i < available.Count; i++) languages.Add(available[i]);

                return true;
            }

            if (node.Type == EndsMcpJsonED.Kind.String)
            {
                error = "languages must be all or an array of language codes.";

                return false;
            }

            if (node.Type != EndsMcpJsonED.Kind.Array || node.Items == null)
            {
                error = "languages must be all or an array of language codes.";

                return false;
            }

            for (int i = 0; i < node.Items.Count; i++)
            {
                EndsMcpJsonED item = node.Items[i];

                if (item == null || item.Type != EndsMcpJsonED.Kind.String || string.IsNullOrEmpty(item.Text))
                {
                    error = "Language code is empty.";

                    return false;
                }

                if (!HasLanguage(available, item.Text))
                {
                    error = "Unknown language " + item.Text + ".";
                    
                    return false;
                }

                if (HasLanguage(languages, item.Text)) continue;

                languages.Add(item.Text);
            }

            return true;
        }

        private static string OptionalString(EndsMcpJsonED args, string key)
        {
            if (args == null) return "";

            EndsMcpJsonED node = args.Get(key);

            if (node == null || node.Type != EndsMcpJsonED.Kind.String || node.Text == null) return "";

            return node.Text;
        }

        #endregion ARGUMENTS

        #region TEXT AND NODES

        private static List<string> AvailableLanguages(DialogDatabase database)
        {
            List<string> languages = new List<string>();

            if (database.AvailableLanguages == null) return languages;

            for (int i = 0; i < database.AvailableLanguages.Count; i++)
            {
                string language = database.AvailableLanguages[i];

                if (string.IsNullOrEmpty(language)) continue;
                if (HasLanguage(languages, language)) continue;

                languages.Add(language);
            }

            return languages;
        }

        private static bool HasLanguage(List<string> languages, string language)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == language) return true;
            }

            return false;
        }

        private static bool DialogHits(DialogEntry dialog, string find)
        {
            if (dialog.dialogName != null && dialog.dialogName.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (dialog.nodes == null) return false;

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                if (NodeHits(dialog.nodes[i], find)) return true;
            }

            return false;
        }

        private static bool NodeHits(DialogNode node, string find)
        {
            if (node == null) return false;
            if (node.NodeType == NodeType.Line && node.LineData != null && TextsContain(node.LineData.DialogsText, find)) return true;
            if (node.NodeType != NodeType.Choice || node.ChoiceData == null) return false;

            for (int i = 0; i < node.ChoiceData.Count; i++)
            {
                ChoiceNode choice = node.ChoiceData[i];

                if (choice == null) continue;
                if (TextsContain(choice.Translations, find)) return true;
            }

            return false;
        }

        private static bool TextsContain(IReadOnlyList<IDialogText> texts, string phrase)
        {
            if (texts == null) return false;

            for (int i = 0; i < texts.Count; i++)
            {
                IDialogText entry = texts[i];

                if (entry == null || string.IsNullOrEmpty(entry.Text)) continue;
                if (entry.Text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private static EndsMcpJsonED TextObject(IReadOnlyList<IDialogText> texts, List<string> languages)
        {
            EndsMcpJsonED obj = EndsMcpJsonED.Obj();

            for (int i = 0; i < languages.Count; i++)
            {
                string language = languages[i];
                string value = "";

                if (texts != null)
                {
                    for (int t = 0; t < texts.Count; t++)
                    {
                        IDialogText entry = texts[t];

                        if (entry == null || entry.LanguageCode != language) continue;

                        if (entry.Text != null) value = entry.Text;
                        break;
                    }
                }

                obj.Set(language, EndsMcpJsonED.Str(value));
            }

            return obj;
        }

        private static string FirstText(IReadOnlyList<IDialogText> texts)
        {
            if (texts == null) return "";

            for (int i = 0; i < texts.Count; i++)
            {
                IDialogText entry = texts[i];

                if (entry == null || string.IsNullOrEmpty(entry.Text)) continue;

                return entry.Text;
            }

            return "";
        }

        private static EndsMcpJsonED ActionArray(List<CharacterActionData> actions)
        {
            EndsMcpJsonED list = EndsMcpJsonED.Arr();

            if (actions == null) return list;

            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionData action = actions[i];

                if (action == null) continue;

                string actionName = "show";

                if (action.Action == CharacterAction.Hide) actionName = "hide";

                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                if (action.CharacterName == null) item.Set("character", EndsMcpJsonED.Str(""));
                else item.Set("character", EndsMcpJsonED.Str(action.CharacterName));
                if (action.EmotionName == null) item.Set("emotion", EndsMcpJsonED.Str(""));
                else item.Set("emotion", EndsMcpJsonED.Str(action.EmotionName));
                item.Set("action", EndsMcpJsonED.Str(actionName));
                list.Add(item);
            }

            return list;
        }

        private static EndsMcpJsonED NodeRef(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return EndsMcpJsonED.Null();

            return EndsMcpJsonED.Str(EndsNodeRefED.ShortRef(nodeId));
        }

        private static DialogNode FindNode(DialogEntry dialog, string id)
        {
            if (EndsNodeRefED.TryResolveInDialog(dialog, id, out DialogNode node, out _)) return node;

            return null;
        }

        private static EndsMcpJsonED VoiceObject(List<LineText> texts, List<string> languages)
        {
            EndsMcpJsonED obj = EndsMcpJsonED.Obj();

            for (int i = 0; i < languages.Count; i++)
            {
                string language = languages[i];
                string path = "";

                if (texts != null)
                {
                    for (int t = 0; t < texts.Count; t++)
                    {
                        LineText entry = texts[t];

                        if (entry == null || entry.LanguageCode != language) continue;
                        if (entry.VoiceClip != null) path = AssetDatabase.GetAssetPath(entry.VoiceClip);

                        break;
                    }
                }

                obj.Set(language, EndsMcpJsonED.Str(path));
            }

            return obj;
        }

        private static EndsMcpJsonED ConditionArray(List<DialogCondition> conditions)
        {
            EndsMcpJsonED list = EndsMcpJsonED.Arr();

            if (conditions == null) return list;

            for (int i = 0; i < conditions.Count; i++)
            {
                DialogCondition condition = conditions[i];

                if (condition == null) continue;

                list.Add(ConditionItem(condition.VariableName, condition.VariableType, condition.ConditionOperator, condition.BoolValue, condition.IntValue, condition.FloatValue, condition.StringValue));
            }

            return list;
        }

        private static EndsMcpJsonED ChoiceConditionArray(List<ChoiceCondition> conditions)
        {
            EndsMcpJsonED list = EndsMcpJsonED.Arr();

            if (conditions == null) return list;

            for (int i = 0; i < conditions.Count; i++)
            {
                ChoiceCondition condition = conditions[i];

                if (condition == null) continue;

                list.Add(ConditionItem(condition.VariableName, condition.VariableType, condition.ConditionOperator, condition.BoolValue, condition.IntValue, condition.FloatValue, condition.StringValue));
            }

            return list;
        }

        private static EndsMcpJsonED ConditionItem(string variable, ConditionVariableType type, ConditionOperator op, bool boolValue, int intValue, float floatValue, string stringValue)
        {
            EndsMcpJsonED item = EndsMcpJsonED.Obj();
            item.Set("variable", EndsMcpJsonED.Str(variable ?? ""));
            item.Set("type", EndsMcpJsonED.Str(VariableTypeName(type)));
            item.Set("op", EndsMcpJsonED.Str(OperatorName(op)));

            if (type == ConditionVariableType.Bool) item.Set("value", EndsMcpJsonED.Boolean(boolValue));
            else if (type == ConditionVariableType.Int) item.Set("value", EndsMcpJsonED.Num(intValue));
            else if (type == ConditionVariableType.Float) item.Set("value", EndsMcpJsonED.Num(floatValue));
            else item.Set("value", EndsMcpJsonED.Str(stringValue ?? ""));

            return item;
        }

        private static string VariableTypeName(ConditionVariableType type)
        {
            if (type == ConditionVariableType.Bool) return "bool";
            if (type == ConditionVariableType.Int) return "int";
            if (type == ConditionVariableType.Float) return "float";
            if (type == ConditionVariableType.String) return "string";

            return "string";
        }

        private static string OperatorName(ConditionOperator op)
        {
            if (op == ConditionOperator.Equals) return "equals";
            if (op == ConditionOperator.NotEquals) return "notEquals";
            if (op == ConditionOperator.GreaterThan) return "greater";
            if (op == ConditionOperator.LessThan) return "less";
            if (op == ConditionOperator.GreaterThanOrEquals) return "greaterOrEquals";
            if (op == ConditionOperator.LessThanOrEquals) return "lessOrEquals";

            return "equals";
        }

        private static string TypeName(NodeType type)
        {
            if (type == NodeType.Start) return "start";
            if (type == NodeType.Line) return "line";
            if (type == NodeType.Choice) return "choice";
            if (type == NodeType.Condition) return "condition";
            if (type == NodeType.Redirect) return "redirect";

            return "unknown";
        }

        private static EndsMcpJsonED StringArray(List<string> values)
        {
            EndsMcpJsonED array = EndsMcpJsonED.Arr();

            for (int i = 0; i < values.Count; i++) array.Add(EndsMcpJsonED.Str(values[i]));

            return array;
        }

        #endregion TEXT AND NODES
    }
}
#endif