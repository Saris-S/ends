using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    [CreateAssetMenu(fileName = "Dialog Database", menuName = "Easy Node Dialog System/Dialog Database")]
    public class DialogDatabase : ScriptableObject
    {
        [FormerlySerializedAs("CurrentLanguage")][SerializeField] private string _currentLanguage = "en";
        [FormerlySerializedAs("Categories")][SerializeField] private List<string> _categories;
        [FormerlySerializedAs("Dialogs")][SerializeField] private List<DialogEntry> _dialogs;
        [FormerlySerializedAs("AvailableLanguages")][SerializeField] private List<string> _availableLanguages = new List<string> { "ru", "en" };
        [FormerlySerializedAs("CharacterNameColors")][SerializeField] private List<CharacterNameColor> _characterNameColors;
        [SerializeField] private List<VariableDefinition> _variables = new List<VariableDefinition>();
        [SerializeField] private List<CharacterGroup> _characterGroups = new List<CharacterGroup>();
        [SerializeField] private bool _voiceClipsEnabled = true;
        [SerializeField] private int _nextNodeNumber = 1;
        [SerializeField] private int _nodeIdentityVersion;

        public string CurrentLanguage { get => _currentLanguage; set => _currentLanguage = value; }
        public List<string> Categories { get => _categories; set => _categories = value; }
        public List<DialogEntry> Dialogs { get => _dialogs; set => _dialogs = value; }
        public List<string> AvailableLanguages { get => _availableLanguages; set => _availableLanguages = value; }
        public List<CharacterNameColor> CharacterNameColors { get => _characterNameColors; set => _characterNameColors = value; }
        public IReadOnlyList<VariableDefinition> Variables => _variables;
        public List<CharacterGroup> CharacterGroups { get => _characterGroups; set => _characterGroups = value; }
        public bool VoiceClipsEnabled { get => _voiceClipsEnabled; set => _voiceClipsEnabled = value; }
        public int NextNodeNumber => _nextNodeNumber;
        public int NodeIdentityVersion => _nodeIdentityVersion;

        public DialogNode GetStartNode(string dialogName)
        {
            DialogEntry dialog = Dialogs.Find(d => d.dialogName == dialogName);
            return dialog?.nodes.Find(n => n != null && n.NodeType == NodeType.Start);
        }

        public string GetNodeText(DialogNode node)
        {
            if (node.NodeType == NodeType.Line)
            {
                string text = node.LineData.GetTextForLanguage(CurrentLanguage);

                if (!string.IsNullOrEmpty(text)) return text;

                string english = node.LineData.GetTextForLanguage("en");

                if (!string.IsNullOrEmpty(english)) return english;

                return "Text not found!";
            }
            return "";
        }

        public Sprite GetSprite(string characterName, string spriteName)
        {
            if (_characterGroups == null) return null;

            CharacterGroup group = _characterGroups.FirstOrDefault(g => g != null && g.characterName == characterName);

            if (group == null)
            {
                Debug.LogError($"Character '{characterName}' not found in the database!");

                return null;
            }

            CharacterSprite sprite = group.sprites?.FirstOrDefault(s => s != null && s.spriteName == spriteName);

            if (sprite == null)
            {
                Debug.LogWarning($"Sprite '{spriteName}' not found for character '{characterName}'!");

                return null;
            }

            return sprite.sprite;
        }

        public string[] GetVariableNamesByType(ConditionVariableType type)
        {
            var list = new List<string>();

            if (_variables == null) return list.ToArray();

            foreach (VariableDefinition v in _variables)
            {
                if (v != null && v.variableType == type) list.Add(v.variableName);
            }

            return list.ToArray();
        }

        public string[] GetBoolVariableNames() => GetVariableNamesByType(ConditionVariableType.Bool);
        public string[] GetIntVariableNames() => GetVariableNamesByType(ConditionVariableType.Int);
        public string[] GetFloatVariableNames() => GetVariableNamesByType(ConditionVariableType.Float);
        public string[] GetStringVariableNames() => GetVariableNamesByType(ConditionVariableType.String);

#if UNITY_EDITOR
        /// <summary> Advances editor-owned identity metadata without reusing allocated node numbers. </summary>
        public void SetNodeIdentityState(int nextNumber, int version)
        {
            _nextNodeNumber = Math.Max(_nextNodeNumber, nextNumber);
            _nodeIdentityVersion = version;
        }

        public void AddVariable(VariableDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.variableName)) return;
            if (_variables == null) _variables = new List<VariableDefinition>();

            _variables.Add(def);
        }

        public void RemoveVariableAt(int index)
        {
            if (_variables == null) return;
            if (index >= 0 && index < _variables.Count) _variables.RemoveAt(index);
        }

        public void SetVariables(List<VariableDefinition> newList)
        {
            _variables = newList ?? new List<VariableDefinition>();
        }
