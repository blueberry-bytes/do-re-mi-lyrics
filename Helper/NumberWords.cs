using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Do_Re_Mi_Lyrics.Helper;

internal static partial class NumberWords
{
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
    ];

    private static readonly Dictionary<string, string> Ordinals = new()
    {
        ["one"] = "first",
        ["two"] = "second",
        ["three"] = "third",
        ["five"] = "fifth",
        ["eight"] = "eighth",
        ["nine"] = "ninth",
        ["twelve"] = "twelfth"
    };

    private static readonly string[] Tens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    internal static List<string> ExpandWord(string word)
    {
        List<string> parts = [];

        foreach (Match match in NumberOrTextRegex().Matches(word))
        {
            if (char.IsDigit(match.Value[0]))
            {
                parts.AddRange(long.TryParse(match.Value, out long number) ? ToWords(number) : [match.Value]);
            }
            else if (parts.Count > 0 && match.Value is "s" or "'s")
            {
                parts[^1] = ToPlural(parts[^1]);
            }
            else if (parts.Count > 0 && match.Value is "st" or "nd" or "rd" or "th")
            {
                parts[^1] = ToOrdinal(parts[^1]);
            }
            else
            {
                parts.Add(match.Value);
            }
        }

        return parts;
    }

    internal static string ReplaceNumbers(string line)
    {
        return string.Concat(TimeTagRegex().Split(line).Select(part =>
            TimeTagRegex().IsMatch(part)
                ? part
                : NumberRegex().Replace(part, match => $" {string.Join(" ", ToWords(match))} ")));
    }

    internal static List<string> ToWords(long number)
    {
        switch (number)
        {
            case < 20:
                return [Ones[number]];
            case < 100:
                return number % 10 == 0 ? [Tens[number / 10]] : [Tens[number / 10], Ones[number % 10]];
            case < 1000:
                return [Ones[number / 100], "hundred", .. number % 100 > 0 ? ToWords(number % 100) : []];
            case < 10000 when number % 100 >= 10:
                return [.. ToWords(number / 100), .. ToWords(number % 100)];
            case < 1000000:
                return [.. ToWords(number / 1000), "thousand", .. number % 1000 > 0 ? ToWords(number % 1000) : []];
            default:
                return [number.ToString()];
        }
    }

    private static List<string> ToWords(Match match)
    {
        if (!long.TryParse(match.Groups[1].Value, out long number))
        {
            return [match.Value];
        }

        List<string> words = ToWords(number);

        if (match.Groups[2].Success)
        {
            words.Add("point");
            words.AddRange(match.Groups[2].Value.Select(digit => Ones[digit - '0']));
        }

        switch (match.Groups[3].Value)
        {
            case "s" or "'s":
                words[^1] = ToPlural(words[^1]);
                break;
            case "st" or "nd" or "rd" or "th":
                words[^1] = ToOrdinal(words[^1]);
                break;
        }

        return words;
    }

    private static string ToOrdinal(string word)
    {
        if (Ordinals.TryGetValue(word, out string? ordinal))
        {
            return ordinal;
        }

        return word.EndsWith('y') ? $"{word[..^1]}ieth" : $"{word}th";
    }

    private static string ToPlural(string word)
    {
        return word.EndsWith('y') ? $"{word[..^1]}ies" : $"{word}s";
    }

    [GeneratedRegex(@"(\d+)(?:\.(\d+))?(?:('s|s|st|nd|rd|th)(?![a-z]))?", RegexOptions.IgnoreCase)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\d+|'s|\D+")]
    private static partial Regex NumberOrTextRegex();

    [GeneratedRegex(@"(\[\d{2}:\d{2}\.\d{2}\]|<\d{2}:\d{2}\.\d{2}>)")]
    private static partial Regex TimeTagRegex();
}