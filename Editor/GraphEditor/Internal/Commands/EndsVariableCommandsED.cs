#if UNITY_EDITOR
using System;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsVariableCommandsED
    {
        #region DEFINITION

        internal static VariableDefinition Create(DialogDatabase database, VariableDefinition definition, bool rejectExisting)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (definition == null) throw new InvalidOperationException("Variable definition is missing.");

            definition.variableName = EndsDialogRulesED.NormalizeVariableName(definition.variableName);

            if (string.IsNullOrEmpty(definition.variableName)) throw new InvalidOperationException("Variable name cannot be empty.");

            int index = FindIndex(database, definition.variableName);

            if (index >= 0)
            {
                if (rejectExisting) throw new InvalidOperationException("Variable already exists.");
                if (database.Variables[index] == null) throw new InvalidOperationException("Variable not found.");

                return database.Variables[index];
            }

            database.AddVariable(definition);

            return definition;
        }

        internal static int Rename(DialogDatabase database, string oldName, string newName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");

            newName = EndsDialogRulesED.NormalizeVariableName(newName);

            if (string.IsNullOrEmpty(newName)) throw new InvalidOperationException("Variable name cannot be empty.");

            int index = FindIndex(database, oldName);
            VariableDefinition definition = null;

            if (index >= 0) definition = database.Variables[index];
            if (definition == null) definition = Find(database, newName);
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            if (definition.variableName == oldName && oldName != newName)
            {
                if (FindIndex(database, newName) >= 0) throw new InvalidOperationException("Variable already exists.");

                definition.variableName = newName;
            }

            return DialogReferenceRename.RenameVariableInDialogs(database, oldName, newName);
        }

        internal static void SetType(VariableDefinition definition, ConditionVariableType type)
        {
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            definition.variableType = type;
        }

        internal static void SetBoolDefault(VariableDefinition definition, bool value)
        {
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            definition.boolDefault = value;
        }

        internal static void SetIntDefault(VariableDefinition definition, int value)
        {
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            definition.intDefault = value;
        }

        internal static void SetFloatDefault(VariableDefinition definition, float value)
        {
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            definition.floatDefault = value;
        }

        internal static void SetStringDefault(VariableDefinition definition, string value)
        {
            if (definition == null) throw new InvalidOperationException("Variable not found.");

            definition.stringDefault = value ?? "";
        }

        internal static void MarkMissing(DialogDatabase database, string name)
        {
            if (database == null || string.IsNullOrEmpty(name)) return;

            DialogReferenceCleanup.MarkVariableMissingInDialogs(database, name);
        }

        internal static void RemoveAt(DialogDatabase database, int index)
        {
            if (database?.Variables == null) return;
            if (index < 0 || index >= database.Variables.Count) return;

            database.RemoveVariableAt(index);
        }

        internal static void Delete(DialogDatabase database, string name, bool missingIsError)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Variable name cannot be empty.");
            if (missingIsError && Find(database, name) == null) throw new InvalidOperationException("Variable not found.");

            MarkMissing(database, name);

            if (database.Variables == null) return;

            for (int i = database.Variables.Count - 1; i >= 0; i--)
            {
                VariableDefinition definition = database.Variables[i];

                if (definition != null && definition.variableName == name) database.RemoveVariableAt(i);
            }
        }

        #endregion DEFINITION

        #region LOOKUP

        internal static int FindIndex(DialogDatabase database, string name)
        {
            if (database?.Variables == null || string.IsNullOrEmpty(name)) return -1;

            for (int i = 0; i < database.Variables.Count; i++)
            {
                VariableDefinition definition = database.Variables[i];

                if (definition != null && definition.variableName == name) return i;
            }

            return -1;
        }

        internal static VariableDefinition Find(DialogDatabase database, string name)
        {
            int index = FindIndex(database, name);

            if (index < 0) return null;

            return database.Variables[index];
        }

        #endregion LOOKUP
    }
}
#endif
