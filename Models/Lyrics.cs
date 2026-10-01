using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using Do_Re_Mi_Lyrics.Helper;

namespace Do_Re_Mi_Lyrics.Models;

public partial class Lyrics : INotifyPropertyChanged
{
    private static readonly TimeSpan LargeGap = TimeSpan.FromSeconds(1.5);

    private static readonly TimeSpan MediumGap = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ShortGap = TimeSpan.FromSeconds(0.5);

    private LyricsWord? _currentPlayingWord;
    private LyricsWord? _currentWord;
    private ObservableCollection<LyricsLine> _lyricsLines = [];
    public event PropertyChangedEventHandler? PropertyChanged;

    public int WordCount => LyricsLines.Sum(lyricsLine => lyricsLine.Words.Count);

    public ObservableCollection<LyricsLine> LyricsLines
    {
        get => _lyricsLines;
        set
        {
            _lyricsLines = value;
            OnPropertyChanged();
        }
    }

    internal int CurrentLineIndex => CurrentLine != null ? LyricsLines.IndexOf(CurrentLine) : 0;
    internal LyricsLine? FirstLine => LyricsLines.Count > 0 ? LyricsLines[0] : null;
    internal LyricsLine? LastLine => LyricsLines.Count > 0 ? LyricsLines[^1] : null;

    internal LyricsLine? CurrentLine { get; private set; }

    private IEnumerable<LyricsWord> AllWords => LyricsLines.SelectMany(lyricsLine => lyricsLine.Words);

    public Lyrics Clone()
    {
        Lyrics lyrics = new();
        foreach (LyricsLine lyricsLine in _lyricsLines)
        {
            LyricsLine newLyricsLine = lyricsLine.Clone(lyrics);
            lyrics.LyricsLines.Add(newLyricsLine);
            if (lyricsLine == CurrentLine)
            {
                lyrics.CurrentLine = newLyricsLine;
            }

            for (int i = 0; i < lyricsLine.Words.Count; i++)
            {
                LyricsWord lyricsWord = lyricsLine.Words[i];
                if (lyricsWord.IsSelected)
                {
                    lyrics._currentWord = newLyricsLine.Words[i];
                }

                if (lyricsWord.IsPlaying)
                {
                    lyrics._currentPlayingWord = newLyricsLine.Words[i];
                }
            }
        }

        lyrics.CheckProperTimes();
        return lyrics;
    }

    public void CheckProperTimes()
    {
        foreach (LyricsLine lyricsLine in LyricsLines)
        {
            lyricsLine.RefreshTimes();
        }
    }

    internal static string CleanPastedText(string text)
    {
        IEnumerable<string> lines = text.Replace("\r", "").Split('\n').Select(line =>
        {
            line = SquareBracketTextRegex().Replace(line, "");
            line = PunctuationRegex().Replace(line, "");
            line = SlashRegex().Replace(line, " ");
            line = DotNotBetweenNumbersRegex().Replace(line, "");
            line = NumberWords.ReplaceNumbers(line);
            line = MultipleSpacesRegex().Replace(line, " ");
            return line.Trim().ToLower();
        });

        return string.Join(Environment.NewLine, lines.Where(line => line.Length > 0));
    }

