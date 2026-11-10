#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsMcpDocumentationED
    {
        #region FIELDS

        private sealed class Topic
        {
            internal readonly string[] Notes;
            internal readonly Type[] Models;

            internal Topic(string[] notes, params Type[] models)
            {
                Notes = notes;
                Models = models;
            }
        }

        private static readonly Dictionary<string, Topic> _topics = new Dictionary<string, Topic>(StringComparer.Ordinal)
        {
            { "ids", new Topic(new[]
            {
                "Node ids are permanent strings: STN for Start, LNN for Line, CHN for Choice, CDN for Condition, RDN for Redirect, followed by a dot and a positive integer.",
                "Each dialog has an increasing counter for each node type. A new dialog starts with STN.1 and its first Line is LNN.1. Numbers are not list positions and are not reused after deletion, Undo, or Discard within that dialog.",
                "Use database, dialog and the exact full node id returned by reading. A missing LNN.1 never resolves to LNN.10. Do not abbreviate ids or infer links from their numbers.",
                "Omit id when creating or duplicating a node. Explicit new ids must match the node type and use a fresh number in that dialog. A complete dialog copy preserves local node ids, links, and counters in a new independent dialog scope. Existing ids are immutable. "
                + "NextNodeNumbers is read-only metadata: Start, Line, Choice, Condition, Redirect, Option, Condition block, Consequence, Animation.",
                "Opening a legacy database plans an exact id replacement map and remaps every stored node edge while retaining node order and content. Ambiguous duplicate or empty legacy ids require repair first.",
                "Migration remains pending until the complete database is saved. External CSV keys and external application references need conversion using the migration map. FormerlySerializedAs cannot convert their values.",
                "Use includeMigrationMap=true with database to obtain replacements grouped by dialog. Form external text keys with the literal dialog name, a dot, the new node id and its text suffix. Dialog names cannot contain dots. "
                + "Repeated blocks have permanent BlockId values: OPT.N for options, CND.N for conditions, CNS.N for consequences and ANM.N for animations. API indices still select the current list position.",
                "The replacement map survives script reloads in this Unity session but is not stored in the database. Export it before closing Unity if external references need conversion. Internal node ids and edges persist when saved."
            }) },
            { "layout", new Topic(new[]
            {
                "Plan the layout from the story structure. Read the nodes, ports and edges, then identify the main path, branches, where they rejoin, and which nodes end a branch.",
                "Read the main path from left to right. Use short straight runs where the ports line up. Turn down and right when that follows an outgoing port or brings a short side branch closer to its parent.",
                "Keep a fork's branches in the visual order of its output ports. Give long branches their own space, tuck a simple ending into free space near its source, "
                    + "and bring a shared continuation close to the outputs that feed it. Below the branches can be clearer when both outputs point down.",
                "Judge the positions by the fully expanded card bounds and actual port locations. Look for short, distinct links, few crossings, and no links running behind unrelated cards. Avoid long empty rows or columns created just to align cards.",
                "Automatic placement steps down and right from tall Lines and places joins under incoming Line outputs when there is room. A lower branch that ends immediately can sit 40 units below its parent. Other cards keep the standard 180 horizontal or 120 vertical clearance.",
                "Use automatic placement or graph.layout as a starting point. Read the returned placement, including expandedSize, final coordinates and conflicts, and refine the graph with node.patch Position where needed.",
                "Review the final positions and links after edits. If the solver adjusted a proposal, check the new route and move the nearby cards together if needed. Save the reviewed layout.",
                "Positions supplied in MCP writes are proposals. Review placement for the final position, expandedSize, requestedPosition when supplied, and conflicts with other node ids. An overlap intersects a node, clearance means the cards were too close.",
                "Run graph.layout through ends_contract_apply to rearrange an existing dialog around its Start node. The operation reports placements in its result and follows the same save and discard rules as other graph edits."
            }) },
            { "line", new Topic(new[]
            {
                "A Line has one next port, localized LineText entries, an optional character display-name reference, character actions and isInstant.",
                "LineText supports LanguageCode, Text, SayerName and VoiceClip. The line CharacterName references the display-name catalog, character actions reference the separate character sprite catalog.",
                "Write nested objects as patches. Supplying a list replaces it completely, preserve entries and unchanged reference diagnostics you intend to keep.",
                "Assets must already exist and be imported as the expected type. Text writing does not create an AudioClip or a Sprite."
            }, typeof(LineNode), typeof(LineText)) },
            { "choice", new Topic(new[]
            {
                "A Choice node contains an ordered list of ChoiceNode options. Each Option has a permanent OPT.N identity, translated text, optional visibility conditions, consequences and its own next node.",
                "CH.N and Choice.N option IDs migrate to OPT.N with their numbers preserved. Option indices are zero-based. choice.add appends, choice.remove shifts later indices, and replacing ChoiceData changes the complete list and its order.",
                "ChoiceText contains LanguageCode and Text only. Speaker labels and voice clips are LineText fields and are rejected for ChoiceText.",
                "Conditions decide whether the option is available. Consequences run after the option is chosen. Consult conditions or consequences when their runtime meaning matters."
            }, typeof(ChoiceNode), typeof(ChoiceText)) },
            { "condition", new Topic(new[]
            {
                "A Condition node evaluates DialogConditions using one flat AND or OR flag and follows its true or false port.",
                "An empty list, or a list without evaluable conditions, passes. Missing variable references are skipped by runtime evaluation and remain visible in diagnostics.",
                "Nested groups such as (A AND B) OR C are not a single-list feature. Build multiple Condition nodes to express groups without inventing model fields."
            }, typeof(ConditionNode), typeof(DialogCondition)) },
            { "conditions", new Topic(new[]
            {
                "DialogEntry.conditions decide which dialog an initiator selects. ConditionNode.DialogConditions branch inside a dialog. ChoiceNode.ChoiceCondition control option availability.",
                "Each list has a single AND or OR flag. Empty or entirely unevaluable lists pass. Nested group objects are unsupported.",
                "Reference an existing variable and its declared type. Bool and String use equality or inequality, numeric types support comparison operators. Shared rules also prevent incompatible repeated comparisons.",
                "Locked dialogs are skipped by initiator selection and cannot edit start conditions. A locked dialog can still be a redirect target.",
                "An initiator prefers the first matching dialog with evaluable conditions over an unconditional fallback, regardless of the fallback position. Global database order chooses the first match within each group. dialog.move uses the complete list index across categories.",
                "Deleting a referenced variable preserves its original label in read-only diagnostics. Repair by assigning a valid variable, do not manufacture MissingReferenceLabel or ReferenceState."
            }, typeof(DialogEntry), typeof(DialogCondition), typeof(ChoiceCondition)) },
            { "consequences", new Topic(new[]
            {
                "Choice consequences assign the stored typed value to the selected variable after choosing an option. IntValue=1 means set to 1, not add 1.",
                "Increment, decrement, multiplication and expression evaluation are unsupported. Do not present a fixed assignment as an implementation of an arithmetic request.",
                "Select an existing variable and its declared type. Only the corresponding BoolValue, IntValue, FloatValue or StringValue controls the runtime assignment.",
                "A consequence list cannot reuse a variable under the shared editing rules. Missing references remain diagnostic and are skipped at runtime."
            }, typeof(ChoiceConsequence), typeof(VariableDefinition)) },
            { "variables", new Topic(new[]
            {
                "VariableDefinition stores a database variable type and its default typed values. variable.add, variable.patch, variable.rename and variable.remove edit these definitions and maintain graph references.",
                "Runtime current values are separate from database defaults. ends_write_variable_current changes the current value, load and save use runtime save.json, they do not edit VariableDefinition defaults.",
                "Adding, renaming, removing, or changing a variable definition synchronizes current values without writing save.json. Undo and Discard restore definitions while retaining live values; undoing a deletion restores its value.",
                "Edit Mode retains current values per database. In Play Mode these tools require the active DialogRunner database. Load works in both modes; missing files keep values and return missing=true. Invalid files and failed saves return errors.",
                "Use the existing variable type. BoolValue, IntValue, FloatValue or StringValue on a condition or consequence must correspond to that type.",
                "Variable names are normalized by the shared rules. Read the returned name after creation or rename, do not keep references to an unnormalized spelling."
            }, typeof(VariableDefinition)) },
            { "animations", new Topic(new[]
            {
                "A Line holds CharacterActions. Show chooses a character sprite slot and transform values, Hide hides the character. Actions are not a general timeline or arbitrary animation program.",
                "A character can appear only once in the action list of one Line. Use another Line when the same character needs another action.",
                "Use action.add, action.patch, action.remove or action.copy. Indices are zero-based and shift after removal. Position and zoom limits below come directly from the shared field rules.",
                "Characters and sprite slots must exist. Sprite assignment accepts imported Sprite assets or an exact Sprite subasset identity, a Texture2D without a Sprite import is a type mismatch."
            }, typeof(CharacterActionData), typeof(CharacterGroup), typeof(CharacterSprite)) },
            { "redirect", new Topic(new[]
            {
                "A Redirect targets an eligible dialog in an existing category, not a node id. Eligible targets are locked dialogs or dialogs without evaluable start conditions. Use edge.set with port=redirect, category and targetDialog.",
                "An empty targetDialog clears the explicit dialog target. It does not create a dialog or guarantee that runtime chooses a particular candidate.",
                "Dialog or category rename updates stored redirect references. Deletion preserves the missing target label for diagnostics."
            }, typeof(RedirectNode)) },
            { "start", new Topic(new[]
            {
                "A dialog starts from its Start node and its next port. Start has no incoming edges. Self-connections are forbidden for every node type.",
                "dialog.add creates a Start node with a fresh STN id. Removing the only Start is rejected.",
                "Dialog start selection conditions belong to DialogEntry, not StartNode. Consult conditions for initiator selection and order."
            }, typeof(StartNode), typeof(DialogEntry)) },
            { "localization", new Topic(new[]
            {
                "Text keys include the literal dialog name and full node id separated by dots: Test.LNN.5.line or Test.CHN.2.OPT.3. Dialog names cannot contain dots. Option IDs remain stable across reordering. Index-based choice keys are rejected. Unscoped node addresses must resolve uniquely.",
                "Use existing language codes. Line text supports speaker labels and voice clips, choice text supports only translated text.",
                "localization.csv imports into the same working session. addLanguages explicitly permits new language codes. Importing an old CSV after id migration requires converting its keys using the id map.",
                "Removing an option makes its old key unknown. A replacement option receives a new permanent ID, so old translations are never applied by list position. Export again after changing dialog names or converting keys from the previous slash-separated format."
            }, typeof(LineText), typeof(ChoiceText)) },
            { "sessions", new Topic(new[]
            {
                "UI and MCP write through the same database session, registered operations and shared rules. A batch prepares a detached database and commits only after all operations and final validation pass.",
                "Use expectedRevision from the latest read. On conflict reread and rebuild the request. A rejected batch changes no graph data, its reserved identity numbers may be consumed.",
                "The editor edits a detached workspace. Acquire dialogs with ends_edit_lease before writing, acquire shared=true for common catalogs or dialog ordering. Claim every dialog affected by rename, deletion or reference cleanup. Different sessions may own different dialogs.",
                "Leases belong to MCP sessions, expire after 120 seconds without ENDS activity and are released on session termination. Renew explicitly during long work. "
                + "Local Take control revokes ownership, an old prepared request must not be replayed with a new revision without reconsidering intent.",
                "save=true saves only scopes affected by the batch. ends_session_save/discard accept dialogs and shared selectors. Omitting both targets the whole database, ownership checks still apply. "
                + "Saving validates the exact disk candidate and rejects unsaved dependencies. Discard restores selected scopes and retains allocated identity numbers.",
                "On save_failed the edit may already be applied, inspect changed and dirty before retrying. errorDetails identifies an operation or argument, validation identifies graph owners and fields.",
                "Unchanged pre-existing graph errors do not block unrelated repairs. ends_contract_validate reports existing issues, it does not substitute for automatic write validation."
            }) },
            { "assets", new Topic(new[]
            {
                "Display names are trimmed on add and rename. Empty or conflicting names are rejected. name.add with unique=true returns a generated available name. Existing name selectors are exact, use the returned name.",
                "CharacterNameColor.Color is a Unity Color. Canonical reads return r,g,b,a in [0,1], canonical writes accept those exact components or #RRGGBB/#RRGGBBAA. The convenience name tool and catalog use HEX, ColorHex is no longer a public field.",
                "Legacy name colors are converted manually with Migrate name colors in ENDS Settings. Save the database to persist the converted format. Opening a database and building do not migrate colors or save pending graph edits.",
                "Asset references accept an imported asset path or an object with asset or guid and an optional localId. Path and guid, when both supplied, must identify the same asset.",
                "localId selects the exact imported subasset, which must have the expected Unity type. A transient instanceId works only while that editor object exists.",
                "Diagnostics distinguish missing path, unavailable guid or localId, and wrong imported type. A Sprite may be a subasset of an imported texture.",
                "ENDS MCP assigns existing assets. It does not import external files, generate voice audio, create a Sprite asset or create a Dialog Database asset."
            }) }
        };

        #endregion FIELDS

        #region TOOLS

        internal static EndsMcpJsonED Tool()
        {
            EndsMcpJsonED properties = EndsMcpJsonED.Obj();
            EndsMcpJsonED topic = EndsMcpJsonED.Obj();
            topic.Set("type", EndsMcpJsonED.Str("string"));
            EndsMcpJsonED values = EndsMcpJsonED.Arr();

            foreach (string name in _topics.Keys)
            {
                values.Add(EndsMcpJsonED.Str(name));
            }

            topic.Set("enum", values);
            properties.Set("topic", topic);
            EndsMcpJsonED database = EndsMcpJsonED.Obj();
            database.Set("type", EndsMcpJsonED.Str("string"));
            database.Set("description", EndsMcpJsonED.Str("Existing database path, needed only when requesting its id migration map."));
            properties.Set("database", database);
            EndsMcpJsonED map = EndsMcpJsonED.Obj();
            map.Set("type", EndsMcpJsonED.Str("boolean"));
            map.Set("description", EndsMcpJsonED.Str("Include old-to-new id maps grouped by dialog for topic ids. Default false."));
            properties.Set("includeMigrationMap", map);
            EndsMcpJsonED schema = EndsMcpJsonED.Obj();
            schema.Set("type", EndsMcpJsonED.Str("object"));
            schema.Set("properties", properties);
            schema.Set("additionalProperties", EndsMcpJsonED.Boolean(false));
            EndsMcpJsonED required = EndsMcpJsonED.Arr();
            required.Add(EndsMcpJsonED.Str("topic"));
            schema.Set("required", required);
            EndsMcpJsonED tool = EndsMcpJsonED.Obj();
            tool.Set("name", EndsMcpJsonED.Str("ends_describe"));
            tool.Set("description", EndsMcpJsonED.Str("Read detailed runtime semantics and limitations of one ENDS topic when a request needs clarification. Reuse the answer while contractVersion stays unchanged, do not call for every node."));
            tool.Set("inputSchema", schema);

            return tool;
        }

        internal static bool TryDescribe(EndsMcpJsonED args, out string json, out string error)
        {
            json = null;
            error = null;

            try
            {
                string name = EndsDialogGraphContractED.String(args.Get("topic"));

                if (!_topics.TryGetValue(name, out Topic topic)) throw new InvalidOperationException("Unknown documentation topic. Use a topic from the ends_describe schema.");

                EndsMcpJsonED root = EndsMcpJsonED.Obj();
                root.Set("contractVersion", EndsMcpJsonED.Num(EndsDialogGraphContractED.CONTRACT_VERSION));
                root.Set("topic", EndsMcpJsonED.Str(name));
                EndsMcpJsonED notes = EndsMcpJsonED.Arr();

                foreach (string note in topic.Notes)
                {
                    notes.Add(EndsMcpJsonED.Str(note));
                }

                root.Set("notes", notes);
                EndsMcpJsonED models = EndsMcpJsonED.Obj();

                foreach (Type model in topic.Models)
                {
                    models.Set(model.Name, EndsDialogGraphContractED.ReadModelSchema(model));
                }

                root.Set("models", models);

                if (args.Get("includeMigrationMap") != null && EndsDialogGraphContractED.Boolean(args.Get("includeMigrationMap")))
                {
                    if (name != "ids") throw new InvalidOperationException("includeMigrationMap requires topic ids.");

                    DialogDatabase database = EndsDialogAiWriteED.RequestDatabase(args, out error);

                    if (database == null) return false;

                    root.Set("migrationMap", EndsSessionED.ForDatabase(database).ReadNodeIdMigration());
                }

                json = root.Write();

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;

                return false;
            }
        }

        #endregion TOOLS
    }
}
#endif