using System;
using System.Collections.Generic;
using System.Linq;
using Do_Re_Mi_Lyrics.Models;

namespace Do_Re_Mi_Lyrics.Helper;

public static class WordsMatching
{
    private const int DifferentWordScore = -1;
    private const int ExactWordScore = 2;
    private const int GapScore = -1;
    private const int MaxDifferentWords = 3;
    private const double MinBlockSecondsPerWord = 0.2;
    private const double MinMissingWordDuration = 0.1;
    private const int MinWordsForMissingEdgeWord = 4;
    private const double RealignPadding = 0.6;
    private const int SimilarWordScore = 1;
    private const double SimilarWordThreshold = 0.5;
    private const double StartOffset = 0.15;

    public static List<WhisperLine> MatchLyrics(IReadOnlyList<WhisperWordDto> roughWords, Lyrics lyrics,
        double duration)
    {
        List<LineWords> lines =
        [
            .. lyrics.LyricsLines.Select(x => new LineWords(x)).Where(x => x.Words.Count > 0)
        ];

        roughWords = ExpandNumbers(roughWords);
        List<string> rough = [.. roughWords.Select(x => NormalizeWord(x.Word))];
        int?[] map = AlignWords([.. lines.SelectMany(x => x.Normalized)], rough);
        int offset = 0;

        foreach (LineWords line in lines)
        {
            int?[] lineMap = map[offset..(offset + line.Words.Count)];
            offset += line.Words.Count;

            if (!IsLineMatched(line, lineMap, rough))
            {
                continue;
            }

            int first = lineMap.First(x => x != null)!.Value;
            int last = lineMap.Last(x => x != null)!.Value;
            double from = first > 0 ? roughWords[first - 1].End : 0;
            double to = last + 1 < roughWords.Count ? roughWords[last + 1].Start : duration;

            ApplyTimings(line, FillMissingTimings(GetTimings(lineMap, roughWords), from, to));
        }

        WidenShortBlocks(lines, duration);

        return GetAlignBlocks(lines, duration);
    }

    public static void ApplyAlignResult(IReadOnlyList<WhisperLine> blocks, WhisperAlignResult result)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            WhisperLine block = blocks[i];
            List<LineWords> lines = [.. block.Lines.Select(x => new LineWords(x))];
            WordTiming?[] aligned = GetAlignedTimings(lines, result, i);
            int alignedCount = aligned.Count(x => x != null);
            bool isAlignLost = block.HasPreviousTimings && alignedCount * 2 < aligned.Length;

            WordTiming[] timings = isAlignLost
                ? block.PreviousTimings
                : FillMissingTimings(aligned, block.Start, block.End);

            int offset = 0;

