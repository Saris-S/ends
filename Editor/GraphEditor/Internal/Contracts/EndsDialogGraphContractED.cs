#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region FIELDS

        internal const int CONTRACT_VERSION = 15;

        private static readonly IReadOnlyDictionary<Type, EndsDialogRulesED.ModelField[]> _models = EndsDialogRulesED.Models;
        private static bool _coverageChecked;

        internal static IEnumerable<Type> ModelTypes => _models.Keys;

        private sealed class Operation
        {
            internal string Name;
            internal string Description;
            internal OperationArgument[] Arguments;
            internal Action<DialogDatabase, EndsMcpJsonED, EndsMcpJsonED> Apply;
            internal bool DialogOnly;
        }

        private static readonly Dictionary<string, Operation> _operations = BuildOperations();

        #endregion FIELDS

        #region OPERATION REGISTRATION

        private static Dictionary<string, Operation> BuildOperations()
        {
            Dictionary<string, Operation> operations = new Dictionary<string, Operation>(StringComparer.Ordinal);

            RegisterDialogOperation(operations, "node.add", "Creates a node with one prepared id.", ApplyNodeAdd, "dialog:string", "type:NodeType", "id:string?", "position:Vector2?", "patch:DialogNode?");
            RegisterDialogOperation(operations, "node.remove", "Deletes a node and clears its incoming links.", ApplyNodeRemove, "dialog:string", "node:string");
            RegisterDialogOperation(operations, "node.duplicate", "Copies a node and clears outgoing links.", ApplyNodeDuplicate, "dialog:string", "node:string", "id:string?", "offset:Vector2?");
            RegisterDialogOperation(operations, "node.patch", "Changes registered node fields, including nested content.", ApplyNodePatch, "dialog:string", "node:string", "patch:DialogNode");
            RegisterDialogOperation(operations, "edge.set", "Changes one outgoing port.", ApplyEdgeSet, "dialog:string", "node:string", "port:string", "to:string?", "category:string?", "targetDialog:string?");
            RegisterDialogOperation(operations, "edge.disconnectInput", "Clears incoming links of one node.", ApplyDisconnectInput, "dialog:string", "node:string");
            RegisterOperation(operations, "dialog.add", "Creates a dialog and a Start node.", ApplyDialogAdd, "name:string", "category:string?", "id:string?", "position:Vector2?");
            RegisterOperation(operations, "dialog.remove", "Deletes a dialog and marks redirect references missing.", ApplyDialogRemove, "dialog:string");
            RegisterOperation(operations, "dialog.rename", "Renames a dialog and updates redirect references.", ApplyDialogRename, "dialog:string", "newName:string");
            RegisterOperation(operations, "dialog.duplicate", "Copies a dialog with its local ids, counters, and links. An omitted name receives a free (N) suffix.", ApplyDialogDuplicate, "dialog:string", "newName:string?", "ids:array?");
            RegisterOperation(operations, "dialog.patch", "Changes category, lock, and start conditions.", ApplyDialogPatch, "dialog:string", "patch:DialogEntry");
            RegisterOperation(operations, "dialog.move", "Moves a dialog in initiator evaluation order.", ApplyDialogMove, "dialog:string", "index:integer");
            RegisterDialogOperation(operations, "choice.add", "Appends a choice option.", ApplyChoiceAdd, "dialog:string", "node:string", "patch:ChoiceNode?");
            RegisterDialogOperation(operations, "choice.remove", "Removes a choice option.", ApplyChoiceRemove, "dialog:string", "node:string", "index:integer");
            RegisterDialogOperation(operations, "choice.patch", "Changes one choice option.", ApplyChoicePatch, "dialog:string", "node:string", "index:integer", "patch:ChoiceNode");
            RegisterDialogOperation(operations, "action.add", "Appends a character action.", ApplyActionAdd, "dialog:string", "node:string", "patch:CharacterActionData?");
            RegisterDialogOperation(operations, "action.remove", "Removes a character action.", ApplyActionRemove, "dialog:string", "node:string", "index:integer");
            RegisterDialogOperation(operations, "action.patch", "Changes a character action.", ApplyActionPatch, "dialog:string", "node:string", "index:integer", "patch:CharacterActionData");
            RegisterDialogOperation(operations, "action.copy", "Copies character action parameters.", ApplyActionCopy, "dialog:string", "node:string", "index:integer", "sourceNode:string", "sourceIndex:integer");
            RegisterOperation(operations, "category.add", "Adds a category.", ApplyCategoryAdd, "name:string");
            RegisterOperation(operations, "category.rename", "Renames a category and its references.", ApplyCategoryRename, "name:string", "newName:string");
            RegisterOperation(operations, "category.remove", "Moves dialogs into the default category.", ApplyCategoryRemove, "name:string");
            RegisterOperation(operations, "language.add", "Adds a language.", ApplyLanguageAdd, "name:string");
            RegisterOperation(operations, "language.rename", "Renames a language and its translations.", ApplyLanguageRename, "name:string", "newName:string");
            RegisterOperation(operations, "language.remove", "Removes a language and its translations.", ApplyLanguageRemove, "name:string");
            RegisterOperation(operations, "language.deleteTexts", "Removes translations for an orphan language.", ApplyLanguageDeleteTexts, "name:string");
            RegisterOperation(operations, "name.add", "Adds a trimmed display name and color, unique generates an available name.", ApplyNameAdd, "name:string", "patch:CharacterNameColor?", "unique:boolean?");
            RegisterOperation(operations, "name.rename", "Renames a display name and line references.", ApplyNameRename, "name:string", "newName:string");
            RegisterOperation(operations, "name.remove", "Removes a display name and marks references missing.", ApplyNameRemove, "name:string");
            RegisterOperation(operations, "name.patch", "Changes a display name color and translations.", ApplyNamePatch, "name:string", "patch:CharacterNameColor");
            RegisterOperation(operations, "character.add", "Adds a character and optionally copies sprite slot names.", ApplyCharacterAdd, "name:string", "copyFrom:string?", "patch:CharacterGroup?");
            RegisterOperation(operations, "character.rename", "Renames a character and action references.", ApplyCharacterRename, "name:string", "newName:string");
            RegisterOperation(operations, "character.remove", "Removes a character and marks references missing.", ApplyCharacterRemove, "name:string");
            RegisterOperation(operations, "sprite.add", "Adds a sprite slot.", ApplySpriteAdd, "name:string", "slot:string", "patch:CharacterSprite?");
            RegisterOperation(operations, "sprite.rename", "Renames a sprite slot and action references.", ApplySpriteRename, "name:string", "slot:string", "newName:string");
            RegisterOperation(operations, "sprite.remove", "Removes a sprite slot and marks action references missing.", ApplySpriteRemove, "name:string", "slot:string");
            RegisterOperation(operations, "sprite.patch", "Assigns or clears a sprite asset.", ApplySpritePatch, "name:string", "slot:string", "patch:CharacterSprite");
            RegisterOperation(operations, "variable.add", "Adds a variable definition.", ApplyVariableAdd, "name:string", "patch:VariableDefinition?");
            RegisterOperation(operations, "variable.rename", "Renames a variable and condition references.", ApplyVariableRename, "name:string", "newName:string");
            RegisterOperation(operations, "variable.remove", "Removes a variable and marks references missing.", ApplyVariableRemove, "name:string");
            RegisterOperation(operations, "variable.patch", "Changes variable type and default values.", ApplyVariablePatch, "name:string", "patch:VariableDefinition");
            RegisterOperation(operations, "database.patch", "Changes current language and voice clip availability.", ApplyDatabasePatch, "patch:DialogDatabase");
            RegisterOperation(operations, "database.initialize", "Initializes missing database collections through the common session.", ApplyDatabaseInitialize);
            RegisterOperation(operations, "database.voice", "Enables voice slots or clears clips and disables them.", ApplyDatabaseVoice, "enabled:boolean", "clearClips:boolean?");
            RegisterOperation(operations, "graph.validate", "Reads graph integrity, reference issues, and start condition conflicts without changing data.", ApplyGraphValidate, "dialog:string?");
            RegisterDialogOperation(operations, "graph.layout", "Arranges an existing dialog from left to right, following ports and spacing fully expanded nodes.", ApplyGraphLayout, "dialog:string");
            RegisterDialogOperation(operations, "graph.repair", "Repairs dangling edges and invalid redirect targets.", ApplyGraphRepair, "dialog:string");
            RegisterOperation(operations, "localization.text", "Changes one translated line or choice text.", ApplyLocalizationText, "key:string", "language:string", "text:string");
            RegisterOperation(operations, "localization.sayer", "Changes a translated line speaker label.", ApplyLocalizationSayer, "key:string", "language:string", "text:string");
            RegisterOperation(operations, "localization.voice", "Changes a line voice clip asset.", ApplyLocalizationVoice, "key:string", "language:string", "asset:string");
            RegisterOperation(operations, "localization.csv", "Imports localization into the working session.", ApplyLocalizationCsv, "csv:string", "addLanguages:boolean?");

            return operations;
        }

        private static Operation RegisterOperation(Dictionary<string, Operation> operations, string name, string description, Action<DialogDatabase, EndsMcpJsonED, EndsMcpJsonED> apply, params string[] arguments)
        {
            OperationArgument[] metadata = new OperationArgument[arguments.Length];

            for (int i = 0; i < arguments.Length; i++)
            {
                metadata[i] = DescribeArgument(name, arguments[i]);
            }

            Operation operation = new Operation { Name = name, Description = description, Arguments = metadata, Apply = apply };
            operations.Add(name, operation);

            return operation;
        }

        private static void RegisterDialogOperation(Dictionary<string, Operation> operations, string name, string description, Action<DialogDatabase, EndsMcpJsonED, EndsMcpJsonED> apply, params string[] arguments)
        {
            RegisterOperation(operations, name, description, apply, arguments).DialogOnly = true;
        }

        #endregion OPERATION REGISTRATION

        #region EXECUTION

        internal static EndsMcpJsonED Command(string operation)
        {
            if (!_operations.ContainsKey(operation)) throw new InvalidOperationException("Unregistered editor operation: " + operation);

            EndsMcpJsonED command = EndsMcpJsonED.Obj();
            command.Set("op", EndsMcpJsonED.Str(operation));

            return command;
        }

        internal static EndsMcpJsonED NodeCommand(string operation, string dialog, string node)
        {
            EndsMcpJsonED command = Command(operation);
            command.Set("dialog", EndsMcpJsonED.Str(dialog));
            command.Set("node", EndsMcpJsonED.Str(node));

            return command;
        }

        internal static EndsSessionED.Result Execute(DialogDatabase database, EndsMcpJsonED command, int? expectedRevision = null, bool save = false)
        {
            EndsMcpJsonED commands = EndsMcpJsonED.Arr();
            commands.Add(command);

            return ExecuteBatch(database, commands, expectedRevision, save, out _);
        }

        internal static EndsSessionED.Result ExecuteBatch(DialogDatabase database, EndsMcpJsonED commands, int? expectedRevision, bool save, out EndsMcpJsonED output, bool solvePlacement = false)
        {
            EndsMcpJsonED prepared = null;
            List<string> changes = new List<string>();
            EndsMcpJsonED responses = EndsMcpJsonED.Arr();
            EndsMcpJsonED placement = EndsMcpJsonED.Arr();
            List<EndsVariableRuntimeED.DefinitionEdit> variableEdits = new List<EndsVariableRuntimeED.DefinitionEdit>();

            EndsSessionED session = EndsSessionED.ForDatabase(database);
            HashSet<string> dialogScopes = ReadOperationScopes(session.WorkingDatabase, commands);
            EndsSessionED.Result result = session.Execute(staged =>
            {
                RequireArray(commands);
                EnsureCoverage();
                prepared = EndsMcpJsonED.Parse(commands.Write());
                var graphPlacement = new GraphPlacement();

                for (int i = 0; i < prepared.Items.Count; i++)
                {
                    EndsMcpJsonED command = prepared.Items[i];
                    string name = "";

                    try
                    {
                        RequireObject(command);
                        name = String(command.Get("op"));

                        if (!_operations.TryGetValue(name, out Operation operation)) throw new InvalidOperationException("Unregistered editor operation: " + name);

                        ValidateArguments(operation, command, i);

                        DialogEntry placementDialog = null;

                        if (solvePlacement)
                        {
                            placementDialog = GraphPlacement.FindAffectedDialog(staged, name, command);

                            if (placementDialog != null) graphPlacement.Capture(staged, placementDialog);
                        }

                        EndsMcpJsonED response = EndsMcpJsonED.Obj();
                        operation.Apply(staged, command, response);

                        if (placementDialog != null) graphPlacement.Record(placementDialog, name, command, response);
                        if (solvePlacement && name == "graph.layout") graphPlacement.Forget(FindDialog(staged, command));

                        if (name == "variable.add" || name == "variable.remove" || name == "variable.rename")
                        {
                            string variableName = String(command.Get("name"));
                            string newName = name == "variable.rename" ? EndsDialogRulesED.NormalizeVariableName(String(command.Get("newName"))) : null;

                            if (name == "variable.add") variableName = EndsDialogRulesED.NormalizeVariableName(variableName);

                            variableEdits.Add(new EndsVariableRuntimeED.DefinitionEdit { Operation = name, Name = variableName, NewName = newName });
                        }

                        responses.Add(response);
                        changes.Add(ChangePath(command, response));
                    }
                    catch (Exception exception)
                    {
                        if (exception is OperationFailure failure)
                        {
                            failure.Command = command;

                            throw;
                        }

                        throw new OperationFailure(i, name, "operations[" + i + "]", exception) { Command = command };
                    }
                }

                graphPlacement.Resolve(staged, placement);
            }, "ENDS editor operation", expectedRevision, save, changes, variableEdits: variableEdits, dialogScopes: dialogScopes);

            output = EndsMcpJsonED.Obj();
            output.Set("session", EndsSessionED.WriteResult(result));

            if (result.Success || result.Changed) output.Set("results", responses);
            else output.Set("results", EndsMcpJsonED.Arr());

            if ((result.Success || result.Changed) && placement.Items.Count > 0) output.Set("placement", placement);

            return result;
        }

        private static string ChangePath(EndsMcpJsonED command, EndsMcpJsonED response)
        {
            string operation = String(command.Get("op"));
            string dialog = Optional(command, "dialog");
            string node = Optional(command, "node");

            if (response.Get("dialog") != null) dialog = String(response.Get("dialog"));
            if (response.Get("nodeId") != null) node = String(response.Get("nodeId"));
            if (!string.IsNullOrEmpty(node)) return "node/" + dialog + "/" + node;
            if (!string.IsNullOrEmpty(dialog)) return "dialog/" + dialog;

            if (operation.StartsWith("name.", StringComparison.Ordinal) && response.Get("name") != null) return "name/" + String(response.Get("name"));

            return operation.Split('.')[0] + "/" + Optional(command, "name");
        }

        #endregion EXECUTION

        private static HashSet<string> ReadOperationScopes(DialogDatabase database, EndsMcpJsonED commands)
        {
            if (commands?.Type != EndsMcpJsonED.Kind.Array || commands.Items.Count == 0) return null;

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsMcpJsonED command in commands.Items)
            {
                if (command?.Type != EndsMcpJsonED.Kind.Object) return null;

                string name = command.Get("op")?.Text;
                string dialogName = command.Get("dialog")?.Text;

                if (name == null || !_operations.TryGetValue(name, out Operation operation) || !operation.DialogOnly) return null;

                DialogEntry dialog = database.Dialogs?.Find(candidate => candidate != null && candidate.dialogName == dialogName);

                if (dialog == null || string.IsNullOrEmpty(dialog.NodeIdentityKey)) return null;

                keys.Add(dialog.NodeIdentityKey);
            }

            return keys;
        }

        #region COMMAND INPUT

        private static string Optional(EndsMcpJsonED command, string name)
        {
            EndsMcpJsonED value = command.Get(name);

            if (value == null) return "";

            return String(value);
        }

        private static int Index(EndsMcpJsonED command, int count)
        {
            int index = Integer(command.Get("index"));

            if (index < 0 || index >= count) throw new InvalidOperationException("Index is out of range.");

            return index;
        }

        #endregion COMMAND INPUT
    }
}
#endif