using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

namespace ExileApi.Localization;

// Embedded into ImGui.NET by the patcher. No additional runtime DLL is required.
public static class ChineseLocalization
{
    private static readonly Dictionary<string, string> Strings = Load();
    private static readonly Dictionary<string, Regex> Patterns = LoadPatterns();
    public static bool IsEnabled() => Strings.Count != 0;

    private static Dictionary<string, string> Load()
    {
        try
        {
            string root = Path.GetDirectoryName(typeof(ChineseLocalization).Assembly.Location)!;
            string language = Environment.GetEnvironmentVariable("EXILEAPI_LANGUAGE")
                ?? File.ReadAllText(Path.Combine(root, "config", "language.txt")).Trim();
            if (!language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
                return new();
            var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(root, "config", "localization", "zh-CN.json")));
            if (catalog == null) return new();
            foreach (var entry in catalog)
                if (entry.Key.Length == 0 || string.IsNullOrEmpty(entry.Value)) return new();
            return catalog;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // An absent or broken catalog must never prevent the overlay from starting.
            return new();
        }
    }

    private static Dictionary<string, Regex> LoadPatterns()
    {
        var patterns = new Dictionary<string, Regex>();
        foreach (string source in Strings.Keys)
        {
            // Only deliberately authored templates with a literal prefix are eligible.
            if (source.IndexOf("{0}", StringComparison.Ordinal) <= 0) continue;
            string pattern = Regex.Escape(source);
            for (int i = 0; i < 5; i++)
                pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">.*?)");
            patterns.Add(source, new Regex("\\A" + pattern + "\\z",
                RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(10)));
        }
        return patterns;
    }

    public static string? Text(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsEnabled()) return value;
        if (Strings.TryGetValue(value, out string? translated)) return translated;
        string trimmed = value.Trim();
        if (Strings.TryGetValue(trimmed, out translated))
        {
            int start = value.IndexOf(trimmed, StringComparison.Ordinal);
            return value[..start] + translated + value[(start + trimmed.Length)..];
        }
        // Labels commonly append a colon. Only translate the entire known phrase.
        if (trimmed.EndsWith(':') && Strings.TryGetValue(trimmed[..^1], out translated))
            return translated + "：" + value[(value.LastIndexOf(':') + 1)..];
        foreach (var entry in Patterns)
        {
            int placeholder = entry.Key.IndexOf("{0}", StringComparison.Ordinal);
            if (!value.StartsWith(entry.Key[..placeholder], StringComparison.Ordinal)) continue;
            try
            {
                Match match = entry.Value.Match(value);
                if (!match.Success) continue;
                translated = Strings[entry.Key];
                var formatted = new StringBuilder();
                for (int i = 0; i < translated.Length; i++)
                {
                    if (i + 2 < translated.Length && translated[i] == '{' && translated[i + 2] == '}' &&
                        translated[i + 1] is >= '0' and <= '4')
                    {
                        // Captured names are data: never expand braces inside them a second time.
                        formatted.Append(match.Groups["p" + translated[i + 1]].Value);
                        i += 2;
                    }
                    else formatted.Append(translated[i]);
                }
                return formatted.ToString();
            }
            catch (RegexMatchTimeoutException) { }
        }
        return value;
    }

    public static string? Label(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsEnabled()) return value;
        int hidden = value.IndexOf("##", StringComparison.Ordinal);
        string visible = hidden < 0 ? value : value[..hidden];
        string translated = Text(visible)!;
        if (visible == translated) return value;
        int stable = value.IndexOf("###", StringComparison.Ordinal);
        // Preserve explicit ### IDs. Otherwise use the full original label as a stable,
        // distinct ID suffix; existing ## suffixes remain part of that identity.
        return translated + (stable < 0 ? "###" + value : value[stable..]);
    }

    public static ReadOnlySpan<char> TextSpan(ReadOnlySpan<char> value) =>
        IsEnabled() ? Text(value.ToString()).AsSpan() : value;

    public static ReadOnlySpan<char> LabelSpan(ReadOnlySpan<char> value) =>
        IsEnabled() ? Label(value.ToString()).AsSpan() : value;

    public static string[] Items(string[] values)
    {
        if (!IsEnabled()) return values;
        var result = new string[values.Length];
        for (int i = 0; i < values.Length; i++) result[i] = Text(values[i])!;
        return result;
    }

    public static string ZeroSeparatedItems(string value)
    {
        if (!IsEnabled()) return value;
        return string.Join('\0', Items(value.Split('\0')));
    }

    public static ReadOnlySpan<char> ZeroSeparatedItemsSpan(ReadOnlySpan<char> value) =>
        IsEnabled() ? ZeroSeparatedItems(value.ToString()).AsSpan() : value;
}
