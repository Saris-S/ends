#if UNITY_EDITOR
using System.Collections.Generic;
using GraphValidationIssue = ALTUSHKA.EasyNodeDialogSystem.Editor.EndsDialogRulesED.GraphValidationIssue;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region VALIDATION ADAPTERS

        internal static void RequireValidConnection(DialogNode source, DialogNode target, string dialog, string field)
        {
            EndsDialogRulesED.RequireValidConnection(source, target, dialog, field);
        }

        internal static string GetConnectionError(DialogNode source, DialogNode target)
        {
            return EndsDialogRulesED.GetConnectionError(source, target);
        }

        internal static void ValidateDatabaseChanges(DialogDatabase before, DialogDatabase after)
        {
            EndsDialogRulesED.ValidateDatabaseChanges(before, after);
        }

        internal static List<string> ReadGraphIssues(DialogDatabase database, DialogEntry dialog)
        {
            List<string> messages = new List<string>();

            foreach (GraphValidationIssue issue in CollectGraphValidation(database, dialog.dialogName, true))
            {
                messages.Add("[" + issue.Severity + "] " + issue.Field + ": " + issue.Message);
            }

            return messages;
        }

        private static void ApplyGraphValidate(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            response.Set("validation", ReadValidationReport(database, Optional(command, "dialog")));
        }

        internal static EndsMcpJsonED ReadValidationReport(DialogDatabase database, string dialogName = null)
        {
            if (!string.IsNullOrEmpty(dialogName)) FindDialog(database, dialogName);

            return WriteValidationReport(CollectGraphValidation(database, dialogName, true));
        }

        internal static EndsMcpJsonED WriteValidationReport(List<GraphValidationIssue> issues)
        {
            EndsMcpJsonED rows = EndsMcpJsonED.Arr();
            int errors = 0;
            int warnings = 0;

            foreach (GraphValidationIssue issue in issues)
            {
                EndsMcpJsonED row = EndsMcpJsonED.Obj();
                row.Set("code", EndsMcpJsonED.Str(issue.Code));
                row.Set("severity", EndsMcpJsonED.Str(issue.Severity));
                row.Set("dialog", EndsMcpJsonED.Str(issue.Dialog));
                row.Set("nodeId", EndsMcpJsonED.Str(issue.Node));
                row.Set("field", EndsMcpJsonED.Str(issue.Field));
                row.Set("message", EndsMcpJsonED.Str(issue.Message));
                rows.Add(row);

                if (issue.Severity == "error") errors++;
                else warnings++;
            }

            EndsMcpJsonED report = EndsMcpJsonED.Obj();
            report.Set("valid", EndsMcpJsonED.Boolean(errors == 0));
            report.Set("errorCount", EndsMcpJsonED.Num(errors));
            report.Set("warningCount", EndsMcpJsonED.Num(warnings));
            report.Set("issues", rows);

            return report;
        }

        internal static List<GraphValidationIssue> CollectGraphValidation(DialogDatabase database, string dialogName, bool includeOverlaps)
        {
            return EndsDialogRulesED.CollectGraphValidation(database, dialogName, includeOverlaps);
        }

        #endregion VALIDATION ADAPTERS
    }
}
#endif
