#if UNITY_EDITOR
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region VARIABLE RULE ADAPTERS

        internal static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<DialogCondition> conditions, int excludeIndex, string variableName)
        {
            return EndsDialogRulesED.GetAllowedNumericConditionOperators(conditions, excludeIndex, variableName);
        }

        internal static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<ChoiceCondition> conditions, int excludeIndex, string variableName)
        {
            return EndsDialogRulesED.GetAllowedNumericConditionOperators(conditions, excludeIndex, variableName);
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<DialogCondition> conditions, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(conditions, excludeIndex);
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceCondition> conditions, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(conditions, excludeIndex);
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceConsequence> consequences, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(consequences, excludeIndex);
        }

        #endregion VARIABLE RULE ADAPTERS
    }
}
#endif