    internal void ParseLyrics(string text, int selectedWordIndex = 0, bool isTest = false)
    {
        try
        {
            text = text.ToLower();
            text = text.Replace(",", "");
            text = DotNotBetweenNumbersRegex().Replace(text, "");
            if (GetLyricsTextWithTimestamps(out int _) == text)
            {
                return;
            }

            if (!isTest)
            {
                Global.MainWindowViewModel.AddToUndoList();
            }

            CurrentLine = null;
            _currentWord = null;
            _currentPlayingWord = null;
            text = text.Replace("\r", "");
            LyricsLines.Clear();
            List<string> lines = [.. text.Split('\n')];
            while (lines.Count > 1 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            foreach (string line in lines)
            {
                LyricsLine lyricsLine = new(this);
                LyricsLines.Add(lyricsLine);
                Match match = PatternLineRegex().Match(line);
                string restOfLine = line;
                if (match.Success)
                {
                    lyricsLine.StartTime = new TimeSpan(0, 0, int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value) * 10);

                    restOfLine = match.Groups[4].Value;
                }

                foreach (string word in restOfLine.Split(' ').Where(word => !string.IsNullOrWhiteSpace(word)))
                {
                    ParseWord(word, lyricsLine, selectedWordIndex);
                }
            }

            UpdateLeadingEmptyLine(false);
            AddTrailingEmptyLine();
            NormalizeTimes();

            if (_currentWord == null)
            {
                SelectFirstLine();
            }

            if (!isTest)
            {
                Global.MainWindowViewModel.IsSaved = false;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void RemoveStartTime()
    {
        try
        {
            if (_currentWord == null)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            SelectPreviousWord();
            if (_currentWord is not { } word)
            {
                return;
            }

            word.Line.IsUncertain = false;
            word.PreviousWord?.EndTime = null;
            word.StartTime = null;
            if (word.IsFirstInLine)
            {
                word.Line.StartTime = TimeSpan.Zero;
            }

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SetEndTimeOfPreviousWord()
    {
        try
        {
            if (_currentWord is not {PreviousWord: { } previousWord} word)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            word.Line.IsUncertain = false;
            TimeSpan currentTime = Global.Audio.CurrentTime;

            if (word.IsFirstInLine)
            {
                if (word.Line.StartTime < currentTime)
                {
                    word.Line.StartTime = currentTime;
                }

                while (word.Line.PreviousLine is {IsEmpty: true} tooShortEmptyLine &&
                       word.Line.StartTime - currentTime <= LargeGap)
                {
                    LyricsLines.Remove(tooShortEmptyLine);
                }

                if (word.Line.PreviousLine is {IsEmpty: true} emptyLine)
                {
                    emptyLine.StartTime = currentTime;
                    emptyLine.PreviousLine?.LastWord?.EndTime = null;
                }
                else
                {
                    previousWord.EndTime = currentTime;
                    ApplyGapBeforeLine(word.Line);
                }
            }
            else
            {
                TimeSpan? startTime = word.EffectiveStartTime;
                previousWord.EndTime = currentTime;
                word.StartTime = startTime > currentTime ? startTime : null;
            }

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SetTimeToCurrentWord()
    {
        try
        {
            if (_currentWord is not { } word)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            word.Line.IsUncertain = false;
            TimeSpan currentTime = Global.Audio.CurrentTime;

            if (word.EndTime <= currentTime)
            {
                word.EndTime = null;
            }

            if (word.IsFirstInLine)
            {
                word.Line.StartTime = currentTime;
                word.StartTime = null;
                ApplyChangedLineStart(word.Line);
            }
            else if (word.PreviousWordInLine is { } previousWord)
            {
                if (!HasStaleEndTime(previousWord) && previousWord.EndTime is { } previousEndTime &&
                    currentTime - previousEndTime >= ShortGap)
                {
                    word.StartTime = currentTime;
                    if (currentTime - previousEndTime > LargeGap)
                    {
                        ApplyGapBeforeLine(SplitLineBefore(word));
                    }
                }
                else
                {
                    previousWord.EndTime = currentTime;
                    word.StartTime = null;
                }
            }

            SelectNextWord();

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SelectWord(LyricsWord? word)
    {
        try
        {
            _currentWord?.IsSelected = false;

            _currentWord = word;
            CurrentLine = word?.Line;
            _currentWord?.IsSelected = true;

            Global.MainWindowViewModel.CheckScrollView();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SelectNextWord()
    {
        try
        {
            if (_currentWord?.NextWord != null)
            {
                SelectWord(_currentWord?.NextWord);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SelectPreviousWord()
    {
        try
        {
            if (_currentWord?.PreviousWord != null)
            {
                SelectWord(_currentWord?.PreviousWord);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SelectNextLine()
    {
        try
        {
            if (CurrentLine?.LastWord?.NextWord is { } nextWord)
            {
                SelectWord(nextWord);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SelectPreviousLine()
    {
        try
        {
            if (CurrentLine?.FirstWord?.PreviousWord is { } previousWord)
            {
                SelectWord(previousWord.Line.FirstWord);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void MoveWordsToNewLine()
    {
        try
        {
            if (_currentWord is not {IsFirstInLine: false} word || CurrentLine == null)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            LyricsLine newLine = SplitLineBefore(word);
            CurrentLine = newLine;
            ApplyGapBeforeLine(newLine);
            NormalizeTimes();
            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void MoveLineToPrevious()
    {
        try
        {
            if (_currentWord is not {IsFirstInLine: true} word || CurrentLine?.PreviousLine is not { } previousLine)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            LyricsLine currentLine = CurrentLine;

            if (previousLine.IsEmpty)
            {
                RemoveEmptyLine(previousLine);
                CloseShortGapBeforeLine(currentLine);
            }
            else
            {
                MergeNextLine(previousLine, currentLine);
                CurrentLine = word.Line;
            }

            NormalizeTimes();
            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void MoveNextLineToCurrent()
    {
        try
        {
            if (CurrentLine is not { } currentLine || _currentWord == null || _currentWord != currentLine.LastWord ||
                currentLine.NextLine is not { } nextLine)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();

            if (nextLine.IsEmpty)
            {
                RemoveEmptyLine(nextLine);
                if (currentLine.NextLine is { } followingLine)
                {
                    CloseShortGapBeforeLine(followingLine);
                }
            }
            else
            {
                MergeNextLine(currentLine, nextLine);
            }

            NormalizeTimes();
            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal string GetLyricsTextWithTimestamps(out int caretIndex)
    {
        caretIndex = 0;
        string text = "";
        try
        {
            foreach (LyricsLine lyricsLine in LyricsLines)
            {
                int textLength = text.Length;
                text += lyricsLine.ToString(out int lineCaretIndex);
                if (lineCaretIndex > 0)
                {
                    caretIndex = textLength + lineCaretIndex;
                }

                text += Environment.NewLine;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
            return "";
        }

        return text;
    }

    internal string GetLyricsText()
    {
        string text = "";
        try
        {
            foreach (LyricsLine lyricsLine in LyricsLines)
            {
                text += lyricsLine.LineText;
                text += Environment.NewLine;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
            return "";
        }

        return text;
    }

    internal void SelectFirstLine()
    {
        try
        {
            SelectWord(AllWords.FirstOrDefault());
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal TimeSpan GetCurrentWordStartTime()
    {
        for (LyricsWord? word = _currentWord; word != null; word = word.PreviousWord)
        {
            if (word.EffectiveStartTime is { } startTime)
            {
                return startTime;
            }
        }

        return TimeSpan.Zero;
    }

    internal void ChangePlayingWord()
    {
        TimeSpan currentTime = Global.Audio.CurrentTime;
        if (_currentPlayingWord != null && IsPlayingAt(_currentPlayingWord, currentTime))
        {
            return;
        }

        if (_currentPlayingWord != null)
        {
            _currentPlayingWord.IsPlaying = false;
            _currentPlayingWord = null;
        }

        LyricsWord? word = AllWords.FirstOrDefault(word => IsPlayingAt(word, currentTime));
        if (word == null)
        {
            return;
        }

        word.IsPlaying = true;
        _currentPlayingWord = word;
    }

    internal void ChangeStartTimeOfCurrentWord(double seconds)
    {
        if (_currentWord == null)
        {
            return;
        }

        Global.MainWindowViewModel.AddToUndoList();
        CurrentLine?.IsUncertain = false;
        ShiftStartOfWord(_currentWord, TimeSpan.FromSeconds(seconds));
        Global.MainWindowViewModel.IsSaved = false;
    }

    internal void ChangeStartingTimeOfAllWordsFromCurrent(double seconds)
    {
        if (_currentWord is not { } currentWord)
        {
            return;
        }

        Global.MainWindowViewModel.AddToUndoList();
        CurrentLine?.IsUncertain = false;
        TimeSpan shift = TimeSpan.FromSeconds(seconds);
        LyricsLine currentLine = currentWord.Line;

        ShiftStartOfWord(currentWord, shift);
        currentWord.EndTime = ShiftTime(currentWord.EndTime, shift);
        foreach (LyricsWord word in currentLine.Words.Skip(currentLine.Words.IndexOf(currentWord) + 1))
        {
            ShiftExplicitTimes(word, shift);
        }

        foreach (LyricsLine line in LyricsLines.Skip(LyricsLines.IndexOf(currentLine) + 1))
        {
            line.StartTime = ShiftTime(line.StartTime, shift) ?? TimeSpan.Zero;
            foreach (LyricsWord word in line.Words)
            {
                ShiftExplicitTimes(word, shift);
            }
        }

        NormalizeTimes();
        Global.MainWindowViewModel.IsSaved = false;
    }

    internal void CloseShortGapsAndInsertEmptyLines()
    {
        foreach (LyricsLine line in LyricsLines.Where(line => line.IsEmpty).ToList())
        {
            LyricsLines.Remove(line);
        }

        foreach (LyricsLine line in LyricsLines)
        {
            if (line.FirstWord is {StartTime: { } firstWordStartTime} firstWord)
            {
                line.StartTime = firstWordStartTime;
                firstWord.StartTime = null;
            }
        }

        ApplyTimingCorrections();

        if (CurrentLine == null || !LyricsLines.Contains(CurrentLine))
        {
            SelectFirstLine();
        }
    }

    internal void MarkUncertainLines(IReadOnlySet<TimeSpan> lineStartTimes)
    {
        foreach (LyricsLine line in LyricsLines.Where(line => !line.IsEmpty && lineStartTimes.Contains(line.StartTime)))
        {
            line.IsUncertain = true;
        }
    }

    internal bool ApplyTimingCorrectionsWithUndo()
    {
        Lyrics correctedLyrics = Clone();
        correctedLyrics.ApplyTimingCorrections();
        if (correctedLyrics.GetLyricsTextWithTimestamps(out _) == GetLyricsTextWithTimestamps(out _))
        {
            return false;
        }

        Global.MainWindowViewModel.AddToUndoList();
        ApplyTimingCorrections();
        return true;
    }

    internal void ApplyRealignedLine(LyricsLine line)
    {
        if (line.FirstWord is {StartTime: { } firstWordStartTime} firstWord)
        {
            line.StartTime = firstWordStartTime;
            firstWord.StartTime = null;
        }

        CloseShortGapsInsideLine(line);
        List<LyricsLine> lines = SplitLineAtLargeGaps(line);
        ApplyChangedLineStart(lines[0]);
        foreach (LyricsLine splitLine in lines.Skip(1))
        {
            ApplyGapBeforeLine(splitLine);
        }

        ApplyChangedLineEnd(lines[^1]);
        NormalizeTimes();
    }

    private void MergeNextLine(LyricsLine line, LyricsLine nextLine)
    {
        if (nextLine.FirstWord is { } firstWord)
        {
            firstWord.StartTime ??= nextLine.StartTime;
        }

        line.IsUncertain |= nextLine.IsUncertain;
        foreach (LyricsWord movedWord in nextLine.Words.ToList())
        {
            nextLine.RemoveWord(movedWord);
            movedWord.Line = line;
            line.AddWord(movedWord);
        }

        LyricsLines.Remove(nextLine);
        NormalizeTimes();
        CloseShortGapsInsideLine(line);
    }

    private void MergeShortLines()
    {
        foreach (LyricsLine line in LyricsLines.ToList().Where(line => !line.IsEmpty))
        {
            while (LyricsLines.Contains(line) && line.NextLine is {IsEmpty: false} nextLine &&
                   nextLine.StartTime > line.StartTime && nextLine.StartTime - line.StartTime < LargeGap)
            {
                MergeNextLine(line, nextLine);
            }
        }
    }

    private LyricsLine SplitLineBefore(LyricsWord word)
    {
        LyricsLine line = word.Line;
        TimeSpan newLineStartTime = word.EffectiveStartTime ?? TimeSpan.Zero;
        LyricsLine newLine = new(this) {IsUncertain = line.IsUncertain};
        LyricsLines.Insert(LyricsLines.IndexOf(line) + 1, newLine);
        newLine.StartTime = newLineStartTime;

        foreach (LyricsWord movedWord in line.Words.Skip(line.Words.IndexOf(word)).ToList())
        {
            line.RemoveWord(movedWord);
            movedWord.Line = newLine;
            newLine.AddWord(movedWord);
        }

        word.StartTime = null;
        return newLine;
    }

    private List<LyricsLine> SplitLineAtLargeGaps(LyricsLine line)
    {
        List<LyricsLine> lines = [line];
        for (int i = 1; i < lines[^1].Words.Count; i++)
        {
            LyricsWord previousWord = lines[^1].Words[i - 1];
            LyricsWord word = lines[^1].Words[i];
            if (word.StartTime - previousWord.EndTime > LargeGap)
            {
                lines.Add(SplitLineBefore(word));
                i = 0;
            }
        }

        return lines;
    }

    private static void CloseShortGapsInsideLine(LyricsLine line)
    {
        for (int i = 1; i < line.Words.Count; i++)
        {
            LyricsWord previousWord = line.Words[i - 1];
            LyricsWord word = line.Words[i];
            if (word.StartTime is not { } startTime || previousWord.EndTime is not { } previousEndTime)
            {
                continue;
            }

            TimeSpan gap = startTime - previousEndTime;
            if (gap >= TimeSpan.Zero && gap < ShortGap)
            {
                previousWord.EndTime = startTime;
                word.StartTime = null;
            }
        }
    }

    private static void MoveLineStartAfterOverlappingWord(LyricsLine line)
    {
        if (line.FirstWord is not { } firstWord || line.PreviousLine is not {LastWord.EndTime: { } previousEndTime} ||
            previousEndTime <= line.StartTime || !(firstWord.EffectiveEndTime > previousEndTime))
        {
            return;
        }

        line.StartTime = previousEndTime;
        if (firstWord.StartTime < previousEndTime)
        {
            firstWord.StartTime = null;
        }
    }

    private static bool HasStaleEndTime(LyricsWord word)
    {
        return word.EndTime <= word.EffectiveStartTime;
    }

    private static bool IsPlayingAt(LyricsWord word, TimeSpan time)
    {
        return time >= word.EffectiveStartTime && time <= word.EffectiveEndTime;
    }

    private static TimeSpan? ShiftTime(TimeSpan? time, TimeSpan shift)
    {
        return time is { } value ? value + shift < TimeSpan.Zero ? TimeSpan.Zero : value + shift : null;
    }

    private static void ShiftExplicitTimes(LyricsWord word, TimeSpan shift)
    {
        word.StartTime = ShiftTime(word.StartTime, shift);
        word.EndTime = ShiftTime(word.EndTime, shift);
    }

    private static void ShiftStartOfWord(LyricsWord word, TimeSpan shift)
    {
        if (word.IsFirstInLine)
        {
            word.Line.StartTime = ShiftTime(word.Line.StartTime, shift) ?? TimeSpan.Zero;
            word.StartTime = ShiftTime(word.StartTime, shift);
            word.PreviousWord?.EndTime = ShiftTime(word.PreviousWord.EndTime, shift);
            return;
        }

        word.StartTime = ShiftTime(word.StartTime, shift);
        word.PreviousWordInLine?.EndTime = ShiftTime(word.PreviousWordInLine.EndTime, shift);
    }

    [GeneratedRegex(@"(?<!\d)\.(?!\d)")]
    private static partial Regex DotNotBetweenNumbersRegex();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"[,?!();""“”„]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"[/\\]")]
    private static partial Regex SlashRegex();

    [GeneratedRegex(@"\[(?![0-9]{2}\:[0-9]{2}\.[0-9]{2}\])[^\]]*\]")]
    private static partial Regex SquareBracketTextRegex();

    [GeneratedRegex(@"\[([0-9]{2})\:([0-9]{2})\.([0-9]{2})\](.*)")]
    private static partial Regex PatternLineRegex();

    [GeneratedRegex(@"\<([0-9]{2})\:([0-9]{2})\.([0-9]{2})\>")]
    private static partial Regex WordTimeRegex();

    [GeneratedRegex(@"(?<!^)(?=-)")]
    private static partial Regex HyphenSplitRegex();

    private void ApplyTimingCorrections()
    {
        foreach (LyricsLine line in LyricsLines.ToList())
        {
            CloseShortGapsInsideLine(line);
            SplitLineAtLargeGaps(line);
        }

        foreach (LyricsLine line in LyricsLines.ToList())
        {
            MoveLineStartAfterOverlappingWord(line);
            ApplyGapBeforeLine(line);
        }

        MergeShortLines();
        UpdateLeadingEmptyLine(true);
        AddTrailingEmptyLine();
        NormalizeTimes();
    }

    private void ApplyChangedLineStart(LyricsLine line)
    {
        if (line.PreviousLine is {IsEmpty: true} emptyLine && line.StartTime - emptyLine.StartTime <= LargeGap)
        {
            RemoveEmptyLine(emptyLine);
        }

        if (line.PreviousLine?.LastWord is { } previousLastWord && previousLastWord.EndTime > line.StartTime)
        {
            previousLastWord.EndTime = null;
        }

        ApplyGapBeforeLine(line);
        UpdateLeadingEmptyLine(true);
    }

    private void ApplyChangedLineEnd(LyricsLine line)
    {
        if (line.LastWord is not {EndTime: { } endTime} lastWord || line.NextLine is not { } nextLine)
        {
            return;
        }

        if (!nextLine.IsEmpty)
        {
            if (endTime > nextLine.StartTime)
            {
                lastWord.EndTime = null;
                return;
            }

            ApplyGapBeforeLine(nextLine);
            return;
        }

        nextLine.StartTime = endTime;
        lastWord.EndTime = null;
        if (nextLine.NextLine is {IsEmpty: false} followingLine &&
            followingLine.StartTime - nextLine.StartTime <= LargeGap)
        {
            RemoveEmptyLine(nextLine);
            ApplyChangedLineEnd(line);
        }
    }

    private void UpdateLeadingEmptyLine(bool isCorrectionAllowed)
    {
        if (FirstLine is not { } firstLine)
        {
            return;
        }

        if (isCorrectionAllowed && firstLine.IsEmpty && firstLine.StartTime == TimeSpan.Zero &&
            firstLine.NextLine is {IsEmpty: false} nextLine && nextLine.StartTime <= LargeGap)
        {
            LyricsLines.Remove(firstLine);
            firstLine = nextLine;
        }

        if (firstLine.IsEmpty)
        {
            return;
        }

        if (isCorrectionAllowed && firstLine.StartTime < MediumGap)
        {
            firstLine.StartTime = TimeSpan.Zero;
        }
        else if (firstLine.StartTime > LargeGap)
        {
            InsertEmptyLineBefore(firstLine, TimeSpan.Zero);
        }
    }

    private void ParseWord(string word, LyricsLine lyricsLine, int selectedWordIndex)
    {
        List<(string Text, TimeSpan? StartTime, TimeSpan? EndTime)> timedParts = [];
        TimeSpan? pendingStartTime = null;
        int position = 0;

        foreach (Match match in WordTimeRegex().Matches(word))
        {
            TimeSpan time = new(0, 0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value) * 10);
            string text = word[position..match.Index];
            position = match.Index + match.Length;

            if (text.Length == 0)
            {
                pendingStartTime = time;
                continue;
            }

            timedParts.Add((text, pendingStartTime, time));
            pendingStartTime = null;
        }

        if (position < word.Length)
        {
            timedParts.Add((word[position..], pendingStartTime, null));
        }

        List<LyricsWord> parsedWords = [];
        foreach ((string text, TimeSpan? startTime, TimeSpan? endTime) in timedParts)
        {
            List<string> subWords =
            [
                .. text.Split('|').SelectMany(subWord => HyphenSplitRegex().Split(subWord))
                    .Where(subWord => subWord.Length > 0)
            ];
            for (int i = 0; i < subWords.Count; i++)
            {
                LyricsWord lyricsWord = new(lyricsLine) {Word = subWords[i], IsPartOfWord = true};
                lyricsLine.AddWord(lyricsWord);
                lyricsWord.StartTime = i == 0 ? startTime : null;
                lyricsWord.EndTime = i == subWords.Count - 1 ? endTime : null;
                parsedWords.Add(lyricsWord);

                if (WordCount == selectedWordIndex)
                {
                    SelectWord(lyricsWord);
                }
            }
        }

        if (parsedWords.Count > 0)
        {
            parsedWords[^1].IsPartOfWord = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ApplyGapBeforeLine(LyricsLine line)
    {
        if (line.IsEmpty || line.PreviousLine is not {LastWord: {EndTime: { } previousEndTime} previousWord})
        {
            return;
        }

        if (HasStaleEndTime(previousWord))
        {
            previousWord.EndTime = null;
            return;
        }

        if (line.StartTime - previousEndTime > LargeGap)
        {
            InsertEmptyLineBefore(line, previousEndTime);
            previousWord.EndTime = null;
        }
        else
        {
            CloseShortGapBeforeLine(line);
        }
    }

    private static void CloseShortGapBeforeLine(LyricsLine line)
    {
        if (line.IsEmpty || line.PreviousLine is not {LastWord: {EndTime: { } previousEndTime} previousWord})
        {
            return;
        }

        TimeSpan gap = line.StartTime - previousEndTime;
        if (gap >= TimeSpan.Zero && gap < ShortGap)
        {
            previousWord.EndTime = null;
        }
    }

    private void AddTrailingEmptyLine()
    {
        if (LastLine is not {IsEmpty: false} lastLine)
        {
            return;
        }

        LyricsLine newLine = new(this);
        LyricsLines.Add(newLine);
        newLine.StartTime = lastLine.LastWord?.EndTime ?? lastLine.StartTime;
    }

    private void InsertEmptyLineBefore(LyricsLine line, TimeSpan startTime)
    {
        LyricsLine newLine = new(this);
        LyricsLines.Insert(LyricsLines.IndexOf(line), newLine);
        newLine.StartTime = startTime;
    }

    private void NormalizeTimes()
    {
        foreach (LyricsLine line in LyricsLines)
        {
            for (int i = 0; i < line.Words.Count; i++)
            {
                LyricsWord word = line.Words[i];
                if (i == 0)
                {
                    if (word.StartTime == line.StartTime)
                    {
                        word.StartTime = null;
                    }

                    continue;
                }

                LyricsWord previousWord = line.Words[i - 1];
                if (word.StartTime is { } startTime && previousWord.EndTime == null)
                {
                    previousWord.EndTime = startTime;
                    word.StartTime = null;
                }
                else if (word.StartTime != null && word.StartTime == previousWord.EndTime)
                {
                    word.StartTime = null;
                }
            }

            if (line.LastWord is {EndTime: { } endTime} lastWord && line.NextLine?.StartTime == endTime)
            {
                lastWord.EndTime = null;
            }
        }
    }

    private void RemoveEmptyLine(LyricsLine emptyLine)
    {
        if (emptyLine.PreviousLine?.LastWord is {EndTime: null} previousWord)
        {
            previousWord.EndTime = emptyLine.StartTime;
        }

        LyricsLines.Remove(emptyLine);
    }
}