            foreach (LineWords line in lines)
            {
                int count = line.Words.Count;
                ApplyTimings(line, timings[offset..(offset + count)]);
                offset += count;
            }
        }
    }

    public static WhisperLine CreateLineBlock(LyricsLine line, double start, double end)
    {
        return new WhisperLine {Text = GetText([new LineWords(line)]), Start = start, End = end, Lines = [line]};
    }

    private static string NormalizeWord(string word)
    {
        return new string(word.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '\'').ToArray());
    }

    private static List<WhisperWordDto> ExpandNumbers(IReadOnlyList<WhisperWordDto> words)
    {
        List<WhisperWordDto> result = [];

        foreach (WhisperWordDto word in words)
        {
            string normalized = NormalizeWord(word.Word);

            if (!normalized.Any(char.IsDigit))
            {
                result.Add(word);
                continue;
            }

            List<string> parts =
            [
                .. word.Word.Split('/', '\\').Select(NormalizeWord).Where(part => part.Length > 0)
                    .SelectMany(NumberWords.ExpandWord)
            ];
            int totalLength = Math.Max(1, parts.Sum(x => x.Length));
            double start = word.Start;
            int length = 0;

            foreach (string part in parts)
            {
                length += part.Length;
                double end = word.Start + (word.End - word.Start) * length / totalLength;
                result.Add(new WhisperWordDto {Word = part, Start = start, End = end, Line = word.Line});
                start = end;
            }
        }

        return result;
    }

    private static int?[] AlignWords(List<string> lyricsWords, List<string> whisperWords)
    {
        int n = lyricsWords.Count;
        int m = whisperWords.Count;
        int[,] score = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            score[i, 0] = i * GapScore;
        }

        for (int j = 0; j <= m; j++)
        {
            score[0, j] = j * GapScore;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                score[i, j] = Math.Max(score[i - 1, j - 1] + PairScore(lyricsWords[i - 1], whisperWords[j - 1]),
                    Math.Max(score[i - 1, j], score[i, j - 1]) + GapScore);
            }
        }

        int?[] map = new int?[n];
        int a = n;
        int b = m;

        while (a > 0 && b > 0)
        {
            if (score[a, b] == score[a - 1, b] + GapScore)
            {
                a--;
            }
            else if (score[a, b] == score[a - 1, b - 1] + PairScore(lyricsWords[a - 1], whisperWords[b - 1]))
            {
                map[a - 1] = b - 1;
                a--;
                b--;
            }
            else
            {
                b--;
            }
        }

        return map;
    }

    private static int PairScore(string a, string b)
    {
        if (a == b)
        {
            return ExactWordScore;
        }

        return WordSimilarity(a, b) >= SimilarWordThreshold ? SimilarWordScore : DifferentWordScore;
    }

    private static bool IsLineMatched(LineWords line, int?[] map, List<string> rough)
    {
        List<int> aligned = [.. Enumerable.Range(0, map.Length).Where(i => map[i] != null)];
        int missing = map.Length - aligned.Count;

        if (aligned.Count == 0 || missing > 1)
        {
            return false;
        }

        if (missing == 1 && (map.Length < MinWordsForMissingEdgeWord || (map[0] != null && map[^1] != null)))
        {
            return false;
        }

        if (aligned.Zip(aligned.Skip(1)).Any(x => map[x.Second] - map[x.First] != x.Second - x.First))
        {
            return false;
        }

        int same = aligned.Count(i => line.Normalized[i] == rough[map[i]!.Value]);

        return aligned.Count - same <= MaxDifferentWords && same * 2 > map.Length;
    }

    private static WordTiming?[] GetAlignedTimings(List<LineWords> lines, WhisperAlignResult result, int blockIndex)
    {
        List<WhisperWordDto> words = [.. result.Words.Where(x => x.Line == blockIndex)];

        int?[] map = AlignWords([.. lines.SelectMany(x => x.Normalized)],
            [.. words.Select(x => NormalizeWord(x.Word))]);

        return GetTimings(map, words);
    }

    private static WordTiming?[] GetTimings(int?[] map, IReadOnlyList<WhisperWordDto> words)
    {
        return
        [
            .. map.Select(x => x is { } i
                ? new WordTiming(words[i].Start + StartOffset, words[i].End + StartOffset)
                : (WordTiming?) null)
        ];
    }

    private static WordTiming[] FillMissingTimings(WordTiming?[] timings, double from, double to)
    {
        WordTiming[] result = new WordTiming[timings.Length];
        int i = 0;

        while (i < timings.Length)
        {
            if (timings[i] is { } timing)
            {
                result[i] = timing;
                i++;
                continue;
            }

            int first = i;
            while (i < timings.Length && timings[i] == null)
            {
                i++;
            }

            int count = i - first;
            double start = first > 0 ? result[first - 1].End : from;
            double end = i < timings.Length ? timings[i]!.Value.Start : to;

            if (end - start < MinMissingWordDuration * count)
            {
                if (i < timings.Length)
                {
                    WordTiming next = timings[i]!.Value;
                    end = (next.Start + next.End) / 2;
                    timings[i] = next with {Start = end};
                }
                else if (first > 0)
                {
                    WordTiming previous = result[first - 1];
                    start = (previous.Start + previous.End) / 2;
                    result[first - 1] = previous with {End = start};
                }
            }

            double step = Math.Max(0, end - start) / count;

            for (int k = 0; k < count; k++)
            {
                result[first + k] = new WordTiming(start + step * k, start + step * (k + 1));
            }
        }

        return result;
    }

    private static void ApplyTimings(LineWords line, WordTiming[] timings)
    {
        for (int i = 0; i < line.Words.Count; i++)
        {
            List<LyricsWord> parts = line.Words[i];
            WordTiming timing = timings[i];

            if (parts.Count == 1)
            {
                parts[0].StartTime = TimeSpan.FromSeconds(timing.Start);
                parts[0].EndTime = TimeSpan.FromSeconds(timing.End);
                continue;
            }

            int totalLength = Math.Max(1, parts.Sum(x => x.Word.Length));
            double partStart = timing.Start;
            int length = 0;

            foreach (LyricsWord part in parts)
            {
                length += part.Word.Length;
                double partEnd = part == parts[^1]
                    ? timing.End
                    : timing.Start + (timing.End - timing.Start) * length / totalLength;

                part.StartTime = TimeSpan.FromSeconds(partStart);
                part.EndTime = TimeSpan.FromSeconds(partEnd);
                partStart = partEnd;
            }
        }

        line.IsMatched = true;
        line.Timings = timings;
    }

    private static void WidenShortBlocks(List<LineWords> lines, double duration)
    {
        int i = 0;

        while (i < lines.Count)
        {
            if (lines[i].IsMatched)
            {
                i++;
                continue;
            }

            int first = i;
            while (first > 0 && !lines[first - 1].IsMatched)
            {
                first--;
            }

            int last = i;
            while (last + 1 < lines.Count && !lines[last + 1].IsMatched)
            {
                last++;
            }

            int words = lines.Skip(first).Take(last - first + 1).Sum(x => x.Words.Count);
            double start = first > 0 ? lines[first - 1].End : 0;
            double end = last + 1 < lines.Count ? lines[last + 1].Start : duration;

            if (end - start >= words * MinBlockSecondsPerWord || (first == 0 && last == lines.Count - 1))
            {
                i = last + 1;
                continue;
            }

            if (first > 0)
            {
                lines[first - 1].IsMatched = false;
            }

            if (last + 1 < lines.Count)
            {
                lines[last + 1].IsMatched = false;
            }

            i = Math.Max(0, first - 1);
        }
    }

    private static List<WhisperLine> GetAlignBlocks(List<LineWords> lines, double duration)
    {
        List<WhisperLine> blocks = [];
        int i = 0;

        while (i < lines.Count)
        {
            if (lines[i].IsMatched)
            {
                blocks.Add(GetRealignBlock(lines[i], duration));
                i++;
                continue;
            }

            int first = i;
            int last = i;
            while (last + 1 < lines.Count && !lines[last + 1].IsMatched)
            {
                last++;
            }

            LineWords? previous = first > 0 ? lines[first - 1] : null;
            LineWords? next = last + 1 < lines.Count ? lines[last + 1] : null;

            double start = previous?.End ?? 0;
            double end = next?.Start ?? duration;

            if (end <= start)
            {
                start = previous?.Start ?? 0;
                end = next?.End ?? duration;
            }

            List<LineWords> blockLines = [.. lines.Skip(first).Take(last - first + 1)];

            blocks.Add(new WhisperLine
            {
                Text = GetText(blockLines),
                Start = start,
                End = Math.Max(end, start + MinMissingWordDuration),
                Lines = [.. blockLines.Select(x => x.Line)]
            });

            i = last + 1;
        }

        return blocks;
    }

    private static WhisperLine GetRealignBlock(LineWords line, double duration)
    {
        return new WhisperLine
        {
            Text = GetText([line]),
            Start = Math.Max(0, line.Start - RealignPadding),
            End = Math.Min(duration, line.End + RealignPadding),
            Lines = [line.Line],
            PreviousTimings = line.Timings
        };
    }

    private static string GetText(IEnumerable<LineWords> lines)
    {
        return string.Join(" ",
            lines.SelectMany(x => x.Words).Select(parts => string.Concat(parts.Select(x => x.Word)).Replace("|", "")));
    }

    private static double WordSimilarity(string a, string b)
    {
        if (a == b)
        {
            return 1.0;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0.0;
        }

        int distance = LevenshteinDistance(a, b);

        return 1.0 - (double) distance / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] matrix = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }

    private sealed class LineWords
    {
        public LineWords(LyricsLine line)
        {
            Line = line;
            List<LyricsWord> parts = [];

            foreach (LyricsWord word in line.Words)
            {
                parts.Add(word);

                if (word.IsPartOfWord)
                {
                    continue;
                }

                AddWord(parts);
                parts = [];
            }

            AddWord(parts);
        }

        public double End => Timings[^1].End;
        public LyricsLine Line { get; }
        public List<string> Normalized { get; } = [];
        public double Start => Timings[0].Start;
        public List<List<LyricsWord>> Words { get; } = [];
        public bool IsMatched { get; set; }
        public WordTiming[] Timings { get; set; } = [];

        private void AddWord(List<LyricsWord> parts)
        {
            string normalized = NormalizeWord(string.Concat(parts.Select(x => x.Word)));

            if (normalized.Length == 0)
            {
                return;
            }

            Words.Add(parts);
            Normalized.Add(normalized);
        }
    }
}