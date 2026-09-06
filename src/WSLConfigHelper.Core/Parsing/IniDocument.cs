using System.Text;
using System.Text.RegularExpressions;

namespace WSLConfigHelper.Core.Parsing;

public abstract class IniItem
{
    public abstract string RawText { get; }
}

public class IniBlankLine : IniItem
{
    public string OriginalWhitespace { get; }

    public IniBlankLine(string originalWhitespace = "")
    {
        OriginalWhitespace = originalWhitespace;
    }

    public override string RawText => OriginalWhitespace;
}

public class IniCommentLine : IniItem
{
    public string Text { get; set; }

    public IniCommentLine(string text)
    {
        Text = text;
    }

    public override string RawText => Text;
}

public class IniSectionHeader : IniItem
{
    public string SectionName { get; set; }
    public string? TrailingComment { get; set; }

    public IniSectionHeader(string sectionName, string? trailingComment = null)
    {
        SectionName = sectionName;
        TrailingComment = trailingComment;
    }

    public override string RawText => string.IsNullOrEmpty(TrailingComment)
        ? $"[{SectionName}]"
        : $"[{SectionName}] {TrailingComment}";
}

public class IniKeyValueLine : IniItem
{
    public string SectionName { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string? InlineComment { get; set; }
    public string DelimiterSpacing { get; set; } = "=";

    public IniKeyValueLine(string sectionName, string key, string value, string? inlineComment = null, string delimiterSpacing = "=")
    {
        SectionName = sectionName;
        Key = key;
        Value = value;
        InlineComment = inlineComment;
        DelimiterSpacing = delimiterSpacing;
    }

    public override string RawText
    {
        get
        {
            var text = $"{Key}{DelimiterSpacing}{Value}";
            if (!string.IsNullOrWhiteSpace(InlineComment))
            {
                text += $" {InlineComment.TrimStart()}";
            }
            return text;
        }
    }
}

public class IniRawLine : IniItem
{
    public string Text { get; set; }

    public IniRawLine(string text)
    {
        Text = text;
    }

    public override string RawText => Text;
}

public class IniDocument
{
    private readonly List<IniItem> _items = new();

    public IReadOnlyList<IniItem> Items => _items;

    public static IniDocument Parse(string content)
    {
        var doc = new IniDocument();
        using var reader = new StringReader(content);
        string currentSection = string.Empty;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                doc._items.Add(new IniBlankLine(line));
                continue;
            }

            if (trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                doc._items.Add(new IniCommentLine(line));
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.Contains(']'))
            {
                var closingIndex = trimmed.IndexOf(']');
                var sectionName = trimmed.Substring(1, closingIndex - 1).Trim();
                var remainder = trimmed.Substring(closingIndex + 1).Trim();
                currentSection = sectionName;
                doc._items.Add(new IniSectionHeader(sectionName, string.IsNullOrEmpty(remainder) ? null : remainder));
                continue;
            }

            // Key-Value pair
            var equalIndex = line.IndexOf('=');
            if (equalIndex > 0)
            {
                var rawKey = line.Substring(0, equalIndex);
                var key = rawKey.Trim();
                var rawAfterEqual = line.Substring(equalIndex + 1);

                // Check for inline comments (# or ;) but beware of paths or quotes
                string value;
                string? comment = null;

                var commentIndex = FindInlineCommentIndex(rawAfterEqual);
                if (commentIndex >= 0)
                {
                    value = rawAfterEqual.Substring(0, commentIndex).Trim();
                    comment = rawAfterEqual.Substring(commentIndex).Trim();
                }
                else
                {
                    value = rawAfterEqual.Trim();
                }

                // Determine spacing style: e.g. " = " or "="
                var leftSpaces = rawKey.Length - rawKey.TrimEnd().Length;
                var rightSpaces = rawAfterEqual.Length - rawAfterEqual.TrimStart().Length;
                var delimiter = new string(' ', leftSpaces) + "=" + new string(' ', rightSpaces);
                if (string.IsNullOrEmpty(delimiter))
                {
                    delimiter = "=";
                }

                doc._items.Add(new IniKeyValueLine(currentSection, key, value, comment, delimiter));
                continue;
            }

            doc._items.Add(new IniRawLine(line));
        }

        return doc;
    }

    private static int FindInlineCommentIndex(string text)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && (c == '#' || c == ';'))
            {
                // Must be preceded by whitespace or at start to count as inline comment
                if (i == 0 || char.IsWhiteSpace(text[i - 1]))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    public IEnumerable<string> GetSections()
    {
        return _items.OfType<IniSectionHeader>()
            .Select(h => h.SectionName)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public bool HasSection(string section)
    {
        return _items.OfType<IniSectionHeader>()
            .Any(h => string.Equals(h.SectionName, section, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasKey(string section, string key)
    {
        return _items.OfType<IniKeyValueLine>()
            .Any(kv => string.Equals(kv.SectionName, section, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetValue(string section, string key)
    {
        var item = _items.OfType<IniKeyValueLine>()
            .LastOrDefault(kv => string.Equals(kv.SectionName, section, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        return item?.Value;
    }

    public IReadOnlyDictionary<string, string> GetSectionEntries(string section)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _items.OfType<IniKeyValueLine>()
                     .Where(kv => string.Equals(kv.SectionName, section, StringComparison.OrdinalIgnoreCase)))
        {
            dict[kv.Key] = kv.Value;
        }
        return dict;
    }

    public void SetValue(string section, string key, string value, string? comment = null)
    {
        var existing = _items.OfType<IniKeyValueLine>()
            .FirstOrDefault(kv => string.Equals(kv.SectionName, section, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Value = value;
            if (comment != null)
            {
                existing.InlineComment = comment;
            }
            return;
        }

        // Key doesn't exist. Find section or create section.
        var sectionIndex = _items.FindIndex(i => i is IniSectionHeader h &&
                                                 string.Equals(h.SectionName, section, StringComparison.OrdinalIgnoreCase));

        if (sectionIndex == -1)
        {
            // Section does not exist. Append section header and key-value at the end.
            if (_items.Count > 0 && !(_items[^1] is IniBlankLine))
            {
                _items.Add(new IniBlankLine());
            }

            _items.Add(new IniSectionHeader(section));
            _items.Add(new IniKeyValueLine(section, key, value, comment, "="));
            return;
        }

        // Section exists. Find where this section ends (start of next section or end of list).
        int insertIndex = _items.Count;
        for (int i = sectionIndex + 1; i < _items.Count; i++)
        {
            if (_items[i] is IniSectionHeader)
            {
                insertIndex = i;
                break;
            }
        }

        // Insert before blank line or next section header
        if (insertIndex > sectionIndex + 1 && _items[insertIndex - 1] is IniBlankLine)
        {
            insertIndex--;
        }

        _items.Insert(insertIndex, new IniKeyValueLine(section, key, value, comment, "="));
    }

    public bool RemoveKey(string section, string key)
    {
        var item = _items.OfType<IniKeyValueLine>()
            .FirstOrDefault(kv => string.Equals(kv.SectionName, section, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));

        if (item != null)
        {
            _items.Remove(item);
            return true;
        }

        return false;
    }

    public string Serialize(string newLine = "\n")
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _items.Count; i++)
        {
            sb.Append(_items[i].RawText);
            if (i < _items.Count - 1 || _items[i] is IniBlankLine)
            {
                sb.Append(newLine);
            }
        }
        return sb.ToString();
    }
}
