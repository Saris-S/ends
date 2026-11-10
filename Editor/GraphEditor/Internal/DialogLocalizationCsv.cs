#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Imports and exports localized text using scoped node addresses and stable option IDs, translator-facing columns contain language codes. </summary>
    internal static class EndsDialogLocalizationCsvED
    {
        private const string KEY_COLUMN = "Key";
        private const string LINE_SUFFIX = ".line";
        private static readonly string _optionMarker = "." + EndsBlockIdentityED.Option.Prefix + ".";
        private const int MAX_UNKNOWN_KEY_SAMPLES = 20;
        private const char CSV_SEPARATOR = ',';
        private const char CSV_QUOTE = '"';

        /// <summary> ENDS. Exports text without changing the database, option keys retain their identity across list edits. </summary>
        public static string BuildCsv(DialogDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            List<string> languages = GetLanguageColumns(database);

            if (languages.Count == 0) throw new InvalidOperationException("At least one available language is required to export localization.");

            StringBuilder sb = new StringBuilder();
            AppendCsvRow(sb, BuildHeader(languages));

            if (database.Dialogs == null) return sb.ToString();

            for (int d = 0; d < database.Dialogs.Count; d++)
            {
                DialogEntry dialog = database.Dialogs[d];

                if (dialog?.nodes == null) continue;

                for (int n = 0; n < dialog.nodes.Count; n++)
                {
                    DialogNode node = dialog.nodes[n];

                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;
                    if (node.NodeType == NodeType.Line && node.LineData != null) AppendTextRow(sb, BuildSlotLookupKey(EndsNodeRefED.Address(dialog, node.nodeId), true, null), node.LineData.DialogsText, languages);
                    if (node.NodeType != NodeType.Choice || node.ChoiceData == null) continue;

                    for (int c = 0; c < node.ChoiceData.Count; c++)
                    {
                        ChoiceNode choice = node.ChoiceData[c];
                        if (choice == null) continue;
                        AppendTextRow(sb, BuildSlotLookupKey(EndsNodeRefED.Address(dialog, node.nodeId), false, choice.BlockId), choice.Translations, languages);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary> ENDS. Validates the complete table before writing, unknown or malformed text addresses are reported without redirecting translations to another option. </summary>
        public static ImportResult ApplyCsv(DialogDatabase database, string csvText)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (!TryReadTable(csvText, out List<List<string>> rows, out List<string> languages, out string error)) return ImportResult.Fail(error);

            Dictionary<string, LocalizationSlot> slots = BuildSlotIndex(database);
            var pending = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            int updated = 0;
            int skippedUnknown = 0;
            int skippedMalformed = 0;
            var unknownKeys = new List<string>();

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> cells = rows[r];

                if (cells.Count == 1 && string.IsNullOrWhiteSpace(cells[0])) continue;

                string key = cells[0].Trim();

                if (!TryParseKey(key, out string nodeId, out bool isLine, out string optionId))
                {
                    skippedMalformed++;

                    continue;
                }

                if (!EndsNodeRefED.TryResolveInDatabase(database, nodeId, out nodeId, out _))
                {
                    skippedUnknown++;

                    if (unknownKeys.Count < MAX_UNKNOWN_KEY_SAMPLES) unknownKeys.Add(key);

                    continue;
                }

                string slotKey = BuildSlotLookupKey(nodeId, isLine, optionId);

                if (!slots.ContainsKey(slotKey))
                {
                    skippedUnknown++;

                    if (unknownKeys.Count < MAX_UNKNOWN_KEY_SAMPLES) unknownKeys.Add(key);

                    continue;
                }

                if (pending.ContainsKey(slotKey)) return ImportResult.Fail("Duplicate text key: " + key);

                pending.Add(slotKey, cells);
            }

            foreach (KeyValuePair<string, List<string>> row in pending)
            {
                LocalizationSlot slot = slots[row.Key];
                bool rowChanged = false;

                for (int li = 0; li < languages.Count; li++)
                {
                    if (slot.SetText(languages[li], row.Value[li + 1])) rowChanged = true;
                }

                if (rowChanged) updated++;
            }

            return new ImportResult(updated, skippedUnknown, skippedMalformed, unknownKeys);
        }

        public readonly struct CsvMissingLanguageColumn
        {
            public readonly string LanguageCode;
            public readonly int RowCountWithText;

            public CsvMissingLanguageColumn(string languageCode, int rowCountWithText)
            {
                LanguageCode = languageCode;
                RowCountWithText = rowCountWithText;
            }
        }

        /// <summary> ENDS. Lists valid CSV language columns absent from the database, invalid tables never propose registry changes. </summary>
        public static List<CsvMissingLanguageColumn> GetMissingLanguageColumns(string csvText, DialogDatabase database)
        {
            var missing = new List<CsvMissingLanguageColumn>();

            if (database == null || !TryReadTable(csvText, out List<List<string>> rows, out List<string> languages, out _)) return missing;

            for (int li = 0; li < languages.Count; li++)
            {
                string language = languages[li];

                if (IsListedLanguage(database, language)) continue;

                missing.Add(new CsvMissingLanguageColumn(language, CountCsvRowsWithLanguageText(rows, li + 1)));
            }

            return missing;
        }

        public static string BuildMissingLanguagesImportPromptMessage(IReadOnlyList<CsvMissingLanguageColumn> missingLanguages)
        {
            if (missingLanguages == null || missingLanguages.Count == 0) return "";
            if (missingLanguages.Count == 1)
            {
                CsvMissingLanguageColumn column = missingLanguages[0];
                return "CSV contains \"" + column.LanguageCode + "\" (" + column.RowCountWithText + " rows) not in Available languages. Add to Available languages?";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("CSV contains languages not in Available languages:").Append('\n').Append('\n');

            for (int i = 0; i < missingLanguages.Count; i++)
            {
                CsvMissingLanguageColumn column = missingLanguages[i];
                sb.Append("- ").Append(column.LanguageCode).Append(" (").Append(column.RowCountWithText).Append(" rows)").Append('\n');
            }

            sb.Append('\n').Append("Add them to Available languages?");

            return sb.ToString();
        }

        public static void AddLanguagesToAvailableList(DialogDatabase database, IReadOnlyList<CsvMissingLanguageColumn> missingLanguages)
        {
            if (database == null || missingLanguages == null || missingLanguages.Count == 0) return;
            if (database.AvailableLanguages == null) database.AvailableLanguages = new List<string>();

            for (int i = 0; i < missingLanguages.Count; i++)
            {
                string languageCode = missingLanguages[i].LanguageCode;

                if (string.IsNullOrEmpty(languageCode)) continue;
                if (database.AvailableLanguages.Contains(languageCode)) continue;

                database.AvailableLanguages.Add(languageCode);
            }
        }

        private static bool IsListedLanguage(DialogDatabase database, string languageCode)
        {
            if (database?.AvailableLanguages == null) return false;

            return database.AvailableLanguages.Contains(languageCode);
        }

        private static int CountCsvRowsWithLanguageText(List<List<string>> rows, int languageCellIndex)
        {
            int count = 0;

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> cells = rows[r];

                if (cells.Count <= languageCellIndex || string.IsNullOrWhiteSpace(cells[languageCellIndex])) continue;

                count++;
            }

            return count;
        }

        private static List<string> GetLanguageColumns(DialogDatabase database)
        {
            List<string> languages = new List<string>();

            if (database.AvailableLanguages == null) return languages;

            for (int i = 0; i < database.AvailableLanguages.Count; i++)
            {
                string lang = database.AvailableLanguages[i];

                if (string.IsNullOrEmpty(lang)) continue;
                if (languages.Contains(lang)) continue;

                languages.Add(lang);
            }

            return languages;
        }

        private static List<string> BuildHeader(List<string> languages)
        {
            List<string> header = new List<string>(languages.Count + 1) { KEY_COLUMN };

            for (int i = 0; i < languages.Count; i++)
            {
                header.Add(languages[i]);
            }

            return header;
        }

        private static void AppendTextRow(StringBuilder sb, string key, IReadOnlyList<IDialogText> texts, List<string> languages)
        {
            List<string> cells = new List<string>(languages.Count + 1) { key };

            for (int i = 0; i < languages.Count; i++)
            {
                cells.Add(GetTextForLanguage(texts, languages[i]));
            }

            AppendCsvRow(sb, cells);
        }

        private static string GetTextForLanguage(IReadOnlyList<IDialogText> texts, string language)
        {
            if (texts == null) return "";

            for (int i = 0; i < texts.Count; i++)
            {
                IDialogText entry = texts[i];

                if (entry == null) continue;
                if (entry.LanguageCode != language) continue;
                if (entry.Text == null) return "";

                return entry.Text;
            }

            return "";
        }

        private static bool SetLocalizedText<T>(List<T> texts, string language, string newText) where T : class, IDialogText, new()
        {
            if (texts == null) return false;

            for (int i = 0; i < texts.Count; i++)
            {
                T entry = texts[i];

                if (entry == null) continue;
                if (entry.LanguageCode != language) continue;

                string current = entry.Text ?? "";

                if (current == newText) return false;

                entry.Text = newText;

                return true;
            }

            if (newText.Length == 0) return false;

            T added = new T { LanguageCode = language, Text = newText };

            if (added is LineText line) line.SayerName = "";

            texts.Add(added);

            return true;
        }

        private static Dictionary<string, LocalizationSlot> BuildSlotIndex(DialogDatabase database)
        {
            Dictionary<string, LocalizationSlot> slots = new Dictionary<string, LocalizationSlot>();

            if (database.Dialogs == null) return slots;

            for (int d = 0; d < database.Dialogs.Count; d++)
            {
                DialogEntry dialog = database.Dialogs[d];

                if (dialog?.nodes == null) continue;

                for (int n = 0; n < dialog.nodes.Count; n++)
                {
                    DialogNode node = dialog.nodes[n];

                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;

                    if (node.NodeType == NodeType.Line && node.LineData != null)
                    {
                        slots.Add(BuildSlotLookupKey(EndsNodeRefED.Address(dialog, node.nodeId), true, null), new LocalizationSlot(node.LineData));
                    }

                    if (node.NodeType != NodeType.Choice || node.ChoiceData == null) continue;

                    for (int c = 0; c < node.ChoiceData.Count; c++)
                    {
                        ChoiceNode choice = node.ChoiceData[c];

                        if (choice == null) continue;

                        slots.Add(BuildSlotLookupKey(EndsNodeRefED.Address(dialog, node.nodeId), false, choice.BlockId), new LocalizationSlot(choice));
                    }
                }
            }

            return slots;
        }

        private static string BuildSlotLookupKey(string nodeId, bool isLine, string optionId)
        {
            int separator = nodeId.LastIndexOf('/');

            if (separator >= 0)
            {
                string dialogName = Uri.UnescapeDataString(nodeId.Substring(0, separator));
                EndsDialogRulesED.RequireDialogName(dialogName);
                nodeId = dialogName + "." + nodeId.Substring(separator + 1);
            }

            if (isLine) return nodeId + LINE_SUFFIX;
            if (!EndsBlockIdentityED.Option.TryReadNumber(optionId, out _)) throw new InvalidOperationException("Option requires a valid stable ID: " + nodeId);

            return nodeId + "." + optionId;
        }

        internal static bool TryParseKey(string key, out string nodeId, out bool isLine, out string optionId)
        {
            nodeId = null;
            isLine = false;
            optionId = null;

            if (string.IsNullOrEmpty(key)) return false;
            if (key.EndsWith(LINE_SUFFIX, StringComparison.Ordinal))
            {
                nodeId = key.Substring(0, key.Length - LINE_SUFFIX.Length);
                isLine = true;
            }
            else
            {
                int optionPos = key.LastIndexOf(_optionMarker, StringComparison.Ordinal);

                if (optionPos <= 0) return false;

                nodeId = key.Substring(0, optionPos);
                optionId = key.Substring(optionPos + 1);

                if (!EndsBlockIdentityED.Option.TryReadNumber(optionId, out _)) return false;
            }

            if (string.IsNullOrEmpty(nodeId)) return false;
            if (EndsNodeIdentityED.TryReadNumber(nodeId, out _)) return true;

            int separator = nodeId.IndexOf('.');

            if (separator < 0) return nodeId.IndexOf('/') < 0;

            string fullNodeId = nodeId.Substring(separator + 1);

            if (separator == 0 || !EndsNodeIdentityED.TryReadNumber(fullNodeId, out _)) return false;

            nodeId = Uri.EscapeDataString(nodeId.Substring(0, separator)) + "/" + fullNodeId;

            return true;
        }

        private static void AppendCsvRow(StringBuilder sb, List<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(CSV_SEPARATOR);

                sb.Append(EscapeCsvCell(cells[i]));
            }

            sb.Append("\r\n");
        }

        private static string EscapeCsvCell(string value)
        {
            if (value == null) return "";

            bool needsQuotes = value.IndexOf(CSV_SEPARATOR) >= 0
                || value.IndexOf(CSV_QUOTE) >= 0
                || value.IndexOf('\n') >= 0
                || value.IndexOf('\r') >= 0;

            if (!needsQuotes) return value;

            return CSV_QUOTE + value.Replace("\"", "\"\"") + CSV_QUOTE;
        }

        private static bool TryReadTable(string csvText, out List<List<string>> rows, out List<string> languages, out string error)
        {
            languages = new List<string>();

            if (!TryReadRows(csvText, out rows, out error)) return false;
            if (rows.Count == 0)
            {
                error = "CSV is empty.";

                return false;
            }

            List<string> header = rows[0];

            if (header.Count < 2 || !string.Equals(header[0], KEY_COLUMN, StringComparison.OrdinalIgnoreCase))
            {
                error = "First column must be \"" + KEY_COLUMN + "\" followed by language columns.";

                return false;
            }

            var columns = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 1; i < header.Count; i++)
            {
                string language = header[i].Trim();

                if (language.Length == 0 || !columns.Add(language))
                {
                    error = "Language columns must be non-empty and unique.";

                    return false;
                }

                languages.Add(language);
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> cells = rows[r];

                if (cells.Count == 1 && string.IsNullOrWhiteSpace(cells[0])) continue;
                if (cells.Count != header.Count)
                {
                    error = "CSV row " + (r + 1).ToString(CultureInfo.InvariantCulture) + " has " + cells.Count.ToString(CultureInfo.InvariantCulture) + " columns, expected " + header.Count.ToString(CultureInfo.InvariantCulture) + ".";

                    return false;
                }

                if (!keys.Add(cells[0].Trim()))
                {
                    error = "Duplicate text key: " + cells[0];

                    return false;
                }
            }

            return true;
        }

        private static bool TryReadRows(string csvText, out List<List<string>> rows, out string error)
        {
            rows = new List<List<string>>();
            error = null;

            if (string.IsNullOrEmpty(csvText)) return true;

            var cells = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            bool quotedCell = false;
            int start = 0;

            if (csvText[0] == '\uFEFF') start = 1;

            for (int i = start; i < csvText.Length; i++)
            {
                char ch = csvText[i];

                if (inQuotes)
                {
                    if (ch == CSV_QUOTE && i + 1 < csvText.Length && csvText[i + 1] == CSV_QUOTE)
                    {
                        current.Append(CSV_QUOTE);
                        i++;
                    }
                    else if (ch == CSV_QUOTE) inQuotes = false;
                    else current.Append(ch);

                    continue;
                }

                if (ch == CSV_SEPARATOR || ch == '\n' || ch == '\r')
                {
                    cells.Add(current.ToString());
                    current.Length = 0;
                    quotedCell = false;

                    if (ch == CSV_SEPARATOR) continue;
                    if (ch == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n') i++;

                    rows.Add(cells);
                    cells = new List<string>();

                    continue;
                }

                if (ch == CSV_QUOTE && current.Length == 0 && !quotedCell)
                {
                    inQuotes = true;
                    quotedCell = true;

                    continue;
                }

                if (ch == CSV_QUOTE || quotedCell)
                {
                    error = "Invalid CSV quotes in row " + (rows.Count + 1).ToString(CultureInfo.InvariantCulture) + ".";

                    return false;
                }

                current.Append(ch);
            }

            if (inQuotes)
            {
                error = "Unclosed CSV quote in row " + (rows.Count + 1).ToString(CultureInfo.InvariantCulture) + ".";

                return false;
            }

            if (cells.Count > 0 || current.Length > 0 || quotedCell)
            {
                cells.Add(current.ToString());
                rows.Add(cells);
            }

            return true;
        }

        private readonly struct LocalizationSlot
        {
            /// <summary> ENDS. Retains the owning line so lookup remains read-only when its translations are absent. </summary>
            internal readonly LineNode Line;
            /// <summary> ENDS. Retains the exact option identified by BlockId, independent of its current position. </summary>
            internal readonly ChoiceNode Option;

            /// <summary> ENDS. Captures an existing line without allocating translations during key lookup. </summary>
            public LocalizationSlot(LineNode line)
            {
                Line = line;
                Option = null;
            }

            /// <summary> ENDS. Captures an existing option without allocating translations during key lookup. </summary>
            public LocalizationSlot(ChoiceNode option)
            {
                Line = null;
                Option = option;
            }

            /// <summary> ENDS. Writes one existing translation or creates a non-empty translation, empty absent cells remain unchanged. </summary>
            internal bool SetText(string language, string text)
            {
                if (Line != null)
                {
                    if (Line.DialogsText == null && text.Length == 0) return false;
                    if (Line.DialogsText == null) Line.DialogsText = new List<LineText>();

                    return SetLocalizedText(Line.DialogsText, language, text);
                }

                if (Option.Translations == null && text.Length == 0) return false;
                if (Option.Translations == null) Option.Translations = new List<ChoiceText>();

                return SetLocalizedText(Option.Translations, language, text);
            }
        }

        public readonly struct ImportResult
        {
            public readonly bool Ok;
            public readonly string Error;
            public readonly int UpdatedRows;
            public readonly int UnknownKeys;
            public readonly int MalformedKeys;
            public readonly IReadOnlyList<string> SampleUnknownKeys;

            public ImportResult(int updatedRows, int unknownKeys, int malformedKeys, List<string> sampleUnknownKeys)
            {
                Ok = true;
                Error = null;
                UpdatedRows = updatedRows;
                UnknownKeys = unknownKeys;
                MalformedKeys = malformedKeys;
                if (sampleUnknownKeys == null) SampleUnknownKeys = Array.Empty<string>();
                else SampleUnknownKeys = sampleUnknownKeys;
            }

            private ImportResult(string error)
            {
                Ok = false;
                Error = error;
                UpdatedRows = 0;
                UnknownKeys = 0;
                MalformedKeys = 0;
                SampleUnknownKeys = Array.Empty<string>();
            }

            public static ImportResult Fail(string error) => new ImportResult(error);

            public string BuildMessage()
            {
                if (!Ok) return Error;

                StringBuilder sb = new StringBuilder();
                sb.Append("Updated rows: ").Append(UpdatedRows).Append('.');

                if (UnknownKeys > 0) sb.Append(" Unknown keys: ").Append(UnknownKeys).Append('.');
                if (MalformedKeys > 0) sb.Append(" Malformed keys: ").Append(MalformedKeys).Append('.');

                if (SampleUnknownKeys != null && SampleUnknownKeys.Count > 0)
                {
                    sb.Append("\n\nUnknown key samples:\n");
                    for (int i = 0; i < SampleUnknownKeys.Count; i++)
                    {
                        sb.Append("- ").Append(SampleUnknownKeys[i]).Append('\n');
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary> ENDS. Checks an existing scoped line or stable option key without creating translation lists. </summary>
        public static bool ContainsTextKey(DialogDatabase database, string key)
        {
            if (database == null || string.IsNullOrEmpty(key)) return false;
            if (!TryParseKey(key, out string nodeId, out bool isLine, out string optionId)) return false;
            if (!EndsNodeRefED.TryResolveInDatabase(database, nodeId, out nodeId, out _)) return false;

            Dictionary<string, LocalizationSlot> slots = BuildSlotIndex(database);

            return slots.ContainsKey(BuildSlotLookupKey(nodeId, isLine, optionId));
        }

        /// <summary> ENDS. Writes one language into the existing line or option identified by its stable key. </summary>
        public static bool TryApplyText(DialogDatabase database, string key, string language, string text, out bool changed, out string error)
        {
            changed = false;
            error = null;

            if (database == null)
            {
                error = "Dialog database is not assigned.";
                return false;
            }

            if (string.IsNullOrEmpty(language))
            {
                error = "Language is empty.";
                return false;
            }

            if (!TryParseKey(key, out string nodeId, out bool isLine, out string optionId))
            {
                error = "Malformed key.";

                return false;
            }

            if (!EndsNodeRefED.TryResolveInDatabase(database, nodeId, out nodeId, out error)) return false;

            Dictionary<string, LocalizationSlot> slots = BuildSlotIndex(database);
            string slotKey = BuildSlotLookupKey(nodeId, isLine, optionId);

            if (!slots.TryGetValue(slotKey, out LocalizationSlot slot))
            {
                error = "Unknown key.";
                return false;
            }

            if (text == null) text = "";

            changed = slot.SetText(language, text);

            return true;
        }

        /// <summary> Writes the speaker display name on a line slot. </summary>
        public static bool TryApplySayer(DialogDatabase database, string key, string language, string sayer, out string error)
        {
            if (!TryLineSlot(database, key, language, out LineText entry, out error)) return false;
            if (entry.SayerName != (sayer ?? "")) entry.SayerName = sayer ?? "";

            return true;
        }

        /// <summary> Writes or clears the voice clip on a line slot. </summary>
        public static bool TryApplyVoice(DialogDatabase database, string key, string language, string assetPath, out string error)
        {
            if (!TryLineSlot(database, key, language, out LineText entry, out error)) return false;
            if (string.IsNullOrEmpty(assetPath))
            {
                entry.VoiceClip = null;

                return true;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

            if (clip == null)
            {
                error = "Voice clip not found: " + assetPath;

                return false;
            }

            entry.VoiceClip = clip;

            return true;
        }

        private static bool TryLineSlot(DialogDatabase database, string key, string language, out LineText entry, out string error)
        {
            entry = null;
            error = null;

            if (!TryParseKey(key, out string nodeId, out bool isLine, out string optionId))
            {
                error = "Malformed key.";

                return false;
            }

            if (!EndsNodeRefED.TryResolveInDatabase(database, nodeId, out nodeId, out error)) return false;

            if (!isLine)
            {
                error = "Sayer and voice belong to a line.";

                return false;
            }

            Dictionary<string, LocalizationSlot> slots = BuildSlotIndex(database);
            string slotKey = BuildSlotLookupKey(nodeId, true, optionId);

            if (!slots.TryGetValue(slotKey, out LocalizationSlot slot) || slot.Line == null)
            {
                error = "Unknown key.";
                return false;
            }

            if (slot.Line.DialogsText == null) slot.Line.DialogsText = new List<LineText>();

            for (int i = 0; i < slot.Line.DialogsText.Count; i++)
            {
                LineText text = slot.Line.DialogsText[i];

                if (text == null || text.LanguageCode != language) continue;

                entry = text;

                return true;
            }

            entry = new LineText();
            entry.LanguageCode = language;
            entry.SayerName = "";
            entry.Text = "";
            slot.Line.DialogsText.Add(entry);

            return true;
        }

        public static void WriteCsvFile(DialogDatabase database, string path)
        {
            string csv = BuildCsv(database);
            File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        public static ImportResult ReadAndApplyCsvFile(DialogDatabase database, string path)
        {
            string csv = File.ReadAllText(path, Encoding.UTF8);

            return ApplyCsv(database, csv);
        }
    }
}
#endif