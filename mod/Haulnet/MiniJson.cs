using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Haulnet;

/// <summary>
/// Minimal JSON reader for the server list: objects become <see cref="Dictionary{TKey,TValue}"/>, arrays
/// <see cref="List{T}"/>, numbers <see cref="double"/>, plus string, bool and null. Unity's JsonUtility silently
/// returned empty arrays for the list's nested types in a mod assembly, and the list is small and fixed.
/// </summary>
internal sealed class MiniJson
{
    private const int MaxDepth = 32;

    private readonly string _text;
    private int _pos;

    private MiniJson(string text) => _text = text;

    /// <summary>Parses <paramref name="text"/>; throws <see cref="FormatException"/> on invalid JSON.</summary>
    public static object? Parse(string text)
    {
        var reader = new MiniJson(text);
        var value = reader.ReadValue(0);
        reader.SkipWhitespace();
        if (reader._pos != text.Length)
        {
            throw new FormatException($"unexpected data at {reader._pos}");
        }

        return value;
    }

    public static Dictionary<string, object?>? Obj(object? value, string key) =>
        value is Dictionary<string, object?> d && d.TryGetValue(key, out var v) ? v as Dictionary<string, object?> : null;

    public static List<object?> Arr(object? value, string key) =>
        value is Dictionary<string, object?> d && d.TryGetValue(key, out var v) && v is List<object?> l ? l : new List<object?>();

    public static string Str(object? value, string key) =>
        value is Dictionary<string, object?> d && d.TryGetValue(key, out var v) && v is string s ? s : "";

    public static int Int(object? value, string key) =>
        value is Dictionary<string, object?> d && d.TryGetValue(key, out var v) && v is double n && n >= int.MinValue && n <= int.MaxValue ? (int)n : 0;

    private object? ReadValue(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new FormatException("nesting too deep");
        }

        SkipWhitespace();
        if (_pos >= _text.Length)
        {
            throw new FormatException("unexpected end");
        }

        switch (_text[_pos])
        {
            case '{':
                return ReadObject(depth);
            case '[':
                return ReadArray(depth);
            case '"':
                return ReadString();
            case 't':
                Expect("true");
                return true;
            case 'f':
                Expect("false");
                return false;
            case 'n':
                Expect("null");
                return null;
            default:
                return ReadNumber();
        }
    }

    private Dictionary<string, object?> ReadObject(int depth)
    {
        var result = new Dictionary<string, object?>();
        _pos++; // {
        SkipWhitespace();
        if (Peek() == '}')
        {
            _pos++;
            return result;
        }

        while (true)
        {
            SkipWhitespace();
            var key = ReadString();
            SkipWhitespace();
            Expect(":");
            result[key] = ReadValue(depth + 1);
            SkipWhitespace();
            var c = Next();
            if (c == '}')
            {
                return result;
            }

            if (c != ',')
            {
                throw new FormatException($"expected ',' or '}}' at {_pos}");
            }
        }
    }

    private List<object?> ReadArray(int depth)
    {
        var result = new List<object?>();
        _pos++; // [
        SkipWhitespace();
        if (Peek() == ']')
        {
            _pos++;
            return result;
        }

        while (true)
        {
            result.Add(ReadValue(depth + 1));
            SkipWhitespace();
            var c = Next();
            if (c == ']')
            {
                return result;
            }

            if (c != ',')
            {
                throw new FormatException($"expected ',' or ']' at {_pos}");
            }
        }
    }

    private string ReadString()
    {
        if (Next() != '"')
        {
            throw new FormatException($"expected string at {_pos}");
        }

        var sb = new StringBuilder();
        while (true)
        {
            var c = Next();
            if (c == '"')
            {
                return sb.ToString();
            }

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            var e = Next();
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (_pos + 4 > _text.Length)
                    {
                        throw new FormatException("bad \\u escape");
                    }

                    sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _pos += 4;
                    break;
                default:
                    throw new FormatException($"bad escape at {_pos}");
            }
        }
    }

    private double ReadNumber()
    {
        var start = _pos;
        while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0)
        {
            _pos++;
        }

        if (!double.TryParse(_text.Substring(start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"bad value at {start}");
        }

        return value;
    }

    private void Expect(string word)
    {
        if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
        {
            throw new FormatException($"expected '{word}' at {_pos}");
        }

        _pos += word.Length;
    }

    private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

    private char Next() => _pos < _text.Length ? _text[_pos++] : throw new FormatException("unexpected end");

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }
}