#endif
    }

    [Serializable]
    public class VariableDefinition
    {
        public string variableName;
        public ConditionVariableType variableType;
        public bool boolDefault;
        public int intDefault;
        public float floatDefault;
        public string stringDefault;

        public object GetDefaultValue()
        {
            if (variableType == ConditionVariableType.Bool) return boolDefault;
            if (variableType == ConditionVariableType.Int) return intDefault;
            if (variableType == ConditionVariableType.Float) return floatDefault;
            if (variableType == ConditionVariableType.String)
            {
                if (stringDefault == null) return "";
                return stringDefault;
            }

            return null;
        }
    }

    [Serializable]
    public class CharacterGroup
    {
        public string characterName;
        public List<CharacterSprite> sprites = new List<CharacterSprite>();
    }

    [Serializable]
    public class CharacterSprite
    {
        public string spriteName;
        public Sprite sprite;
    }

    [Serializable]
    public class DialogEntry
    {
        [HideInInspector][SerializeField] private string _nodeIdentityKey;
        [HideInInspector][SerializeField] private List<int> _nextNodeNumbers = new List<int> { 1, 1, 1, 1, 1 };

#if UNITY_EDITOR
        public string NodeIdentityKey => _nodeIdentityKey;
        public IReadOnlyList<int> NextNodeNumbers => _nextNodeNumbers;

        /// <summary> Sets editor-owned identity scope and the next number of each node type. </summary>
        public void SetNodeIdentityState(string key, IReadOnlyList<int> nextNumbers)
        {
            _nodeIdentityKey = key;
            _nextNodeNumbers = new List<int>(nextNumbers);
        }
#endif

        public string dialogName;
        public string initiator;
        public List<DialogCondition> conditions;
        public bool isAndLogic = true;
        public bool isLocked;
        public List<DialogNode> nodes;
    }

    [Serializable]
    public class DialogNode
    {
        public NodeType NodeType;
        public string nodeId;
        public Vector2 Position;
        public StartNode StartData;
        public LineNode LineData;
        public List<ChoiceNode> ChoiceData;
        public ConditionNode ConditionData;
        public RedirectNode RedirectData;

        public DialogNode()
        {
            ChoiceData = new List<ChoiceNode>();
            ConditionData = new ConditionNode();
            RedirectData = new RedirectNode();
        }
    }

    [Serializable]
    public enum NodeType
    {
        Start,
        Line,
        Choice,
        Condition,
        Redirect
    }

    [Serializable]
    public class StartNode
    {
        public string NextNodeId;

        public StartNode()
        {
            NextNodeId = null;
        }
    }

    [Serializable]
    public class LineNode
    {
        public string CharacterName;
        public DialogReferenceState CharacterReferenceState;
        public string MissingCharacterReferenceLabel;
        public List<CharacterActionData> CharacterActions;
        public List<LineText> DialogsText;
        public string NextNodeId;
        public bool isInstant = false; // Instant node: updates sprites/text and advances without a click

        public string GetTextForLanguage(string language)
        {
            LineText entry = DialogsText.Find(t => t.LanguageCode == language);

            return entry?.Text ?? "";
        }

        public string GetSayerNameForLanguage(string language)
        {
            LineText entry = DialogsText.Find(t => t.LanguageCode == language);

            return !string.IsNullOrEmpty(entry?.SayerName) ? entry.SayerName : CharacterName;
        }

        public AudioClip GetVoiceClipForLanguage(string language)
        {
            LineText entry = DialogsText.Find(t => t.LanguageCode == language);

            if (entry == null) return null;

            return entry.VoiceClip;
        }
    }

    public interface IDialogText
    {
        string LanguageCode { get; set; }
        string Text { get; set; }
    }

    [Serializable]
    public class LineText : IDialogText
    {
        public string LanguageCode;
        public string SayerName;
        [TextArea(3, 3)]public string Text;
        public AudioClip VoiceClip;

        string IDialogText.LanguageCode { get => LanguageCode; set => LanguageCode = value; }
        string IDialogText.Text { get => Text; set => Text = value; }
    }

    [Serializable]
    public sealed class ChoiceText : IDialogText
    {
        [FormerlySerializedAs("LanguageCode")][SerializeField] private string _languageCode;
        [FormerlySerializedAs("Text")][TextArea(3, 3)][SerializeField] private string _text;

        public string LanguageCode { get => _languageCode; set => _languageCode = value; }
        public string Text { get => _text; set => _text = value; }
    }

    [Serializable]
    public class NameTranslation
    {
        public string languageCode;
        public string displayName;
    }

    [Serializable]
    public class CharacterNameColor
    {
        public string Name;
        [SerializeField] private Color _color = UnityEngine.Color.white;

#if UNITY_EDITOR
        // Temporary serialized input for the separate editor legacy migration.
        public const string LEGACY_COLOR_MIGRATED = "__ENDS_COLOR_MIGRATED__";
        [HideInInspector][FormerlySerializedAs("ColorHex")][SerializeField] private string _legacyColorHex = LEGACY_COLOR_MIGRATED;
#endif

        public Color Color { get => _color; set => _color = value; }
        public List<NameTranslation> Translations;

        public CharacterNameColor()
        {
            Translations = new List<NameTranslation>();
        }

        public string GetDisplayNameForLanguage(string languageCode)
        {
            if (Translations == null) return Name ?? "";

            var t = Translations.Find(x => x.languageCode == languageCode);

            return !string.IsNullOrEmpty(t?.displayName) ? t.displayName : (Name ?? "");
        }
    }

    [Serializable]
    public class CharacterActionData : DialogBlockData
    {
        public DialogReferenceState ReferenceState;
        public string MissingReferenceLabel;
        public string CharacterName;
        public string EmotionName;
        public CharacterAction Action;
        public float PosX;
        public float PosY;
        public float Zoom;
    }

    [Serializable]
    public class ConditionNode
    {
        public List<DialogCondition> DialogConditions;
        public bool IsAndLogic;
        public string TrueNodeId;
        public string FalseNodeId;

        public ConditionNode()
        {
            DialogConditions = new List<DialogCondition>();
            IsAndLogic = true;
            TrueNodeId = null;
            FalseNodeId = null;
        }
    }

    [Serializable]
    public class RedirectNode
    {
        public string TargetCategory;
        public string TargetDialogName;
        public DialogReferenceState DialogReferenceState;
        public string MissingDialogReferenceLabel;

        public RedirectNode()
        {
            TargetCategory = "";
            TargetDialogName = "";
            DialogReferenceState = DialogReferenceState.Valid;
            MissingDialogReferenceLabel = "";
        }
    }

    [Serializable]
    public class DialogCondition : DialogBlockData
    {
        public DialogReferenceState ReferenceState;
        public string MissingReferenceLabel;
        public string VariableName;
        public ConditionVariableType VariableType;
        public ConditionOperator ConditionOperator;
        public bool BoolValue;
        public int IntValue;
        public float FloatValue;
        public string StringValue;
    }

    [Serializable]
    public class ChoiceNode : DialogBlockData
    {
        public string NextNodeId;
        public List<ChoiceCondition> ChoiceCondition;
        public bool IsAndLogic = true;
        public List<ChoiceConsequence> ChoiceConsequence;
        public List<ChoiceText> Translations;

        public ChoiceNode()
        {
            Translations = new List<ChoiceText>();
            ChoiceConsequence = new List<ChoiceConsequence>();
            ChoiceCondition = new List<ChoiceCondition>();
        }
    }

    /// <summary> ENDS. Defines whether a consequence replaces a variable or changes its current numeric value, zero preserves existing assignments. </summary>
    public enum ConsequenceOperation
    {
        Assign = 0,
        Add = 1,
        Subtract = 2
    }

    [Serializable]
    public class ChoiceConsequence : DialogBlockData
    {
        [FormerlySerializedAs(nameof(Operation))][SerializeField] private ConsequenceOperation _operation;

        /// <summary> ENDS. Stores the assignment or arithmetic operation, existing serialized consequences default to Assign. </summary>
        public ConsequenceOperation Operation { get => _operation; set => _operation = value; }
        /// <summary> ENDS. Reports whether the variable supports addition and subtraction. </summary>
        public bool IsNumeric => VariableType == ConditionVariableType.Int || VariableType == ConditionVariableType.Float;
        /// <summary> ENDS. Rejects unknown operations and arithmetic on bool or string variables. </summary>
        public bool IsOperationSupported => _operation == ConsequenceOperation.Assign || (IsNumeric && (_operation == ConsequenceOperation.Add || _operation == ConsequenceOperation.Subtract));

        public DialogReferenceState ReferenceState;
        public string MissingReferenceLabel;
        public ConditionVariableType VariableType;
        public string VariableName;
        public bool BoolValue;
        public int IntValue;
        public float FloatValue;
        public string StringValue;

        /// <summary> ENDS. Evaluates an integer consequence against the current value, overflow throws before the caller writes a result. </summary>
        public int Evaluate(int currentValue)
        {
            if (VariableType != ConditionVariableType.Int) throw new InvalidOperationException("An integer consequence is required.");

            switch (_operation)
            {
                case ConsequenceOperation.Assign: return IntValue;
                case ConsequenceOperation.Add: return checked(currentValue + IntValue);
                case ConsequenceOperation.Subtract: return checked(currentValue - IntValue);
                default: throw new InvalidOperationException("Unknown consequence operation: " + _operation + ".");
            }
        }

        /// <summary> ENDS. Evaluates a float consequence against the current value and rejects nonfinite results before any variable is written. </summary>
        public float Evaluate(float currentValue)
        {
            if (VariableType != ConditionVariableType.Float) throw new InvalidOperationException("A float consequence is required.");

            float result;

            switch (_operation)
            {
                case ConsequenceOperation.Assign: result = FloatValue; break;
                case ConsequenceOperation.Add: result = currentValue + FloatValue; break;
                case ConsequenceOperation.Subtract: result = currentValue - FloatValue; break;
                default: throw new InvalidOperationException("Unknown consequence operation: " + _operation + ".");
            }

            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidOperationException("A consequence must produce a finite float value.");

            return result;
        }
    }

    [Serializable]
    public class ChoiceCondition : DialogBlockData
    {
        public DialogReferenceState ReferenceState;
        public string MissingReferenceLabel;
        public ConditionVariableType VariableType;
        public string VariableName;
        public ConditionOperator ConditionOperator;
        public bool BoolValue;
        public int IntValue;
        public float FloatValue;
        public string StringValue;
    }

    public enum CharacterAction
    {
        Show,
        Hide
    }

    public enum ConditionVariableType
    {
        Bool,
        Int,
        Float,
        String
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEquals,
        LessThanOrEquals
    }

    public enum DialogReferenceState
    {
        Valid,
        Missing
    }

    public static class DialogReferenceUtility
    {
        public static bool IsMissing(DialogReferenceState state)
        {
            return state == DialogReferenceState.Missing;
        }
    }

    public static class DialogEntryUtility
    {
        public static bool HasEvaluableStartConditions(DialogEntry entry)
        {
            if (entry == null || entry.isLocked) return false;
            if (entry.conditions == null) return false;

            foreach (DialogCondition condition in entry.conditions)
            {
                if (condition == null) continue;
                if (DialogReferenceUtility.IsMissing(condition.ReferenceState)) continue;
                if (string.IsNullOrEmpty(condition.VariableName)) continue;

                return true;
            }

            return false;
        }

        public static bool IsExcludedFromInitiatorSelection(DialogEntry entry)
        {
            return entry != null && entry.isLocked;
        }

        public static bool IsRedirectTargetCandidate(DialogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.dialogName)) return false;
            if (entry.isLocked) return true;

            return !HasEvaluableStartConditions(entry);
        }
    }
}