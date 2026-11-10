using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>
    /// Shared condition evaluation helpers used by both runtime DialogWindow
    /// and editor-side EndsDialogSelectionDiagnosticsED.
    /// </summary>
    public static class DialogConditionUtility
    {
        /// <summary>
        /// Evaluates a list of conditions with AND/OR logic.
        /// No evaluable conditions (empty list or all missing/empty) - pass (true).
        /// </summary>
        public static bool EvaluateWithAndOrLogic<T>(
            IReadOnlyList<T> conditions,
            bool isAndLogic,
            Func<T, bool> isEvaluable,
            Func<T, bool> evaluate)
        {
            if (conditions == null) return true;

            List<T> evaluable = new List<T>(conditions.Count);
            
            for (int i = 0; i < conditions.Count; i++)
            {
                T condition = conditions[i];
                if (condition == null) continue;
                if (!isEvaluable(condition)) continue;
                evaluable.Add(condition);
            }

            if (evaluable.Count == 0) return true;

            if (evaluable.Count >= 2 && !isAndLogic)
            {
                for (int i = 0; i < evaluable.Count; i++)
                {
                    if (evaluate(evaluable[i])) return true;
                }

                return false;
            }

            for (int i = 0; i < evaluable.Count; i++)
            {
                if (!evaluate(evaluable[i])) return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two values using a ConditionOperator. T must be IComparable.
        /// </summary>
        public static bool CompareConditionValues<T>(T value, T target, ConditionOperator op) where T : IComparable<T>
        {
            switch (op)
            {
                case ConditionOperator.Equals: return value.CompareTo(target) == 0;
                case ConditionOperator.NotEquals: return value.CompareTo(target) != 0;
                case ConditionOperator.GreaterThan: return value.CompareTo(target) > 0;
                case ConditionOperator.LessThan: return value.CompareTo(target) < 0;
                case ConditionOperator.GreaterThanOrEquals: return value.CompareTo(target) >= 0;
                case ConditionOperator.LessThanOrEquals: return value.CompareTo(target) <= 0;
                default:
                    Debug.LogWarning($"Unknown condition operator {op}.");
                    return false;
            }
        }
    }
}
