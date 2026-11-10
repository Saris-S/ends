#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        #region FIELDS

        private static readonly ConditionOperator[] _equalityConditionOperators = { ConditionOperator.Equals, ConditionOperator.NotEquals };

        private static readonly ConditionOperator[] _numericConditionOperatorsAll =
        {
            ConditionOperator.Equals,
            ConditionOperator.NotEquals,
            ConditionOperator.GreaterThan,
            ConditionOperator.LessThan,
            ConditionOperator.GreaterThanOrEquals,
            ConditionOperator.LessThanOrEquals
        };


        #endregion FIELDS

        #region VARIABLE RULES

        internal static ConditionOperator[] GetSupportedConditionOperators(ConditionVariableType type)
        {
            if (type == ConditionVariableType.Bool || type == ConditionVariableType.String) return _equalityConditionOperators;

            return _numericConditionOperatorsAll;
        }

        private static void AddNumericOperatorReuseForbidden(HashSet<ConditionOperator> forbidden, ConditionOperator siblingOperator)
        {
            if (siblingOperator == ConditionOperator.NotEquals) return;

            if (siblingOperator == ConditionOperator.GreaterThan || siblingOperator == ConditionOperator.GreaterThanOrEquals)
            {
                forbidden.Add(ConditionOperator.GreaterThan);
                forbidden.Add(ConditionOperator.GreaterThanOrEquals);
                forbidden.Add(ConditionOperator.Equals);
                return;
            }

            if (siblingOperator == ConditionOperator.LessThan || siblingOperator == ConditionOperator.LessThanOrEquals)
            {
                forbidden.Add(ConditionOperator.LessThan);
                forbidden.Add(ConditionOperator.LessThanOrEquals);
                forbidden.Add(ConditionOperator.Equals);
            }
        }

        internal static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<DialogCondition> siblingConditions, int excludeIndex, string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return _numericConditionOperatorsAll;

            HashSet<ConditionOperator> forbidden = new HashSet<ConditionOperator>();
            bool hasSameVariableSibling = false;

            if (siblingConditions != null)
            {
                for (int i = 0; i < siblingConditions.Count; i++)
                {
                    if (i == excludeIndex) continue;

                    DialogCondition sibling = siblingConditions[i];

                    if (sibling == null || DialogReferenceUtility.IsMissing(sibling.ReferenceState)) continue;
                    if (string.IsNullOrEmpty(sibling.VariableName)) continue;
                    if (sibling.VariableName != variableName) continue;

                    hasSameVariableSibling = true;
                    AddNumericOperatorReuseForbidden(forbidden, sibling.ConditionOperator);
                }
            }

            if (!hasSameVariableSibling) return _numericConditionOperatorsAll;

            List<ConditionOperator> allowed = new List<ConditionOperator>(_numericConditionOperatorsAll.Length);

            for (int i = 0; i < _numericConditionOperatorsAll.Length; i++)
            {
                ConditionOperator op = _numericConditionOperatorsAll[i];

                if (forbidden.Contains(op)) continue;

                allowed.Add(op);
            }

            if (allowed.Count == 0) return Array.Empty<ConditionOperator>();

            return allowed.ToArray();
        }

        internal static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<ChoiceCondition> siblingConditions, int excludeIndex, string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return _numericConditionOperatorsAll;

            HashSet<ConditionOperator> forbidden = new HashSet<ConditionOperator>();
            bool hasSameVariableSibling = false;

            if (siblingConditions != null)
            {
                for (int i = 0; i < siblingConditions.Count; i++)
                {
                    if (i == excludeIndex) continue;

                    ChoiceCondition sibling = siblingConditions[i];

                    if (sibling == null || DialogReferenceUtility.IsMissing(sibling.ReferenceState)) continue;
                    if (string.IsNullOrEmpty(sibling.VariableName)) continue;
                    if (sibling.VariableName != variableName) continue;

                    hasSameVariableSibling = true;
                    AddNumericOperatorReuseForbidden(forbidden, sibling.ConditionOperator);
                }
            }

            if (!hasSameVariableSibling) return _numericConditionOperatorsAll;

            List<ConditionOperator> allowed = new List<ConditionOperator>(_numericConditionOperatorsAll.Length);

            for (int i = 0; i < _numericConditionOperatorsAll.Length; i++)
            {
                ConditionOperator op = _numericConditionOperatorsAll[i];

                if (forbidden.Contains(op)) continue;

                allowed.Add(op);
            }

            if (allowed.Count == 0) return Array.Empty<ConditionOperator>();

            return allowed.ToArray();
        }

        private static void CollectNumericConditionVariableExclusions<T>(
            IReadOnlyList<T> conditions,
            int excludeIndex,
            HashSet<string> excludedNames,
            Func<T, ConditionVariableType> getVariableType,
            Func<T, string> getVariableName,
            Func<T, ConditionOperator> getConditionOperator,
            Func<T, DialogReferenceState> getReferenceState,
            Func<IReadOnlyList<T>, int, string, ConditionOperator[]> getAllowedOperators)
        {
            HashSet<string> numericNamesToCheck = new HashSet<string>();

            if (conditions == null) return;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (i == excludeIndex) continue;

                T condition = conditions[i];

                if (condition == null || DialogReferenceUtility.IsMissing(getReferenceState(condition))) continue;

                string variableName = getVariableName(condition);

                if (string.IsNullOrEmpty(variableName)) continue;

                ConditionVariableType variableType = getVariableType(condition);

                if (variableType == ConditionVariableType.Bool || variableType == ConditionVariableType.String)
                {
                    excludedNames.Add(variableName);
                    continue;
                }

                if (variableType == ConditionVariableType.Int || variableType == ConditionVariableType.Float)
                {
                    if (getConditionOperator(condition) == ConditionOperator.Equals) excludedNames.Add(variableName);
                    else numericNamesToCheck.Add(variableName);
                    continue;
                }

                excludedNames.Add(variableName);
            }

            foreach (string variableName in numericNamesToCheck)
            {
                if (excludedNames.Contains(variableName)) continue;

                ConditionOperator[] allowedOperators = getAllowedOperators(conditions, excludeIndex, variableName);

                if (allowedOperators.Length == 0) excludedNames.Add(variableName);
            }
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<DialogCondition> conditions, int excludeIndex)
        {
            HashSet<string> excludedNames = new HashSet<string>();
            CollectNumericConditionVariableExclusions(
                conditions,
                excludeIndex,
                excludedNames,
                condition => condition.VariableType,
                condition => condition.VariableName,
                condition => condition.ConditionOperator,
                condition => condition.ReferenceState,
                GetAllowedNumericConditionOperators);

            return new List<string>(excludedNames);
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceCondition> conditions, int excludeIndex)
        {
            HashSet<string> excludedNames = new HashSet<string>();
            CollectNumericConditionVariableExclusions(
                conditions,
                excludeIndex,
                excludedNames,
                condition => condition.VariableType,
                condition => condition.VariableName,
                condition => condition.ConditionOperator,
                condition => condition.ReferenceState,
                GetAllowedNumericConditionOperators);

            return new List<string>(excludedNames);
        }

        internal static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceConsequence> consequences, int excludeIndex)
        {
            List<string> usedNames = new List<string>();

            if (consequences == null) return usedNames;

            for (int i = 0; i < consequences.Count; i++)
            {
                if (i == excludeIndex) continue;

                ChoiceConsequence consequence = consequences[i];

                if (consequence == null || DialogReferenceUtility.IsMissing(consequence.ReferenceState)) continue;
                if (string.IsNullOrEmpty(consequence.VariableName)) continue;

                usedNames.Add(consequence.VariableName);
            }

            return usedNames;
        }

        #endregion VARIABLE RULES
    }
}
#endif