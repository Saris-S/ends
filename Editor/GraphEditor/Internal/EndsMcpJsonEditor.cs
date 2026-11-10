#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed class EndsMcpJsonED
    {
        #region FIELDS

        internal enum Kind
        {
            Null,
            Bool,
            Number,
            String,
            Array,
            Object
        }

        internal Kind Type;
        internal bool Bool;
        internal string Text;
        internal List<EndsMcpJsonED> Items;
        internal List<string> Keys;
        internal List<EndsMcpJsonED> Values;

        #endregion FIELDS

        #region VALUES

        internal static EndsMcpJsonED Null()
        {
            EndsMcpJsonED value = new EndsMcpJsonED();
            value.Type = Kind.Null;
            return value;
        }

        internal static EndsMcpJsonED Boolean(bool value)
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Bool;
            node.Bool = value;
            return node;
        }

        internal static EndsMcpJsonED Num(int number)
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Number;
            node.Text = number.ToString(CultureInfo.InvariantCulture);
            return node;
        }

        /// <summary> ENDS. Preserves all unsigned 64-bit integer digits without passing identifiers through floating-point values. </summary>
        internal static EndsMcpJsonED Num(ulong number)
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Number;
            node.Text = number.ToString(CultureInfo.InvariantCulture);

            return node;
        }

        internal static EndsMcpJsonED Num(float number)
        {
            if (float.IsNaN(number) || float.IsInfinity(number)) throw new InvalidOperationException("JSON numbers must be finite.");

            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Number;
            node.Text = number.ToString("R", CultureInfo.InvariantCulture);

            return node;
        }

        internal static EndsMcpJsonED Str(string text)
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.String;
            if (text == null) node.Text = "";
            else node.Text = text;
            return node;
        }

        internal static EndsMcpJsonED Obj()
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Object;
            node.Keys = new List<string>();
            node.Values = new List<EndsMcpJsonED>();
            return node;
        }

        internal static EndsMcpJsonED Arr()
        {
            EndsMcpJsonED node = new EndsMcpJsonED();
            node.Type = Kind.Array;
            node.Items = new List<EndsMcpJsonED>();
            return node;
        }

        internal EndsMcpJsonED Get(string key)
        {
            if (Type != Kind.Object || Keys == null) return null;

            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i] != key) continue;

                return Values[i];
            }

            return null;
        }

        internal void Set(string key, EndsMcpJsonED value)
        {
            if (Type != Kind.Object) throw new InvalidOperationException("JSON value is not an object.");
            if (Keys == null) Keys = new List<string>();
            if (Values == null) Values = new List<EndsMcpJsonED>();

            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i] != key) continue;

                Values[i] = value;
                return;
            }

            Keys.Add(key);
            Values.Add(value);
        }

        internal bool Remove(string key)
        {
            if (Type != Kind.Object || Keys == null) return false;

            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i] != key) continue;

                Keys.RemoveAt(i);
                Values.RemoveAt(i);
                return true;
            }

            return false;
        }

        internal void Add(EndsMcpJsonED value)
        {
            if (Type != Kind.Array) throw new InvalidOperationException("JSON value is not an array.");
            if (Items == null) Items = new List<EndsMcpJsonED>();

            Items.Add(value);
        }

        internal string Write()
        {
            StringBuilder sb = new StringBuilder();
            Append(sb, this);
            return sb.ToString();
        }

        #endregion VALUES

        #region PARSE

        internal static EndsMcpJsonED Parse(string json)
        {
            if (json == null) throw new FormatException("JSON is empty.");

            Parser parser = new Parser(json);
            EndsMcpJsonED value = parser.ReadValue();
            parser.SkipSpace();

            if (!parser.End) throw new FormatException("Trailing data in JSON.");

            return value;
        }

        private static void Append(StringBuilder sb, EndsMcpJsonED value)
        {
            if (value == null || value.Type == Kind.Null)
            {
                sb.Append("null");
                return;
            }

            if (value.Type == Kind.Bool)
            {
                if (value.Bool) sb.Append("true");
                else sb.Append("false");
                return;
            }

            if (value.Type == Kind.Number)
            {
                sb.Append(value.Text);
                return;
            }

            if (value.Type == Kind.String)
            {
                AppendString(sb, value.Text);
                return;
            }

            if (value.Type == Kind.Array)
            {
                sb.Append('[');

                if (value.Items != null)
                {
                    for (int i = 0; i < value.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');

                        Append(sb, value.Items[i]);
                    }
                }

                sb.Append(']');
                return;
            }

            sb.Append('{');

            if (value.Keys != null)
            {
                for (int i = 0; i < value.Keys.Count; i++)
                {
                    if (i > 0) sb.Append(',');

                    AppendString(sb, value.Keys[i]);
                    sb.Append(':');
                    Append(sb, value.Values[i]);
                }
            }

            sb.Append('}');
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');

            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];

                    if (ch == '"') sb.Append("\\\"");
                    else if (ch == '\\') sb.Append("\\\\");
                    else if (ch == '\n') sb.Append("\\n");
                    else if (ch == '\r') sb.Append("\\r");
                    else if (ch == '\t') sb.Append("\\t");
                    else if (ch < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else sb.Append(ch);
                }
            }

            sb.Append('"');
        }

        private sealed class Parser
        {
            private const int MAX_DEPTH = 64;
            private readonly string _text;
            private int _index;
            private int _depth;

            public Parser(string text)
            {
                _text = text;

                if (_text.Length > 0 && _text[0] == '\uFEFF') _index = 1;
            }

            public bool End
            {
                get { return _index >= _text.Length; }
            }

            public void SkipSpace()
            {
                while (_index < _text.Length)
                {
                    char ch = _text[_index];

                    if (ch != ' ' && ch != '\t' && ch != '\n' && ch != '\r') return;

                    _index++;
                }
            }

            public EndsMcpJsonED ReadValue()
            {
                SkipSpace();

                if (_index >= _text.Length) throw new FormatException("Unexpected end of JSON.");

                char ch = _text[_index];

                if (ch == '{' || ch == '[')
                {
                    if (_depth >= MAX_DEPTH) throw new FormatException("JSON nesting exceeds " + MAX_DEPTH + " at position " + _index + ".");

                    _depth++;

                    try
                    {
                        if (ch == '{') return ReadObject();

                        return ReadArray();
                    }
                    finally
                    {
                        _depth--;
                    }
                }
                if (ch == '"') return Str(ReadString());
                if (ch == 't') return ReadLiteral("true", Boolean(true));
                if (ch == 'f') return ReadLiteral("false", Boolean(false));
                if (ch == 'n') return ReadLiteral("null", Null());
                if (ch == '-' || IsDigit(ch)) return ReadNumber();

                throw new FormatException("Unexpected JSON character.");
            }

            private EndsMcpJsonED ReadObject()
            {
                _index++;
                EndsMcpJsonED obj = Obj();
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
                SkipSpace();

                if (_index < _text.Length && _text[_index] == '}')
                {
                    _index++;
                    return obj;
                }

                while (_index < _text.Length)
                {
                    SkipSpace();

                    if (_index >= _text.Length || _text[_index] != '"') throw new FormatException("JSON object key must be a string.");

                    string key = ReadString();

                    if (!keys.Add(key)) throw new FormatException("Duplicate JSON key " + key + " at position " + _index + ".");

                    SkipSpace();

                    if (_index >= _text.Length || _text[_index] != ':') throw new FormatException("JSON object is missing a colon.");

                    _index++;
                    obj.Keys.Add(key);
                    obj.Values.Add(ReadValue());
                    SkipSpace();

                    if (_index >= _text.Length) throw new FormatException("JSON object is not closed.");
                    if (_text[_index] == '}')
                    {
                        _index++;
                        return obj;
                    }

                    if (_text[_index] != ',') throw new FormatException("JSON object is missing a comma.");

                    _index++;
                }

                throw new FormatException("JSON object is not closed.");
            }

            private EndsMcpJsonED ReadArray()
            {
                _index++;
                EndsMcpJsonED array = Arr();
                SkipSpace();

                if (_index < _text.Length && _text[_index] == ']')
                {
                    _index++;
                    return array;
                }

                while (_index < _text.Length)
                {
                    array.Add(ReadValue());
                    SkipSpace();

                    if (_index >= _text.Length) throw new FormatException("JSON array is not closed.");
                    if (_text[_index] == ']')
                    {
                        _index++;
                        return array;
                    }

                    if (_text[_index] != ',') throw new FormatException("JSON array is missing a comma.");

                    _index++;
                }

                throw new FormatException("JSON array is not closed.");
            }

            private string ReadString()
            {
                _index++;
                StringBuilder sb = new StringBuilder();

                while (_index < _text.Length)
                {
                    char ch = _text[_index];
                    _index++;

                    if (ch == '"') return sb.ToString();

                    if (ch < ' ') throw new FormatException("Unescaped JSON control character at position " + (_index - 1) + ".");

                    if (ch != '\\')
                    {
                        sb.Append(ch);
                        continue;
                    }

                    if (_index >= _text.Length) throw new FormatException("JSON string escape is cut off.");

                    char esc = _text[_index];
                    _index++;

                    if (esc == '"') sb.Append('"');
                    else if (esc == '\\') sb.Append('\\');
                    else if (esc == '/') sb.Append('/');
                    else if (esc == 'b') sb.Append('\b');
                    else if (esc == 'f') sb.Append('\f');
                    else if (esc == 'n') sb.Append('\n');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == 't') sb.Append('\t');
                    else if (esc == 'u') sb.Append(ReadHex());
                    else throw new FormatException("JSON string escape is unknown.");
                }

                throw new FormatException("JSON string is not closed.");
            }

            private char ReadHex()
            {
                if (_index + 4 > _text.Length) throw new FormatException("JSON unicode escape is cut off.");

                int code = 0;

                for (int i = 0; i < 4; i++)
                {
                    char ch = _text[_index];
                    _index++;
                    code = code * 16;

                    if (ch >= '0' && ch <= '9') code += ch - '0';
                    else if (ch >= 'a' && ch <= 'f') code += ch - 'a' + 10;
                    else if (ch >= 'A' && ch <= 'F') code += ch - 'A' + 10;
                    else throw new FormatException("JSON unicode escape is invalid.");
                }

                return (char)code;
            }

            private EndsMcpJsonED ReadLiteral(string word, EndsMcpJsonED value)
            {
                if (_index + word.Length > _text.Length) throw new FormatException("Unexpected end of JSON.");

                for (int i = 0; i < word.Length; i++)
                {
                    if (_text[_index + i] != word[i]) throw new FormatException("Unexpected JSON literal.");
                }

                _index += word.Length;
                return value;
            }

            private EndsMcpJsonED ReadNumber()
            {
                int start = _index;

                if (_text[_index] == '-') _index++;
                if (_index >= _text.Length || !IsDigit(_text[_index])) throw new FormatException("Invalid number.");

                if (_text[_index] == '0')
                {
                    _index++;

                    if (_index < _text.Length && IsDigit(_text[_index])) throw new FormatException("JSON number has a leading zero at position " + _index + ".");
                }
                else
                {
                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }

                if (_index < _text.Length && _text[_index] == '.')
                {
                    _index++;

                    if (_index >= _text.Length || !IsDigit(_text[_index])) throw new FormatException("Invalid number.");

                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }

                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    _index++;

                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    if (_index >= _text.Length || !IsDigit(_text[_index])) throw new FormatException("Invalid number.");

                    while (_index < _text.Length && IsDigit(_text[_index])) _index++;
                }

                EndsMcpJsonED value = new EndsMcpJsonED();
                value.Type = Kind.Number;
                value.Text = _text.Substring(start, _index - start);
                return value;
            }

            private static bool IsDigit(char ch)
            {
                return ch >= '0' && ch <= '9';
            }
        }

        #endregion PARSE
    }
}
#endif