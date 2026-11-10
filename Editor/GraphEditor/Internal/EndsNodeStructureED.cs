#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Shared typed descriptions of stored members, logical blocks and dependencies, consumed by backend adapters without owning model values, commands or sessions. </summary>
    internal static partial class EndsNodeStructureED
    {
        #region DEFINITIONS AND ACCESS

        private static readonly DialogConditionDefinition _dialogConditionSchema = new DialogConditionDefinition();
        private static readonly ChoiceConditionDefinition _choiceConditionSchema = new ChoiceConditionDefinition();
        private static readonly ChoiceConsequenceDefinition _choiceConsequenceSchema = new ChoiceConsequenceDefinition();
        private static readonly AnimationDefinition _animationSchema = new AnimationDefinition();
        private static readonly LineTranslationDefinition _lineTranslationSchema = new LineTranslationDefinition();
        private static readonly ChoiceTranslationDefinition _choiceTranslationSchema = new ChoiceTranslationDefinition();
        private static readonly ChoiceOptionDefinition _choiceOptionSchema = new ChoiceOptionDefinition(_choiceTranslationSchema, _choiceConditionSchema, _choiceConsequenceSchema);
        private static readonly StartDefinition _startNodeSchema = new StartDefinition();
        private static readonly LineDefinition _lineNodeSchema = new LineDefinition(_animationSchema, _lineTranslationSchema);
        private static readonly ConditionNodeDefinition _conditionNodeSchema = new ConditionNodeDefinition(_dialogConditionSchema);
        private static readonly RedirectDefinition _redirectNodeSchema = new RedirectDefinition();
        private static readonly NodeDefinition _nodeSchema = new NodeDefinition(_startNodeSchema, _lineNodeSchema, _choiceOptionSchema, _conditionNodeSchema, _redirectNodeSchema);
        private static readonly DialogDefinition _dialogSchema = new DialogDefinition(_dialogConditionSchema, _nodeSchema);

        /// <summary> ENDS. Returns the shared description of one DialogCondition, used by dialog start conditions and branching nodes. </summary>
        internal static DialogConditionDefinition DialogConditionSchema => _dialogConditionSchema;
        /// <summary> ENDS. Returns the shared description of one option visibility condition, not an option instance or its runtime state. </summary>
        internal static ChoiceConditionDefinition ChoiceConditionSchema => _choiceConditionSchema;
        /// <summary> ENDS. Returns the shared description of one choice consequence, including its variable identity and typed Value. </summary>
        internal static ChoiceConsequenceDefinition ChoiceConsequenceSchema => _choiceConsequenceSchema;
        /// <summary> ENDS. Returns the shared description of one character animation, with named controls, Position grouping and dependencies. </summary>
        internal static AnimationDefinition AnimationSchema => _animationSchema;
        /// <summary> ENDS. Returns the shared description of one localized line row, including its existing language key. </summary>
        internal static LineTranslationDefinition LineTranslationSchema => _lineTranslationSchema;
        /// <summary> ENDS. Returns the shared description of one localized option row, with typed access to ChoiceText properties. </summary>
        internal static ChoiceTranslationDefinition ChoiceTranslationSchema => _choiceTranslationSchema;
        /// <summary> ENDS. Returns the shared description of one option inside a Choice node, including its own conditions and consequences. </summary>
        internal static ChoiceOptionDefinition ChoiceOptionSchema => _choiceOptionSchema;
        /// <summary> ENDS. Returns the shared description of StartNode content and its outgoing link. </summary>
        internal static StartDefinition StartNodeSchema => _startNodeSchema;
        /// <summary> ENDS. Returns the shared description of LineNode content, its animations, translations and outgoing link. </summary>
        internal static LineDefinition LineNodeSchema => _lineNodeSchema;
        /// <summary> ENDS. Returns the shared branching ConditionNode description, distinct from DialogConditionSchema, which describes one condition row. </summary>
        internal static ConditionNodeDefinition ConditionNodeSchema => _conditionNodeSchema;
        /// <summary> ENDS. Returns the shared description of RedirectNode content, including target selection and missing-reference information. </summary>
        internal static RedirectDefinition RedirectNodeSchema => _redirectNodeSchema;
        /// <summary> ENDS. Returns the shared description of the node envelope, identity, position and type-dependent payloads. </summary>
        internal static NodeDefinition NodeSchema => _nodeSchema;
        /// <summary> ENDS. Returns the shared description of dialog settings and nodes, it does not contain a database or save boundary. </summary>
        internal static DialogDefinition DialogSchema => _dialogSchema;

        // Named properties and Members contain the same definition instances.
        // A selector names one actual model member, its name and read/write delegates are derived once.
        // Rules own validation, Commands own operations, Session owns snapshots and persistence.

        #endregion DEFINITIONS AND ACCESS

        #region CONDITIONS AND CONSEQUENCES

        /// <summary> ENDS. Describes one dialog or branch condition, named fields and Members share definitions, dependencies connect Type, Variable, Operator and the selected Value. </summary>
        internal sealed class DialogConditionDefinition : NodeModelDefinition<DialogCondition>
        {
            /// <summary> ENDS. Identifies the condition block across snapshots, excluded from content changes and editable field patches. </summary>
            internal NodeFieldDefinition<DialogCondition, string> Id { get; }
            /// <summary> ENDS. Describes the stored variable type selector, determines the interpretation of VariableName and which Value alternative is active. </summary>
            internal NodeFieldDefinition<DialogCondition, ConditionVariableType> VariableType { get; }
            /// <summary> ENDS. Describes the stored variable name and its type dependency, resolving names against the database remains outside this description. </summary>
            internal NodeFieldDefinition<DialogCondition, string> VariableName { get; }
            /// <summary> ENDS. Describes the comparison operator associated with VariableName, its context link to Value does not treat an unchanged value as another write. </summary>
            internal NodeFieldDefinition<DialogCondition, ConditionOperator> Operator { get; }
            /// <summary> ENDS. Describes one logical comparison or assignment value, exposes all four typed alternatives and compares the selected type as well as its stored value. </summary>
            internal NodeVariableValueDefinition<DialogCondition> Value { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<DialogCondition, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<DialogCondition, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<DialogCondition>> Members { get; }

            /// <summary> ENDS. Builds immutable condition field definitions and dependencies once, creates no DialogCondition and stores no model caption. </summary>
            internal DialogConditionDefinition()
            {
                Id = new NodeFieldDefinition<DialogCondition, string>((DialogCondition condition) => condition.BlockId, "Block ID", FieldRole.Identity);
                VariableType = new NodeFieldDefinition<DialogCondition, ConditionVariableType>((DialogCondition condition) => condition.VariableType, "Type");
                VariableName = new NodeFieldDefinition<DialogCondition, string>((DialogCondition condition) => condition.VariableName, "Variable", dependencies: new[] { Meaning(VariableType) });
                Operator = new NodeFieldDefinition<DialogCondition, ConditionOperator>((DialogCondition condition) => condition.ConditionOperator, "Operator", dependencies: new[] { Meaning(VariableName) });
                Value = new NodeVariableValueDefinition<DialogCondition>(VariableType, new NodeFieldDefinition<DialogCondition, bool>((DialogCondition condition) => condition.BoolValue, "Value"),
                    new NodeFieldDefinition<DialogCondition, int>((DialogCondition condition) => condition.IntValue, "Value"), new NodeFieldDefinition<DialogCondition, float>((DialogCondition condition) => condition.FloatValue, "Value"),
                    new NodeFieldDefinition<DialogCondition, string>((DialogCondition condition) => condition.StringValue, "Value"), Meaning(VariableType), Meaning(VariableName), Context(Operator));
                ReferenceState = new NodeFieldDefinition<DialogCondition, DialogReferenceState>((DialogCondition condition) => condition.ReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<DialogCondition, string>((DialogCondition condition) => condition.MissingReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Id, VariableType, VariableName, Operator, Value, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<DialogCondition>(VariableName, NodeReferenceKind.Variable, variableType: VariableType, state: ReferenceState, missingLabel: MissingReferenceLabel));
            }
        }

        /// <summary> ENDS. Describes one option visibility condition using ChoiceCondition, separates stored fields from their type-dependent meaning and presentation. </summary>
        internal sealed class ChoiceConditionDefinition : NodeModelDefinition<ChoiceCondition>
        {
            /// <summary> ENDS. Identifies the visibility-condition block across snapshots without exposing metadata as a content change. </summary>
            internal NodeFieldDefinition<ChoiceCondition, string> Id { get; }
            /// <summary> ENDS. Describes the stored variable type selector, determines the interpretation of VariableName and which Value alternative is active. </summary>
            internal NodeFieldDefinition<ChoiceCondition, ConditionVariableType> VariableType { get; }
            /// <summary> ENDS. Describes the stored variable name and its type dependency, resolving names against the database remains outside this description. </summary>
            internal NodeFieldDefinition<ChoiceCondition, string> VariableName { get; }
            /// <summary> ENDS. Describes the comparison operator associated with VariableName, its context link to Value does not treat an unchanged value as another write. </summary>
            internal NodeFieldDefinition<ChoiceCondition, ConditionOperator> Operator { get; }
            /// <summary> ENDS. Describes one logical comparison or assignment value, exposes all four typed alternatives and compares the selected type as well as its stored value. </summary>
            internal NodeVariableValueDefinition<ChoiceCondition> Value { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<ChoiceCondition, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<ChoiceCondition, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<ChoiceCondition>> Members { get; }

            /// <summary> ENDS. Builds option-condition definitions and immutable members, creates no condition data or runtime checks. </summary>
            internal ChoiceConditionDefinition()
            {
                Id = new NodeFieldDefinition<ChoiceCondition, string>((ChoiceCondition condition) => condition.BlockId, "Block ID", FieldRole.Identity);
                VariableType = new NodeFieldDefinition<ChoiceCondition, ConditionVariableType>((ChoiceCondition condition) => condition.VariableType, "Type");
                VariableName = new NodeFieldDefinition<ChoiceCondition, string>((ChoiceCondition condition) => condition.VariableName, "Variable", dependencies: new[] { Meaning(VariableType) });
                Operator = new NodeFieldDefinition<ChoiceCondition, ConditionOperator>((ChoiceCondition condition) => condition.ConditionOperator, "Operator", dependencies: new[] { Meaning(VariableName) });
                Value = new NodeVariableValueDefinition<ChoiceCondition>(VariableType, new NodeFieldDefinition<ChoiceCondition, bool>((ChoiceCondition condition) => condition.BoolValue, "Value"),
                    new NodeFieldDefinition<ChoiceCondition, int>((ChoiceCondition condition) => condition.IntValue, "Value"), new NodeFieldDefinition<ChoiceCondition, float>((ChoiceCondition condition) => condition.FloatValue, "Value"),
                    new NodeFieldDefinition<ChoiceCondition, string>((ChoiceCondition condition) => condition.StringValue, "Value"), Meaning(VariableType), Meaning(VariableName), Context(Operator));
                ReferenceState = new NodeFieldDefinition<ChoiceCondition, DialogReferenceState>((ChoiceCondition condition) => condition.ReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<ChoiceCondition, string>((ChoiceCondition condition) => condition.MissingReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Id, VariableType, VariableName, Operator, Value, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<ChoiceCondition>(VariableName, NodeReferenceKind.Variable, variableType: VariableType, state: ReferenceState, missingLabel: MissingReferenceLabel));
            }
        }

        /// <summary> ENDS. Describes one choice consequence, links variable type and name to a typed Value without executing the consequence or creating model data. </summary>
        internal sealed class ChoiceConsequenceDefinition : NodeModelDefinition<ChoiceConsequence>
        {
            /// <summary> ENDS. Identifies one consequence block so deleting it and inserting another remain distinct operations. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, string> Id { get; }
            /// <summary> ENDS. Describes the stored variable type selector, determines the interpretation of VariableName and which Value alternative is active. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, ConditionVariableType> VariableType { get; }
            /// <summary> ENDS. Describes the stored variable name and its type dependency, resolving names against the database remains outside this description. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, string> VariableName { get; }
            /// <summary> ENDS. Describes the operation applied to the variable, arithmetic is restricted to numeric variables by domain validation. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, ConsequenceOperation> Operation { get; }
            /// <summary> ENDS. Describes one logical comparison or assignment value, exposes all four typed alternatives and compares the selected type as well as its stored value. </summary>
            internal NodeVariableValueDefinition<ChoiceConsequence> Value { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<ChoiceConsequence, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<ChoiceConsequence>> Members { get; }

            /// <summary> ENDS. Builds the variable and Value descriptions, executes no assignments and allocates no ChoiceConsequence model. </summary>
            internal ChoiceConsequenceDefinition()
            {
                Id = new NodeFieldDefinition<ChoiceConsequence, string>((ChoiceConsequence consequence) => consequence.BlockId, "Block ID", FieldRole.Identity);
                VariableType = new NodeFieldDefinition<ChoiceConsequence, ConditionVariableType>((ChoiceConsequence consequence) => consequence.VariableType, "Type");
                VariableName = new NodeFieldDefinition<ChoiceConsequence, string>((ChoiceConsequence consequence) => consequence.VariableName, "Variable", dependencies: new[] { Meaning(VariableType) });
                Operation = new NodeFieldDefinition<ChoiceConsequence, ConsequenceOperation>((ChoiceConsequence consequence) => consequence.Operation, "Operation", dependencies: new[] { Meaning(VariableType) });
                Value = new NodeVariableValueDefinition<ChoiceConsequence>(VariableType, new NodeFieldDefinition<ChoiceConsequence, bool>((ChoiceConsequence consequence) => consequence.BoolValue, "Value"),
                    new NodeFieldDefinition<ChoiceConsequence, int>((ChoiceConsequence consequence) => consequence.IntValue, "Value"), new NodeFieldDefinition<ChoiceConsequence, float>((ChoiceConsequence consequence) => consequence.FloatValue, "Value"),
                    new NodeFieldDefinition<ChoiceConsequence, string>((ChoiceConsequence consequence) => consequence.StringValue, "Value"), Meaning(VariableType), Meaning(VariableName), Meaning(Operation));
                ReferenceState = new NodeFieldDefinition<ChoiceConsequence, DialogReferenceState>((ChoiceConsequence consequence) => consequence.ReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<ChoiceConsequence, string>((ChoiceConsequence consequence) => consequence.MissingReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Id, VariableType, VariableName, Operation, Value, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<ChoiceConsequence>(VariableName, NodeReferenceKind.Variable, variableType: VariableType, state: ReferenceState, missingLabel: MissingReferenceLabel));
            }
        }

        #endregion CONDITIONS AND CONSEQUENCES

        #region ANIMATIONS

        /// <summary> ENDS. Describes one character animation, separates field ownership from Character/Emotion and Action/visibility dependencies, groups the existing X and Y definitions. </summary>
        internal sealed class AnimationDefinition : NodeModelDefinition<CharacterActionData>
        {
            /// <summary> ENDS. Identifies an animation block independently of its character, action and parameters. </summary>
            internal NodeFieldDefinition<CharacterActionData, string> Id { get; }
            /// <summary> ENDS. Describes the stored character selection, changes can alter the meaning of Emotion without rewriting it here. </summary>
            internal NodeFieldDefinition<CharacterActionData, string> Character { get; }
            /// <summary> ENDS. Describes the stored emotion and its meaning dependency on Character, available emotions come from the existing catalog. </summary>
            internal NodeFieldDefinition<CharacterActionData, string> Emotion { get; }
            /// <summary> ENDS. Describes Show or Hide, controls Zoom and Position presentation without changing their stored values. </summary>
            internal NodeFieldDefinition<CharacterActionData, CharacterAction> Action { get; }
            /// <summary> ENDS. Describes animation scale, visible for Show and still accessible as stored data for Hide. </summary>
            internal NodeFieldDefinition<CharacterActionData, float> Zoom { get; }
            /// <summary> ENDS. Describes stored PosX, shares its definition with Position.Children and is presented for Show. </summary>
            internal NodeFieldDefinition<CharacterActionData, float> X { get; }
            /// <summary> ENDS. Describes stored PosY, shares its definition with Position.Children and is presented for Show. </summary>
            internal NodeFieldDefinition<CharacterActionData, float> Y { get; }
            /// <summary> ENDS. Groups existing X and Y definitions for one Position entry, adds no serialized field and does not absorb child ownership. </summary>
            internal NodeGroupDefinition<CharacterActionData> Position { get; }
            /// <summary> ENDS. Selects action-specific blocks through registered branches, descendants inherit branch visibility while full storage traversal retains every field. </summary>
            internal NodeVariantDefinition<CharacterActionData, CharacterAction> ActionVariant { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<CharacterActionData, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<CharacterActionData, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<CharacterActionData>> Members { get; }

            /// <summary> ENDS. Builds animation fields, shared X/Y grouping and visibility dependencies, creates no character action or preview object. </summary>
            internal AnimationDefinition()
            {
                Id = new NodeFieldDefinition<CharacterActionData, string>((CharacterActionData animation) => animation.BlockId, "Block ID", FieldRole.Identity);
                Character = new NodeFieldDefinition<CharacterActionData, string>((CharacterActionData animation) => animation.CharacterName, "Character");
                Emotion = new NodeFieldDefinition<CharacterActionData, string>((CharacterActionData animation) => animation.EmotionName, "Emotion", dependencies: new[] { Meaning(Character) });
                Action = new NodeFieldDefinition<CharacterActionData, CharacterAction>((CharacterActionData animation) => animation.Action, "Action");
                Zoom = new NodeFieldDefinition<CharacterActionData, float>((CharacterActionData animation) => animation.Zoom, "Zoom");
                X = new NodeFieldDefinition<CharacterActionData, float>((CharacterActionData animation) => animation.PosX, "X");
                Y = new NodeFieldDefinition<CharacterActionData, float>((CharacterActionData animation) => animation.PosY, "Y");
                Position = new NodeGroupDefinition<CharacterActionData>(nameof(Position), "Position", new NodeElementDefinition<CharacterActionData>[] { X, Y });
                ActionVariant = new NodeVariantDefinition<CharacterActionData, CharacterAction>(nameof(ActionVariant), "Action", Action,
                    new NodeBranchDefinition<CharacterActionData, CharacterAction>(nameof(CharacterAction.Show), "Show", CharacterAction.Show, Zoom, Position), new NodeBranchDefinition<CharacterActionData, CharacterAction>(nameof(CharacterAction.Hide), "Hide", CharacterAction.Hide));
                ReferenceState = new NodeFieldDefinition<CharacterActionData, DialogReferenceState>((CharacterActionData animation) => animation.ReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<CharacterActionData, string>((CharacterActionData animation) => animation.MissingReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Id, Character, Emotion, ActionVariant, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<CharacterActionData>(Character, NodeReferenceKind.Actor, qualifier: Emotion, state: ReferenceState, missingLabel: MissingReferenceLabel),
                    new NodeReferenceDefinition<CharacterActionData>(Emotion, NodeReferenceKind.Sprite, qualifier: Character, state: ReferenceState, missingLabel: MissingReferenceLabel, active: model => Action.Read(model) == CharacterAction.Show));
            }
        }

        #endregion ANIMATIONS

        #region TRANSLATIONS

        /// <summary> ENDS. Describes one localized line, language identifies the row while speaker name, text and voice remain independently accessible fields. </summary>
        internal sealed class LineTranslationDefinition : NodeModelDefinition<LineText>
        {
            /// <summary> ENDS. Describes the existing language key with Identity metadata, a physical setter remains available for creation and restoration, operation policies belong to Commands. </summary>
            internal NodeFieldDefinition<LineText, string> LanguageCode { get; }
            /// <summary> ENDS. Describes localized SayerName, distinct from the line's shared name-color reference. </summary>
            internal NodeFieldDefinition<LineText, string> SpeakerName { get; }
            /// <summary> ENDS. Describes localized line text, reads and writes the selected LineText field without transforming its contents. </summary>
            internal NodeFieldDefinition<LineText, string> Text { get; }
            /// <summary> ENDS. Describes the localized AudioClip reference, lookup, loading and playback remain outside this metadata. </summary>
            internal NodeFieldDefinition<LineText, AudioClip> VoiceClip { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<LineText>> Members { get; }

            /// <summary> ENDS. Builds localized line definitions and classifies the existing language key as Identity without changing its physical writability. </summary>
            internal LineTranslationDefinition()
            {
                LanguageCode = new NodeFieldDefinition<LineText, string>((LineText translation) => translation.LanguageCode, "Language", FieldRole.Identity);
                SpeakerName = new NodeFieldDefinition<LineText, string>((LineText translation) => translation.SayerName, "Speaker name");
                Text = new NodeFieldDefinition<LineText, string>((LineText translation) => translation.Text, "Text");
                VoiceClip = new NodeFieldDefinition<LineText, AudioClip>((LineText translation) => translation.VoiceClip, "Voice");
                Members = DefineMembers(LanguageCode, SpeakerName, Text, VoiceClip);
                DefineReferences(new NodeReferenceDefinition<LineText>(LanguageCode, NodeReferenceKind.Language));
            }
        }

        /// <summary> ENDS. Describes one localized option through ChoiceText properties, the selected public property is distinct from its private serialized backing field. </summary>
        internal sealed class ChoiceTranslationDefinition : NodeModelDefinition<ChoiceText>
        {
            /// <summary> ENDS. Describes the LanguageCode property used as the existing row key, Identity metadata is separate from its available property setter. </summary>
            internal NodeFieldDefinition<ChoiceText, string> LanguageCode { get; }
            /// <summary> ENDS. Describes the Text property, delegates use its getter and setter while serialized backing-field aliases remain a contract adapter concern. </summary>
            internal NodeFieldDefinition<ChoiceText, string> Text { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<ChoiceText>> Members { get; }

            /// <summary> ENDS. Builds property-based option text definitions, creates no translation row and does not rename serialized storage. </summary>
            internal ChoiceTranslationDefinition()
            {
                LanguageCode = new NodeFieldDefinition<ChoiceText, string>((ChoiceText translation) => translation.LanguageCode, "Language", FieldRole.Identity);
                Text = new NodeFieldDefinition<ChoiceText, string>((ChoiceText translation) => translation.Text, "Text");
                Members = DefineMembers(LanguageCode, Text);
                DefineReferences(new NodeReferenceDefinition<ChoiceText>(LanguageCode, NodeReferenceKind.Language));
            }
        }

        #endregion TRANSLATIONS

        #region NODE CONTENT

        /// <summary> ENDS. Describes StartNode content, exposes its next-node reference without resolving links or executing graph traversal. </summary>
        internal sealed class StartDefinition : NodeModelDefinition<StartNode>
        {
            /// <summary> ENDS. Describes the next-node ID stored in StartNode, does not resolve the target or enforce graph policies. </summary>
            internal NodeFieldDefinition<StartNode, string> NextNodeId { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<StartNode>> Members { get; }

            /// <summary> ENDS. Builds typed outgoing-link metadata without creating a StartNode, storing a model caption or executing graph traversal. </summary>
            internal StartDefinition()
            {
                NextNodeId = new NodeFieldDefinition<StartNode, string>((StartNode start) => start.NextNodeId, "Next node");
                Members = DefineMembers(NextNodeId);
                DefineReferences(new NodeReferenceDefinition<StartNode>(NextNodeId, NodeReferenceKind.Node));
            }
        }

        /// <summary> ENDS. Describes LineNode content, connects animation and translation collections to shared item definitions without drawing controls or playing animations. </summary>
        internal sealed class LineDefinition : NodeModelDefinition<LineNode>
        {
            /// <summary> ENDS. Describes ordered CharacterActions and their shared item structure, animation rows receive no new persistent IDs. </summary>
            internal NodeCollectionDefinition<LineNode, CharacterActionData, AnimationDefinition> Animations { get; }
            /// <summary> ENDS. Describes LineNode.CharacterName as the name-color reference, distinct from character selections inside animations. </summary>
            internal NodeFieldDefinition<LineNode, string> NameColor { get; }
            /// <summary> ENDS. Describes localized line rows, ItemKey is the same LanguageCode definition exposed by their item structure. </summary>
            internal NodeCollectionDefinition<LineNode, LineText, LineTranslationDefinition> Translations { get; }
            /// <summary> ENDS. Describes stored isInstant, runtime handling of automatic advancement remains outside the structure. </summary>
            internal NodeFieldDefinition<LineNode, bool> Instant { get; }
            /// <summary> ENDS. Describes the line's outgoing node ID, link resolution and validation remain backend responsibilities. </summary>
            internal NodeFieldDefinition<LineNode, string> NextNodeId { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<LineNode, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<LineNode, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<LineNode>> Members { get; }

            /// <summary> ENDS. Builds line fields using shared animation and translation descriptions, creates no LineNode or collection rows. </summary>
            internal LineDefinition(AnimationDefinition animations, LineTranslationDefinition translations)
            {
                Animations = new NodeCollectionDefinition<LineNode, CharacterActionData, AnimationDefinition>((LineNode line) => line.CharacterActions, "Animations", animations, animations.Id);
                NameColor = new NodeFieldDefinition<LineNode, string>((LineNode line) => line.CharacterName, "Name color");
                Translations = new NodeCollectionDefinition<LineNode, LineText, LineTranslationDefinition>((LineNode line) => line.DialogsText, "Translations", translations, translations.LanguageCode);
                Instant = new NodeFieldDefinition<LineNode, bool>((LineNode line) => line.isInstant, "Instant");
                NextNodeId = new NodeFieldDefinition<LineNode, string>((LineNode line) => line.NextNodeId, "Next node");
                ReferenceState = new NodeFieldDefinition<LineNode, DialogReferenceState>((LineNode line) => line.CharacterReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<LineNode, string>((LineNode line) => line.MissingCharacterReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Animations, NameColor, Translations, Instant, NextNodeId, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<LineNode>(NextNodeId, NodeReferenceKind.Node), new NodeReferenceDefinition<LineNode>(NameColor, NodeReferenceKind.Speaker, state: ReferenceState, missingLabel: MissingReferenceLabel));
            }
        }

        /// <summary> ENDS. Describes one option inside a Choice node, owns text, visibility conditions, consequences and the outgoing link, options retain their existing order. </summary>
        internal sealed class ChoiceOptionDefinition : NodeModelDefinition<ChoiceNode>
        {
            /// <summary> ENDS. Identifies an option independently of its text, ports, conditions and consequences. </summary>
            internal NodeFieldDefinition<ChoiceNode, string> Id { get; }
            /// <summary> ENDS. Describes localized option text rows, their existing language code identifies each translation. </summary>
            internal NodeCollectionDefinition<ChoiceNode, ChoiceText, ChoiceTranslationDefinition> Translations { get; }
            /// <summary> ENDS. Describes ordered option visibility conditions, these rows use ChoiceCondition rather than dialog start-condition instances. </summary>
            internal NodeCollectionDefinition<ChoiceNode, ChoiceCondition, ChoiceConditionDefinition> Conditions { get; }
            /// <summary> ENDS. Describes the option's IsAndLogic flag, true combines visibility conditions with AND and false with OR. </summary>
            internal NodeFieldDefinition<ChoiceNode, bool> AllConditionsRequired { get; }
            /// <summary> ENDS. Describes ordered consequences associated with selecting this option, enumeration does not execute them. </summary>
            internal NodeCollectionDefinition<ChoiceNode, ChoiceConsequence, ChoiceConsequenceDefinition> Consequences { get; }
            /// <summary> ENDS. Describes this option's outgoing node ID, independent of the other options in the same Choice node. </summary>
            internal NodeFieldDefinition<ChoiceNode, string> NextNodeId { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<ChoiceNode>> Members { get; }

            /// <summary> ENDS. Builds an option description using shared translation, condition and consequence definitions, does not create or select an option. </summary>
            internal ChoiceOptionDefinition(ChoiceTranslationDefinition translations, ChoiceConditionDefinition conditions, ChoiceConsequenceDefinition consequences)
            {
                Translations = new NodeCollectionDefinition<ChoiceNode, ChoiceText, ChoiceTranslationDefinition>((ChoiceNode option) => option.Translations, "Translations", translations, translations.LanguageCode);
                Conditions = new NodeCollectionDefinition<ChoiceNode, ChoiceCondition, ChoiceConditionDefinition>((ChoiceNode option) => option.ChoiceCondition, "Conditions", conditions, conditions.Id);
                AllConditionsRequired = new NodeFieldDefinition<ChoiceNode, bool>((ChoiceNode option) => option.IsAndLogic, "All conditions required");
                Consequences = new NodeCollectionDefinition<ChoiceNode, ChoiceConsequence, ChoiceConsequenceDefinition>((ChoiceNode option) => option.ChoiceConsequence, "Consequences", consequences, consequences.Id);
                NextNodeId = new NodeFieldDefinition<ChoiceNode, string>((ChoiceNode option) => option.NextNodeId, "Next node");
                Id = new NodeFieldDefinition<ChoiceNode, string>((ChoiceNode option) => option.BlockId, "Block ID", FieldRole.Identity);
                Members = DefineMembers(Id, Translations, Conditions, AllConditionsRequired, Consequences, NextNodeId);
                DefineReferences(new NodeReferenceDefinition<ChoiceNode>(NextNodeId, NodeReferenceKind.Node, port: NodePortKind.Choice));
            }
        }

        /// <summary> ENDS. Describes a branching ConditionNode, distinguishes its condition collection and AND/OR flag from the two outgoing graph links. </summary>
        internal sealed class ConditionNodeDefinition : NodeModelDefinition<ConditionNode>
        {
            /// <summary> ENDS. Describes ordered DialogConditions evaluated by this branch, items share the dialog-condition description. </summary>
            internal NodeCollectionDefinition<ConditionNode, DialogCondition, DialogConditionDefinition> Conditions { get; }
            /// <summary> ENDS. Describes IsAndLogic, true requires every condition and false accepts any matching condition. </summary>
            internal NodeFieldDefinition<ConditionNode, bool> AllConditionsRequired { get; }
            /// <summary> ENDS. Describes the outgoing node ID used when the combined condition result is true. </summary>
            internal NodeFieldDefinition<ConditionNode, string> TrueNodeId { get; }
            /// <summary> ENDS. Describes the outgoing node ID used when the combined condition result is false. </summary>
            internal NodeFieldDefinition<ConditionNode, string> FalseNodeId { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<ConditionNode>> Members { get; }

            /// <summary> ENDS. Builds branching-node metadata using the shared condition-row description, performs no graph evaluation. </summary>
            internal ConditionNodeDefinition(DialogConditionDefinition conditions)
            {
                Conditions = new NodeCollectionDefinition<ConditionNode, DialogCondition, DialogConditionDefinition>((ConditionNode condition) => condition.DialogConditions, "Conditions", conditions, conditions.Id);
                AllConditionsRequired = new NodeFieldDefinition<ConditionNode, bool>((ConditionNode condition) => condition.IsAndLogic, "All conditions required");
                TrueNodeId = new NodeFieldDefinition<ConditionNode, string>((ConditionNode condition) => condition.TrueNodeId, "True node");
                FalseNodeId = new NodeFieldDefinition<ConditionNode, string>((ConditionNode condition) => condition.FalseNodeId, "False node");
                Members = DefineMembers(Conditions, AllConditionsRequired, TrueNodeId, FalseNodeId);
                DefineReferences(new NodeReferenceDefinition<ConditionNode>(TrueNodeId, NodeReferenceKind.Node, port: NodePortKind.True), new NodeReferenceDefinition<ConditionNode>(FalseNodeId, NodeReferenceKind.Node, port: NodePortKind.False));
            }
        }

        /// <summary> ENDS. Describes a redirect target, relates dialog selection to category and exposes reference diagnostics, target validity remains a Rules responsibility. </summary>
        internal sealed class RedirectDefinition : NodeModelDefinition<RedirectNode>
        {
            /// <summary> ENDS. Describes target category, its dependency guides TargetDialog presentation without resolving a database. </summary>
            internal NodeFieldDefinition<RedirectNode, string> Category { get; }
            /// <summary> ENDS. Describes TargetDialogName and its dependency on Category, does not choose, lock or validate a target dialog. </summary>
            internal NodeFieldDefinition<RedirectNode, string> TargetDialog { get; }
            /// <summary> ENDS. Describes stored missing-reference status for diagnostics, it does not validate or repair the reference. </summary>
            internal NodeFieldDefinition<RedirectNode, DialogReferenceState> ReferenceState { get; }
            /// <summary> ENDS. Describes the preserved label of a missing reference so consumers can explain the unresolved target. </summary>
            internal NodeFieldDefinition<RedirectNode, string> MissingReferenceLabel { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<RedirectNode>> Members { get; }

            /// <summary> ENDS. Builds target fields and their category dependency, does not resolve or change a target. </summary>
            internal RedirectDefinition()
            {
                Category = new NodeFieldDefinition<RedirectNode, string>((RedirectNode redirect) => redirect.TargetCategory, "Category");
                TargetDialog = new NodeFieldDefinition<RedirectNode, string>((RedirectNode redirect) => redirect.TargetDialogName, "Dialog", dependencies: new[] { Meaning(Category) });
                ReferenceState = new NodeFieldDefinition<RedirectNode, DialogReferenceState>((RedirectNode redirect) => redirect.DialogReferenceState, "Reference state", FieldRole.Diagnostic);
                MissingReferenceLabel = new NodeFieldDefinition<RedirectNode, string>((RedirectNode redirect) => redirect.MissingDialogReferenceLabel, "Missing reference", FieldRole.Diagnostic);
                Members = DefineMembers(Category, TargetDialog, ReferenceState, MissingReferenceLabel);
                DefineReferences(new NodeReferenceDefinition<RedirectNode>(Category, NodeReferenceKind.Category), new NodeReferenceDefinition<RedirectNode>(TargetDialog, NodeReferenceKind.Dialog, qualifier: Category, state: ReferenceState, missingLabel: MissingReferenceLabel));
            }
        }

        #endregion NODE CONTENT

        #region NODE AND DIALOG

        /// <summary> ENDS. Describes the node envelope and all five payload types, visibility selects the presented payload while the other stored payloads remain accessible. </summary>
        internal sealed class NodeDefinition : NodeModelDefinition<DialogNode>
        {
            /// <summary> ENDS. Describes existing nodeId as Identity and as the dialog node collection's key, its setter remains available for creation and snapshot restoration. </summary>
            internal NodeFieldDefinition<DialogNode, string> Id { get; }
            /// <summary> ENDS. Describes stored NodeType as Identity and as the source of payload visibility, writability follows storage while operation restrictions belong to Rules and Commands. </summary>
            internal NodeFieldDefinition<DialogNode, NodeType> Type { get; }
            /// <summary> ENDS. Describes the node's Vector2 position as one typed field, position changes remain ordinary data edits. </summary>
            internal NodeFieldDefinition<DialogNode, Vector2> Position { get; }
            /// <summary> ENDS. Describes stored StartData and its shared structure, presented when Type is Start. </summary>
            internal NodeObjectDefinition<DialogNode, StartNode, StartDefinition> Start { get; }
            /// <summary> ENDS. Describes stored LineData and its shared structure, presented when Type is Line. </summary>
            internal NodeObjectDefinition<DialogNode, LineNode, LineDefinition> Line { get; }
            /// <summary> ENDS. Describes ordered ChoiceData, presented when Type is Choice without inventing option IDs. </summary>
            internal NodeCollectionDefinition<DialogNode, ChoiceNode, ChoiceOptionDefinition> Options { get; }
            /// <summary> ENDS. Describes stored ConditionData and its shared branching structure, presented when Type is Condition. </summary>
            internal NodeObjectDefinition<DialogNode, ConditionNode, ConditionNodeDefinition> Condition { get; }
            /// <summary> ENDS. Describes stored RedirectData and its shared target structure, presented when Type is Redirect. </summary>
            internal NodeObjectDefinition<DialogNode, RedirectNode, RedirectDefinition> Redirect { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<DialogNode>> Members { get; }

            /// <summary> ENDS. Builds the envelope using shared payload descriptions, creates no graph node and allocates no IDs. </summary>
            internal NodeDefinition(StartDefinition start, LineDefinition line, ChoiceOptionDefinition options, ConditionNodeDefinition condition, RedirectDefinition redirect)
            {
                Id = new NodeFieldDefinition<DialogNode, string>((DialogNode node) => node.nodeId, "Node ID", FieldRole.Identity);
                Type = new NodeFieldDefinition<DialogNode, NodeType>((DialogNode node) => node.NodeType, "Node type", FieldRole.Identity);
                Position = new NodeFieldDefinition<DialogNode, Vector2>((DialogNode node) => node.Position, "Position");
                Start = new NodeObjectDefinition<DialogNode, StartNode, StartDefinition>((DialogNode node) => node.StartData, "Start", start, VisibleWhen(Type, (DialogNode node) => node.NodeType == NodeType.Start));
                Line = new NodeObjectDefinition<DialogNode, LineNode, LineDefinition>((DialogNode node) => node.LineData, "Line", line, VisibleWhen(Type, (DialogNode node) => node.NodeType == NodeType.Line));
                Options = new NodeCollectionDefinition<DialogNode, ChoiceNode, ChoiceOptionDefinition>((DialogNode node) => node.ChoiceData, "Options", options, options.Id, dependencies: new[] { VisibleWhen(Type, (DialogNode node) => node.NodeType == NodeType.Choice) });
                Condition = new NodeObjectDefinition<DialogNode, ConditionNode, ConditionNodeDefinition>((DialogNode node) => node.ConditionData, "Condition", condition, VisibleWhen(Type, (DialogNode node) => node.NodeType == NodeType.Condition));
                Redirect = new NodeObjectDefinition<DialogNode, RedirectNode, RedirectDefinition>((DialogNode node) => node.RedirectData, "Redirect", redirect, VisibleWhen(Type, (DialogNode node) => node.NodeType == NodeType.Redirect));
                Members = DefineMembers(Id, Type, Position, Start, Line, Options, Condition, Redirect);
            }
        }

        /// <summary> ENDS. Describes dialog settings, start conditions and nodes, locking controls condition presentation while identity counters and persistence remain existing backend concerns. </summary>
        internal sealed class DialogDefinition : NodeModelDefinition<DialogEntry>
        {
            /// <summary> ENDS. Describes dialogName as Identity with its physical setter, rename conflict policies and permissions remain owned by Rules and Commands. </summary>
            internal NodeFieldDefinition<DialogEntry, string> Name { get; }
            /// <summary> ENDS. Describes initiator as dialog category, catalog policies and category movement remain outside the structure. </summary>
            internal NodeFieldDefinition<DialogEntry, string> Category { get; }
            /// <summary> ENDS. Describes isLocked, visibility dependencies hide start-condition controls without deleting stored conditions. </summary>
            internal NodeFieldDefinition<DialogEntry, bool> Locked { get; }
            /// <summary> ENDS. Describes ordered start conditions, presented when unlocked and retained in full member traversal when locked. </summary>
            internal NodeCollectionDefinition<DialogEntry, DialogCondition, DialogConditionDefinition> Conditions { get; }
            /// <summary> ENDS. Describes isAndLogic for start conditions, presented when unlocked, true means AND and false means OR. </summary>
            internal NodeFieldDefinition<DialogEntry, bool> AllConditionsRequired { get; }
            /// <summary> ENDS. Describes ordered nodes, ItemKey is the shared nodeId definition, identity allocation and saving are not implemented here. </summary>
            internal NodeCollectionDefinition<DialogEntry, DialogNode, NodeDefinition> Nodes { get; }
            /// <summary> ENDS. Returns immutable top-level definitions, named properties and logical children share these same objects, including currently hidden fields. </summary>
            internal override IReadOnlyList<NodeElementDefinition<DialogEntry>> Members { get; }

            /// <summary> ENDS. Builds dialog metadata using shared child definitions, creates no dialog, snapshot or save operation. </summary>
            internal DialogDefinition(DialogConditionDefinition conditions, NodeDefinition nodes)
            {
                Name = new NodeFieldDefinition<DialogEntry, string>((DialogEntry dialog) => dialog.dialogName, "Dialog name", FieldRole.Identity);
                Category = new NodeFieldDefinition<DialogEntry, string>((DialogEntry dialog) => dialog.initiator, "Category");
                Locked = new NodeFieldDefinition<DialogEntry, bool>((DialogEntry dialog) => dialog.isLocked, "Locked");
                NodeDependencyDefinition<DialogEntry> unlocked = VisibleWhen(Locked, (DialogEntry dialog) => !dialog.isLocked);
                Conditions = new NodeCollectionDefinition<DialogEntry, DialogCondition, DialogConditionDefinition>((DialogEntry dialog) => dialog.conditions, "Start conditions", conditions, conditions.Id, dependencies: new[] { unlocked });
                AllConditionsRequired = new NodeFieldDefinition<DialogEntry, bool>((DialogEntry dialog) => dialog.isAndLogic, "All conditions required", dependencies: new[] { unlocked });
                Nodes = new NodeCollectionDefinition<DialogEntry, DialogNode, NodeDefinition>((DialogEntry dialog) => dialog.nodes, "Nodes", nodes, nodes.Id);
                Members = DefineMembers(Name, Category, Locked, Conditions, AllConditionsRequired, Nodes);
                DefineReferences(new NodeReferenceDefinition<DialogEntry>(Category, NodeReferenceKind.Category));
            }
        }

        #endregion NODE AND DIALOG

        #region COMMON ELEMENTS AND DEPENDENCIES

        /// <summary> ENDS. Classifies content, identity and diagnostics for consumers, describes purpose without granting or denying writes to physical storage. </summary>
        internal enum FieldRole
        {
            /// <summary> ENDS. Ordinary editable content, the role itself adds no validation or persistence behavior. </summary>
            Content,
            /// <summary> ENDS. Existing identity storage, the role does not disable its setter, creation, restoration and edit permissions remain operation-specific policies. </summary>
            Identity,
            /// <summary> ENDS. Stored reference diagnostics, exposed separately from ordinary content for consumer-specific presentation. </summary>
            Diagnostic
        }

        /// <summary> ENDS. Describes why one element depends on another, a dependency is metadata rather than ownership, an automatic assignment or an executed command. </summary>
        internal enum DependencyKind
        {
            /// <summary> ENDS. Associates related edits for presentation without claiming that an unchanged dependent value was written. </summary>
            Context,
            /// <summary> ENDS. Indicates that changing the source can change this element's interpretation even when its stored value stays equal. </summary>
            Meaning,
            /// <summary> ENDS. Uses a source-dependent predicate for presentation, hidden fields and their values remain in the full description. </summary>
            Visibility
        }

        /// <summary> ENDS. Connects two definitions belonging to the same TModel, records context, meaning or visibility without owning data or applying dependent changes. </summary>
        internal sealed class NodeDependencyDefinition<TModel> where TModel : class
        {
            private readonly NodeElementDefinition<TModel> _source;
            private readonly DependencyKind _kind;
            private readonly Func<TModel, bool> _visibleWhen;

            /// <summary> ENDS. Returns the existing definition this element depends on, the source belongs to the same model description. </summary>
            internal NodeElementDefinition<TModel> Source => _source;
            /// <summary> ENDS. Returns whether this link affects context, meaning or visibility, consumers decide how that information is presented. </summary>
            internal DependencyKind Kind => _kind;

            /// <summary> ENDS. Stores source and kind, requires a predicate only for Visibility and performs no dependent edit. </summary>
            internal NodeDependencyDefinition(NodeElementDefinition<TModel> source, DependencyKind kind, Func<TModel, bool> visibleWhen = null)
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                if ((kind == DependencyKind.Visibility) != (visibleWhen != null)) throw new ArgumentException("Only visibility dependencies require a visibility predicate.");

                _source = source;
                _kind = kind;
                _visibleWhen = visibleWhen;
            }

            /// <summary> ENDS. Evaluates this link's predicate for the supplied model, context and meaning dependencies always allow visibility. </summary>
            internal bool IsVisible(TModel model)
            {
                if (_visibleWhen == null) return true;

                return _visibleWhen(model);
            }
        }

        /// <summary> ENDS. Common API for one field or logical block in TModel, lets consumers compare and traverse different element kinds without repeating type-specific branches. </summary>
        internal abstract class NodeElementDefinition<TModel> where TModel : class
        {
            private readonly string _name;
            private readonly string _label;
            private readonly FieldRole _role;
            private readonly IReadOnlyList<NodeDependencyDefinition<TModel>> _dependencies;
            private NodeElementDefinition<TModel> _parent;
            private bool _isBound;

            /// <summary> ENDS. Returns the selected member name or a logical group's local name, distinct from the human-readable Label. </summary>
            internal string Name => _name;
            /// <summary> ENDS. Returns the display caption, never used to resolve a model member or select a model type. </summary>
            internal string Label => _label;
            /// <summary> ENDS. Returns content, identity or diagnostic classification, this metadata does not determine whether the selected storage has a setter. </summary>
            internal FieldRole Role => _role;
            /// <summary> ENDS. Returns immutable links to other definitions, dependency relationships are separate from containment in Children. </summary>
            internal IReadOnlyList<NodeDependencyDefinition<TModel>> Dependencies => _dependencies;
            /// <summary> ENDS. Returns logical children owned by the same TModel, nested objects and collection items expose their own model structures separately. </summary>
            internal virtual IReadOnlyList<NodeElementDefinition<TModel>> Children => Array.Empty<NodeElementDefinition<TModel>>();
            /// <summary> ENDS. Returns metadata for a selected model member, logical groups and Value return null because they add no stored member. </summary>
            internal virtual MemberInfo SourceMember => null;
            /// <summary> ENDS. Returns the logical containing element in the same model, null for roots, dependency links and nested-model schemas do not change this ownership. </summary>
            internal NodeElementDefinition<TModel> Parent => _parent;
            /// <summary> ENDS. Reports whether this element already belongs to a frozen model description, including roots whose Parent is null. </summary>
            internal bool IsBound => _isBound;

            /// <summary> ENDS. Stores metadata and immutable dependencies, provides the common base without allocating model instances. </summary>
            protected NodeElementDefinition(string name, string label, FieldRole role, params NodeDependencyDefinition<TModel>[] dependencies)
            {
                _name = name;
                _label = label;
                _role = role;
                _dependencies = Array.AsReadOnly((NodeDependencyDefinition<TModel>[])(dependencies ?? Array.Empty<NodeDependencyDefinition<TModel>>()).Clone());
            }

            /// <summary> ENDS. Compares this element in two caller-supplied states, implementations handle fields, logical values and nested content without creating snapshots. </summary>
            internal abstract bool HasSameValue(TModel originalModel, TModel currentModel);

            /// <summary> ENDS. Evaluates own dependencies and inherited containment visibility, inactive branches hide descendants without erasing their stored values. </summary>
            internal bool IsVisible(TModel model)
            {
                if (_parent != null && (!_parent.IsVisible(model) || !_parent.IsChildActive(this, model))) return false;

                foreach (NodeDependencyDefinition<TModel> dependency in _dependencies)
                {
                    if (!dependency.IsVisible(model)) return false;
                }

                return true;
            }

            /// <summary> ENDS. Binds containment once after validation, the same element cannot be owned by multiple blocks or model descriptions. </summary>
            internal void BindParent(NodeElementDefinition<TModel> parent)
            {
                if (_isBound) throw new InvalidOperationException("Structure element already has an owner: " + Name);

                _parent = parent;
                _isBound = true;
            }

            /// <summary> ENDS. Selects active children independently of their own predicates, ordinary containers permit all children and variants override selection. </summary>
            internal virtual bool IsChildActive(NodeElementDefinition<TModel> child, TModel model) => true;

            /// <summary> ENDS. Visits same-model children, nested objects and collections override this boundary to supply their typed child models. </summary>
            internal virtual void TraverseChildren(TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode, NodeAddress address)
            {
                foreach (NodeElementDefinition<TModel> child in Children)
                {
                    TraverseElement(child, model, visitor, mode, address);
                }
            }

            /// <summary> ENDS. Exposes referenced child schemas without model instances, ordinary same-model blocks add no schema boundary. </summary>
            internal virtual void VisitNestedDefinitions(NodeDefinitionVisitor visitor, HashSet<object> visited) { }

            /// <summary> ENDS. Enumerates visible logical children, specialized Value selects exactly one typed alternative. </summary>
            internal virtual IEnumerable<NodeElementDefinition<TModel>> ActiveChildren(TModel model)
            {
                foreach (NodeElementDefinition<TModel> child in Children)
                {
                    if (child.IsVisible(model)) yield return child;
                }
            }
        }

        #endregion COMMON ELEMENTS AND DEPENDENCIES

        #region TYPED MEMBER ACCESS

        /// <summary> ENDS. Lets backend adapters traverse stored fields with different value types, typed callers continue using NodeFieldDefinition.Read and Write. </summary>
        internal interface INodeStoredField<TModel> where TModel : class
        {
            /// <summary> ENDS. Returns the actual storage type so serialization does not infer types from names or displayed values. </summary>
            Type ValueType { get; }
            /// <summary> ENDS. Reports physical write access, operation permissions remain the responsibility of Commands and Rules. </summary>
            bool IsReadOnly { get; }
            /// <summary> ENDS. Reads the complete stored value through cached typed access, visibility does not filter storage. </summary>
            object ReadStoredValue(TModel model);
            /// <summary> ENDS. Restores an already typed stored value, performs no validation, Undo registration or saving. </summary>
            void WriteStoredValue(TModel model, object value);
        }

        /// <summary> ENDS. Describes one TValue member of TModel, derives member identity and cached access from a single selector, specialized blocks override comparison. </summary>
        internal class NodeFieldDefinition<TModel, TValue> : NodeElementDefinition<TModel>, INodeStoredField<TModel> where TModel : class
        {
            private readonly MemberInfo _member;
            private readonly Func<TModel, TValue> _read;
            private readonly Action<TModel, TValue> _write;

            /// <summary> ENDS. Returns the actual selected field or property, member name and read/write access originate from this same selection. </summary>
            internal override MemberInfo SourceMember => _member;
            /// <summary> ENDS. Returns the compile-time TValue type, not inferred from a string name or a current value. </summary>
            internal Type ValueType => typeof(TValue);
            /// <summary> ENDS. Reports whether the selected storage lacks a setter, readonly fields and getter-only properties are read-only, FieldRole does not affect this result. </summary>
            internal bool IsReadOnly => _write == null;

            /// <summary> ENDS. Exposes the existing typed storage metadata to the heterogeneous backend adapter. </summary>
            Type INodeStoredField<TModel>.ValueType => ValueType;
            /// <summary> ENDS. Exposes physical write access without treating identity metadata as a setter restriction. </summary>
            bool INodeStoredField<TModel>.IsReadOnly => IsReadOnly;

            /// <summary> ENDS. Accepts one direct field/property selector, derives its name and compiles access once, creates a setter whenever physical storage permits regardless of FieldRole. </summary>
            internal NodeFieldDefinition(Expression<Func<TModel, TValue>> selector, string label, FieldRole role = FieldRole.Content, NodeDependencyDefinition<TModel>[] dependencies = null) : base(GetMemberExpression(selector).Member.Name, label, role, dependencies)
            {
                MemberExpression selectedMember = GetMemberExpression(selector);
                _member = selectedMember.Member;
                _read = selector.Compile();

                if (_member is FieldInfo field && field.IsInitOnly) return;
                if (_member is PropertyInfo property && property.SetMethod == null) return;

                ParameterExpression newValue = Expression.Parameter(typeof(TValue), "newValue");
                BinaryExpression assignment = Expression.Assign(selectedMember, newValue);
                _write = Expression.Lambda<Action<TModel, TValue>>(Expression.Block(assignment, Expression.Empty()), selector.Parameters[0], newValue).Compile();
            }

            private static MemberExpression GetMemberExpression(Expression<Func<TModel, TValue>> selector)
            {
                if (selector == null) throw new ArgumentNullException(nameof(selector));
                if (!(selector.Body is MemberExpression selectedMember) || selectedMember.Expression != selector.Parameters[0]) throw new ArgumentException("Select one direct model field or property.");

                return selectedMember;
            }

            /// <summary> ENDS. Reads TValue from the supplied model using the cached delegate, returns the stored value and creates no model copy. </summary>
            internal TValue Read(TModel model) => _read(model);

            /// <summary> ENDS. Assigns TValue using the cached setter, throws if read-only, caller owns Rules, Undo, revisions and persistence. </summary>
            internal void Write(TModel model, TValue newValue)
            {
                if (_write == null) throw new InvalidOperationException("Field is read-only: " + Name);

                _write(model, newValue);
            }

            /// <summary> ENDS. Bridges the cached typed getter to backend traversal without member lookup or snapshot creation. </summary>
            object INodeStoredField<TModel>.ReadStoredValue(TModel model) => Read(model);

            /// <summary> ENDS. Casts the backend value to TValue and uses the same cached setter as typed callers. </summary>
            void INodeStoredField<TModel>.WriteStoredValue(TModel model, object value) => Write(model, (TValue)value);

            /// <summary> ENDS. Reads this member from both supplied states and compares TValue without display-text conversion or model snapshots. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel) => EqualityComparer<TValue>.Default.Equals(Read(originalModel), Read(currentModel));
        }

        #endregion TYPED MEMBER ACCESS

        #region LOGICAL VALUES AND GROUPS

        /// <summary> ENDS. Describes one logical Value backed by four typed fields, compares the selected type and value while preserving access to every stored alternative. </summary>
        internal sealed class NodeVariableValueDefinition<TModel> : NodeElementDefinition<TModel> where TModel : class
        {
            private readonly IReadOnlyList<NodeElementDefinition<TModel>> _alternatives;

            /// <summary> ENDS. Returns the shared variable-type definition that selects and interprets the active stored value. </summary>
            internal NodeFieldDefinition<TModel, ConditionVariableType> TypeField { get; }
            /// <summary> ENDS. Returns the BoolValue definition, always accessible but presented as Value only when TypeField selects Bool. </summary>
            internal NodeFieldDefinition<TModel, bool> BoolValue { get; }
            /// <summary> ENDS. Returns the IntValue definition, always accessible but presented as Value only when TypeField selects Int. </summary>
            internal NodeFieldDefinition<TModel, int> IntValue { get; }
            /// <summary> ENDS. Returns the FloatValue definition, always accessible but presented as Value only when TypeField selects Float. </summary>
            internal NodeFieldDefinition<TModel, float> FloatValue { get; }
            /// <summary> ENDS. Returns the StringValue definition, always accessible but presented as Value only when TypeField selects String. </summary>
            internal NodeFieldDefinition<TModel, string> StringValue { get; }
            /// <summary> ENDS. Returns all four stored value definitions for full traversal, ActiveChildren returns the single presented alternative. </summary>
            internal override IReadOnlyList<NodeElementDefinition<TModel>> Children => _alternatives;

            /// <summary> ENDS. Connects the type selector to four existing value definitions, retains every alternative while presenting one logical Value. </summary>
            internal NodeVariableValueDefinition(NodeFieldDefinition<TModel, ConditionVariableType> type, NodeFieldDefinition<TModel, bool> boolValue, NodeFieldDefinition<TModel, int> intValue,
                NodeFieldDefinition<TModel, float> floatValue, NodeFieldDefinition<TModel, string> stringValue, params NodeDependencyDefinition<TModel>[] dependencies) : base("Value", "Value", FieldRole.Content, dependencies)
            {
                TypeField = type;
                BoolValue = boolValue;
                IntValue = intValue;
                FloatValue = floatValue;
                StringValue = stringValue;
                _alternatives = Array.AsReadOnly(new NodeElementDefinition<TModel>[] { BoolValue, IntValue, FloatValue, StringValue });
            }

            /// <summary> ENDS. Returns the existing typed definition selected by the model's variable type, rejects unsupported enum values. </summary>
            internal NodeElementDefinition<TModel> ActiveField(TModel model)
            {
                switch (TypeField.Read(model))
                {
                    case ConditionVariableType.Bool: return BoolValue;
                    case ConditionVariableType.Int: return IntValue;
                    case ConditionVariableType.Float: return FloatValue;
                    case ConditionVariableType.String: return StringValue;
                    default: throw new InvalidOperationException("Unsupported variable type.");
                }
            }

            /// <summary> ENDS. Makes direct visibility queries agree with logical traversal by allowing only the selected typed value alternative. </summary>
            internal override bool IsChildActive(NodeElementDefinition<TModel> child, TModel model) => ReferenceEquals(child, ActiveField(model));

            /// <summary> ENDS. Enumerates exactly the selected value definition, ordinary visibility filtering would not express this type-dependent choice. </summary>
            internal override IEnumerable<NodeElementDefinition<TModel>> ActiveChildren(TModel model)
            {
                yield return ActiveField(model);
            }

            /// <summary> ENDS. Compares variable type first and then selected value, Int 7 and String 7 differ while inactive-alternative edits do not. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel)
            {
                if (!TypeField.HasSameValue(originalModel, currentModel)) return false;

                return ActiveField(originalModel).HasSameValue(originalModel, currentModel);
            }
        }

        /// <summary> ENDS. Groups existing same-model definitions for presentation and comparison, adds no model field and does not override child highlight ownership. </summary>
        internal sealed class NodeGroupDefinition<TModel> : NodeElementDefinition<TModel> where TModel : class
        {
            private readonly IReadOnlyList<NodeElementDefinition<TModel>> _children;

            /// <summary> ENDS. Returns immutable references to existing children, these objects are shared with named accessors rather than duplicated. </summary>
            internal override IReadOnlyList<NodeElementDefinition<TModel>> Children => _children;

            /// <summary> ENDS. Freezes references to existing children, the logical name and display label do not create serialized storage. </summary>
            internal NodeGroupDefinition(string name, string label, NodeElementDefinition<TModel>[] children, params NodeDependencyDefinition<TModel>[] dependencies) : base(name, label, FieldRole.Content, dependencies)
            {
                _children = Array.AsReadOnly((NodeElementDefinition<TModel>[])children.Clone());
            }

            /// <summary> ENDS. Compares child content visible in either state, adds no stored value, hidden children remain accessible through Children. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel) => NodeModelDefinition<TModel>.HaveSameMembers(_children, originalModel, currentModel);
        }

        #endregion LOGICAL VALUES AND GROUPS

        #region CONDITIONAL BRANCHES

        /// <summary> ENDS. Provides access to a variant's existing selector when a consumer knows the owner model but not the selector value type, adds no property to ordinary elements. </summary>
        internal interface INodeVariantDefinition<TModel> where TModel : class
        {
            /// <summary> ENDS. Returns the same selector definition exposed by the concrete variant, generic consumers can follow branch dependencies without enum-specific cases. </summary>
            NodeElementDefinition<TModel> Selector { get; }
        }

        /// <summary> ENDS. Defines one selector value and its same-model content, nested object and collection descriptions can connect independent parameter models to this branch. </summary>
        internal sealed class NodeBranchDefinition<TModel, TValue> : NodeElementDefinition<TModel> where TModel : class
        {
            private readonly IReadOnlyList<NodeElementDefinition<TModel>> _children;

            /// <summary> ENDS. Returns the typed selector value activating this branch, display captions never participate in selection. </summary>
            internal TValue Value { get; }
            /// <summary> ENDS. Returns all registered branch content, children inherit selection from their containing variant. </summary>
            internal override IReadOnlyList<NodeElementDefinition<TModel>> Children => _children;

            /// <summary> ENDS. Freezes branch content without creating data or owning runtime execution, an empty branch is valid for actions such as Hide. </summary>
            internal NodeBranchDefinition(string name, string label, TValue value, params NodeElementDefinition<TModel>[] children) : base(name, label, FieldRole.Content)
            {
                if (children == null) throw new ArgumentNullException(nameof(children));

                Value = value;
                _children = Array.AsReadOnly((NodeElementDefinition<TModel>[])children.Clone());
            }

            /// <summary> ENDS. Compares branch content visible in either state, branch selection is compared by the containing variant. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel) => NodeModelDefinition<TModel>.HaveSameMembers(Children, originalModel, currentModel);
        }

        /// <summary> ENDS. Connects a typed selector to registered branches, new branch content participates in common traversal without adding action-specific switches to consumers. </summary>
        internal sealed class NodeVariantDefinition<TModel, TValue> : NodeElementDefinition<TModel>, INodeVariantDefinition<TModel> where TModel : class
        {
            private readonly IReadOnlyList<NodeBranchDefinition<TModel, TValue>> _branches;
            private readonly IReadOnlyList<NodeElementDefinition<TModel>> _children;

            /// <summary> ENDS. Returns the existing stored selector, this exact definition is also the first child and remains accessible through a concrete named property. </summary>
            internal NodeFieldDefinition<TModel, TValue> Selector { get; }
            /// <summary> ENDS. Exposes the existing typed selector through the variant contract without storing or creating another definition. </summary>
            NodeElementDefinition<TModel> INodeVariantDefinition<TModel>.Selector => Selector;
            /// <summary> ENDS. Returns immutable registered alternatives, selector values must be unique while empty content is allowed. </summary>
            internal IReadOnlyList<NodeBranchDefinition<TModel, TValue>> Branches => _branches;
            /// <summary> ENDS. Returns the selector and every branch for storage traversal, ActiveChildren exposes the selector and selected branch only. </summary>
            internal override IReadOnlyList<NodeElementDefinition<TModel>> Children => _children;

            /// <summary> ENDS. Registers selector and alternatives once, validates nulls and duplicate values, model construction later validates ownership and dependencies. </summary>
            internal NodeVariantDefinition(string name, string label, NodeFieldDefinition<TModel, TValue> selector, params NodeBranchDefinition<TModel, TValue>[] branches) : base(name, label, FieldRole.Content)
            {
                if (selector == null) throw new ArgumentNullException(nameof(selector));
                if (branches == null || branches.Length == 0) throw new ArgumentException("A variant requires at least one branch.", nameof(branches));

                var values = new HashSet<TValue>(EqualityComparer<TValue>.Default);
                var children = new List<NodeElementDefinition<TModel>> { selector };

                foreach (NodeBranchDefinition<TModel, TValue> branch in branches)
                {
                    if (branch == null) throw new ArgumentException("Branch cannot be null.", nameof(branches));
                    if (!values.Add(branch.Value)) throw new ArgumentException("Variant selector values must be unique.", nameof(branches));

                    children.Add(branch);
                }

                Selector = selector;
                _branches = Array.AsReadOnly((NodeBranchDefinition<TModel, TValue>[])branches.Clone());
                _children = children.AsReadOnly();
            }

            /// <summary> ENDS. Finds the registered branch for the actual typed value, unsupported values fail explicitly instead of silently hiding content. </summary>
            internal NodeBranchDefinition<TModel, TValue> ActiveBranch(TModel model)
            {
                TValue value = Selector.Read(model);

                foreach (NodeBranchDefinition<TModel, TValue> branch in _branches)
                {
                    if (EqualityComparer<TValue>.Default.Equals(branch.Value, value)) return branch;
                }

                throw new InvalidOperationException("Unregistered selector value in " + Name + ": " + value);
            }

            /// <summary> ENDS. Keeps the selector visible and permits exactly the active branch, descendants inherit this selection through their containment parent. </summary>
            internal override bool IsChildActive(NodeElementDefinition<TModel> child, TModel model) => ReferenceEquals(child, Selector) || ReferenceEquals(child, ActiveBranch(model));

            /// <summary> ENDS. Compares selector identity before active branch content, switching branches counts even when their displayed values happen to match. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel)
            {
                if (!Selector.HasSameValue(originalModel, currentModel)) return false;

                return ActiveBranch(originalModel).HasSameValue(originalModel, currentModel);
            }
        }

        #endregion CONDITIONAL BRANCHES

        #region NESTED OBJECTS AND COLLECTIONS

        /// <summary> ENDS. Connects a TModel member to TChild data and its concrete TStructure description, preserves named nested access and compares child content rather than references. </summary>
        internal sealed class NodeObjectDefinition<TModel, TChild, TStructure> : NodeFieldDefinition<TModel, TChild> where TModel : class where TChild : class where TStructure : NodeModelDefinition<TChild>
        {
            private readonly TStructure _structure;

            /// <summary> ENDS. Returns the concrete shared TStructure with its named fields, child definitions operate on TChild rather than parent TModel, no cast is needed. </summary>
            internal TStructure Structure => _structure;

            /// <summary> ENDS. Binds one object member to a concrete shared TStructure describing TChild, inherits typed member access for the parent model. </summary>
            internal NodeObjectDefinition(Expression<Func<TModel, TChild>> selector, string label, TStructure structure, params NodeDependencyDefinition<TModel>[] dependencies) : base(selector, label, dependencies: dependencies)
            {
                if (structure == null) throw new ArgumentNullException(nameof(structure));

                _structure = structure;
            }

            /// <summary> ENDS. Compares nested objects through their child structure, handles null children and compares content rather than references alone. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel) => _structure.HasSameValue(Read(originalModel), Read(currentModel));

            /// <summary> ENDS. Crosses the object boundary with its actual child model, visitors receive null children without attempting to read their fields. </summary>
            internal override void TraverseChildren(TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode, NodeAddress address) => TraverseModel(_structure, Read(model), visitor, mode, address);

            /// <summary> ENDS. Discovers this object's child description even when every actual object value is null. </summary>
            internal override void VisitNestedDefinitions(NodeDefinitionVisitor visitor, HashSet<object> visited) => VisitDefinition(_structure, visitor, visited);
        }

        /// <summary> ENDS. Connects a list to TItem data and its concrete TStructure description, preserves named item access, order and an optional existing row key. </summary>
        internal sealed class NodeCollectionDefinition<TModel, TItem, TStructure> : NodeFieldDefinition<TModel, List<TItem>> where TModel : class where TItem : class where TStructure : NodeModelDefinition<TItem>
        {
            private readonly TStructure _items;
            private readonly NodeFieldDefinition<TItem, string> _key;

            /// <summary> ENDS. Returns the concrete shared TStructure with named item fields, contains TItem definitions rather than row instances, no cast is needed. </summary>
            internal TStructure ItemStructure => _items;
            /// <summary> ENDS. Returns an existing string identity definition for matching rows, null means ordered rows without a persistent key. </summary>
            internal NodeFieldDefinition<TItem, string> ItemKey => _key;

            // A null key means ordered items without a persistent identity supplied by their model.
            /// <summary> ENDS. Binds one list to its concrete TStructure describing TItem, verifies that an optional key belongs to those definitions and preserves existing row identity. </summary>
            internal NodeCollectionDefinition(Expression<Func<TModel, List<TItem>>> selector, string label, TStructure items, NodeFieldDefinition<TItem, string> key = null, params NodeDependencyDefinition<TModel>[] dependencies) : base(selector, label, dependencies: dependencies)
            {
                if (items == null) throw new ArgumentNullException(nameof(items));

                _items = items;
                _key = key;

                if (key != null && !new HashSet<NodeElementDefinition<TItem>>(items.ElementsDepthFirst()).Contains(key)) throw new ArgumentException("Collection key must belong to its item structure.");
            }

            /// <summary> ENDS. Compares nullness, count, order and logical item content, ItemKey describes identity but does not make list order irrelevant. </summary>
            internal override bool HasSameValue(TModel originalModel, TModel currentModel)
            {
                List<TItem> originalItems = Read(originalModel);
                List<TItem> currentItems = Read(currentModel);

                if (ReferenceEquals(originalItems, currentItems)) return true;
                if (originalItems == null || currentItems == null || originalItems.Count != currentItems.Count) return false;

                for (int i = 0; i < originalItems.Count; i++)
                {
                    if (!_items.HasSameValue(originalItems[i], currentItems[i])) return false;
                }

                return true;
            }

            /// <summary> ENDS. Visits ordered rows with their existing key when present, unkeyed rows use an explicit positional address and no identities are generated. </summary>
            internal override void TraverseChildren(TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode, NodeAddress address)
            {
                List<TItem> items = Read(model);

                if (items == null) return;

                HashSet<string> keys = null;

                if (_key != null && visitor.RequireUniqueKeys) keys = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < items.Count; i++)
                {
                    TItem item = items[i];
                    string key = null;

                    if (item != null && _key != null) key = _key.Read(item);
                    if (!string.IsNullOrEmpty(key) && keys != null && !keys.Add(key)) throw new InvalidOperationException("Duplicate collection key in " + Name + ": " + key);

                    TraverseModel(_items, item, visitor, mode, address.Item(i, key));
                }
            }

            /// <summary> ENDS. Discovers the item description independently of actual rows, empty and null collections require no special backend registration list. </summary>
            internal override void VisitNestedDefinitions(NodeDefinitionVisitor visitor, HashSet<object> visited) => VisitDefinition(_items, visitor, visited);
        }

        #endregion NESTED OBJECTS AND COLLECTIONS

        #region MODEL TRAVERSAL AND DEFINITION VALIDATION

        /// <summary> ENDS. Common API for one model description, gives concrete named definitions an immutable traversal and comparison surface without owning model instances or snapshots. </summary>
        internal abstract class NodeModelDefinition<TModel> where TModel : class
        {
            private IReadOnlyList<NodeReferenceDefinition<TModel>> _references = Array.Empty<NodeReferenceDefinition<TModel>>();

            /// <summary> ENDS. Returns immutable top-level definitions, including hidden blocks, named accessors expose these same objects rather than a second schema. </summary>
            internal abstract IReadOnlyList<NodeElementDefinition<TModel>> Members { get; }

            /// <summary> ENDS. Returns immutable reference and port semantics attached to this model's existing typed fields. </summary>
            internal IReadOnlyList<NodeReferenceDefinition<TModel>> References => _references;

            /// <summary> ENDS. Registers references after Members, verifies that all supplied accessors belong to this schema and freezes the descriptions. </summary>
            protected void DefineReferences(params NodeReferenceDefinition<TModel>[] references)
            {
                if (references == null) throw new ArgumentNullException(nameof(references));

                var fields = new HashSet<NodeElementDefinition<TModel>>(ElementsDepthFirst());
                var declared = new HashSet<NodeElementDefinition<TModel>>();

                foreach (NodeReferenceDefinition<TModel> reference in references)
                {
                    if (reference == null) throw new ArgumentException("Reference definition cannot be null.");
                    if (!declared.Add(reference.Field)) throw new ArgumentException("Reference field is already described.");
                    if (!fields.Contains(reference.Field)) throw new ArgumentException("Reference field must belong to its model schema.");
                    if (reference.Qualifier != null && !fields.Contains(reference.Qualifier)) throw new ArgumentException("Reference qualifier must belong to its model schema.");
                    if (reference.VariableType != null && !fields.Contains(reference.VariableType)) throw new ArgumentException("Reference namespace must belong to its model schema.");
                    if (reference.State != null && (!fields.Contains(reference.State) || !fields.Contains(reference.MissingLabel))) throw new ArgumentException("Reference diagnostics must belong to their model schema.");
                }

                _references = Array.AsReadOnly((NodeReferenceDefinition<TModel>[])references.Clone());
            }

            /// <summary> ENDS. Creates a meaning dependency, records interpretation changes without assigning a dependent value. </summary>
            protected static NodeDependencyDefinition<TModel> Meaning(NodeElementDefinition<TModel> source) => new NodeDependencyDefinition<TModel>(source, DependencyKind.Meaning);
            /// <summary> ENDS. Creates a contextual link for grouping related edits, an unchanged dependent value is not automatically another change. </summary>
            protected static NodeDependencyDefinition<TModel> Context(NodeElementDefinition<TModel> source) => new NodeDependencyDefinition<TModel>(source, DependencyKind.Context);
            /// <summary> ENDS. Creates a visibility link with an explicit predicate, hidden storage remains part of the complete description. </summary>
            protected static NodeDependencyDefinition<TModel> VisibleWhen(NodeElementDefinition<TModel> source, Func<TModel, bool> predicate) => new NodeDependencyDefinition<TModel>(source, DependencyKind.Visibility, predicate);

            /// <summary> ENDS. Freezes definitions and validates unique names, dependency ownership and cycles, called once while constructing a concrete description. </summary>
            protected static IReadOnlyList<NodeElementDefinition<TModel>> DefineMembers(params NodeElementDefinition<TModel>[] members)
            {
                if (members == null) throw new ArgumentNullException(nameof(members));

                var immutableMembers = Array.AsReadOnly((NodeElementDefinition<TModel>[])members.Clone());
                var elements = new HashSet<NodeElementDefinition<TModel>>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                var visited = new HashSet<NodeElementDefinition<TModel>>();

                foreach (NodeElementDefinition<TModel> element in EnumerateElements(immutableMembers))
                {
                    if (element.IsBound) throw new InvalidOperationException("Structure element already belongs to a description: " + element.Name);
                    if (string.IsNullOrEmpty(element.Name) || !names.Add(element.Name)) throw new InvalidOperationException("Empty or duplicate structure member in " + typeof(TModel).Name + ": " + element.Name);

                    elements.Add(element);
                }

                foreach (NodeElementDefinition<TModel> element in elements)
                {
                    ValidateDependencies(element, elements, new HashSet<NodeElementDefinition<TModel>>(), visited);
                }

                BindParents(immutableMembers, null);

                return immutableMembers;
            }

            /// <summary> ENDS. Walks typed model instances through logical, object and collection boundaries, Stored includes hidden data while Active follows visibility and selected branches. </summary>
            internal void Traverse(TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode = NodeTraversalMode.Active)
            {
                if (visitor == null) throw new ArgumentNullException(nameof(visitor));
                if (mode != NodeTraversalMode.Active && mode != NodeTraversalMode.Stored) throw new ArgumentOutOfRangeException(nameof(mode));

                TraverseModel(this, model, visitor, mode, NodeAddress.Root);
            }

            /// <summary> ENDS. Visits all reachable descriptions without reading or allocating model instances, shared nested schemas are visited once by reference in this walk. </summary>
            internal void VisitDefinitions(NodeDefinitionVisitor visitor)
            {
                if (visitor == null) throw new ArgumentNullException(nameof(visitor));

                VisitDefinition(this, visitor, new HashSet<object>());
            }

            // Full traversal includes hidden payloads and inactive value alternatives.
            // Backend adapters resolve serialized property aliases, logical groups never introduce extra stored fields.
            /// <summary> ENDS. Enumerates all definitions and logical children in this model, including hidden data, child-object and item structures are traversed separately. </summary>
            internal IEnumerable<NodeElementDefinition<TModel>> ElementsDepthFirst() => EnumerateElements(Members);

            /// <summary> ENDS. Enumerates top-level definitions visible in the supplied state, use each element's ActiveChildren for logical children. </summary>
            internal IEnumerable<NodeElementDefinition<TModel>> ActiveMembers(TModel model)
            {
                foreach (NodeElementDefinition<TModel> member in Members)
                {
                    if (member.IsVisible(model)) yield return member;
                }
            }

            // Logical comparison includes a member when it is visible in either state, it is not a serialization diff.
            /// <summary> ENDS. Compares supplied states and content visible in either state, handles null models, creates no snapshots and is not a serialization diff. </summary>
            internal bool HasSameValue(TModel originalModel, TModel currentModel)
            {
                if (ReferenceEquals(originalModel, currentModel)) return true;
                if (originalModel == null || currentModel == null) return false;

                return HaveSameMembers(Members, originalModel, currentModel);
            }

            /// <summary> ENDS. Compares definitions against two non-null model states, skips elements hidden in both states and leaves snapshots owned by the caller. </summary>
            internal static bool HaveSameMembers(IReadOnlyList<NodeElementDefinition<TModel>> members, TModel originalModel, TModel currentModel)
            {
                foreach (NodeElementDefinition<TModel> member in members)
                {
                    if (!member.IsVisible(originalModel) && !member.IsVisible(currentModel)) continue;
                    if (!member.HasSameValue(originalModel, currentModel)) return false;
                }

                return true;
            }

            private static IEnumerable<NodeElementDefinition<TModel>> EnumerateElements(IReadOnlyList<NodeElementDefinition<TModel>> members)
            {
                foreach (NodeElementDefinition<TModel> member in members)
                {
                    if (member == null) throw new InvalidOperationException("Structure elements cannot be null.");

                    yield return member;

                    foreach (NodeElementDefinition<TModel> child in EnumerateElements(member.Children))
                    {
                        yield return child;
                    }
                }
            }

            private static void BindParents(IReadOnlyList<NodeElementDefinition<TModel>> members, NodeElementDefinition<TModel> parent)
            {
                foreach (NodeElementDefinition<TModel> member in members)
                {
                    member.BindParent(parent);
                    BindParents(member.Children, member);
                }
            }

            private static void ValidateDependencies(NodeElementDefinition<TModel> element, HashSet<NodeElementDefinition<TModel>> elements, HashSet<NodeElementDefinition<TModel>> visiting, HashSet<NodeElementDefinition<TModel>> visited)
            {
                if (visited.Contains(element)) return;
                if (!visiting.Add(element)) throw new InvalidOperationException("Cyclic structure dependency: " + element.Name);

                foreach (NodeDependencyDefinition<TModel> dependency in element.Dependencies)
                {
                    if (!elements.Contains(dependency.Source)) throw new InvalidOperationException("Dependency source is outside its structure: " + dependency.Source.Name);

                    ValidateDependencies(dependency.Source, elements, visiting, visited);
                }

                visiting.Remove(element);
                visited.Add(element);
            }
        }

        #endregion MODEL TRAVERSAL AND DEFINITION VALIDATION
    }
}
#endif