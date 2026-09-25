using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace Do_Re_Mi_Lyrics.Models;

public partial class Lyrics : INotifyPropertyChanged
{
    private LyricsLine? _currentLine;
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

    internal int CurrentLineIndex => _currentLine != null ? LyricsLines.IndexOf(_currentLine) : 0;
    internal LyricsLine? FirstLine => LyricsLines.Count > 0 ? LyricsLines[0] : null;
    internal LyricsLine? LastLine => LyricsLines.Count > 0 ? LyricsLines[^1] : null;

    public Lyrics Clone()
    {
        Lyrics lyrics = new();
        foreach (LyricsLine lyricsLine in _lyricsLines)
        {
            LyricsLine newLyricsLine = lyricsLine.Clone(lyrics);
            lyrics.LyricsLines.Add(newLyricsLine);
            newLyricsLine.CheckProperTime();
            if (lyricsLine == _currentLine)
            {
                lyrics._currentLine = lyricsLine;
            }

            for (int i = 0; i < lyricsLine.Words.Count; i++)
            {
                LyricsWord lyricsWord = lyricsLine.Words[i];
                if (lyricsWord.IsSelected)
                {
                    lyrics._currentWord = lyrics.LyricsLines[^1].Words[i];
                }

                if (lyricsWord.IsPlaying)
                {
                    lyrics._currentPlayingWord = lyrics.LyricsLines[^1].Words[i];
                }
            }
        }

        return lyrics;
    }

    internal void ParseLyrics(string text, int selectedWordIndex = 0, bool isTest = false)
    {
        try
        {
            text = text.ToLower();
            text = text.Replace(",", "");
            text = DotNotBetweenNumbersRegex().Replace(text, "");
            if (GetLyricsText(out int _) == text)
            {
                return;
            }

            if (!isTest)
            {
                Global.MainWindowViewModel.AddToUndoList();
            }

            _currentLine = null;
            _currentWord = null;
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
                Regex regex = PatternLineRegex();
                Match match = regex.Match(line);
                string restOfLine = line;
                TimeSpan lineStartTime = TimeSpan.Zero;
                if (match.Success)
                {
                    lineStartTime = new TimeSpan(0, 0, int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value) * 10);

                    restOfLine = match.Groups[4].Value;
                }

                List<string> words = [.. restOfLine.Split(' ')];
                foreach (string word in words.Where(word =>
                             !string.IsNullOrWhiteSpace(word)))
                {
                    ParseWordTimes(word, lyricsLine, lineStartTime, selectedWordIndex);
                }

                lyricsLine.CheckProperTime();
            }

            if (FirstLine?.StartTime > new TimeSpan(0, 0, 3))
            {
                LyricsLine newLine = new(this);
                LyricsLines.Insert(0, newLine);
            }

            if (LastLine?.FirstWord != LastLine?.LastWord || !string.IsNullOrWhiteSpace(LastLine?.FirstWord?.Word))
            {
                LyricsLine newLine = new(this);
                LyricsLines.Add(newLine);
            }

            foreach (LyricsLine line in LyricsLines)
            {
                foreach (LyricsWord word in line.Words)
                {
                    if (word.EndTime == new TimeSpan(0))
                    {
                        word.EndTime = word.NextWord?.StartTime ?? new TimeSpan(0);
                    }
                }
            }

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
            MessageBox.Show(ex.Message);
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

            _currentWord?.PreviousWord?.EndTime = new TimeSpan(0, 0, 0);

            _currentWord?.StartTime = new TimeSpan(0, 0, 0);

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void SetEndingTimeToPreviousLine()
    {
        try
        {
            Global.MainWindowViewModel.AddToUndoList();
            TimeSpan timeSpan = Global.Audio.CurrentTime;

            _currentWord?.PreviousWord?.EndTime = timeSpan;

            if (_currentWord?.StartTime < timeSpan)
            {
                _currentWord.StartTime = timeSpan;
            }

            _currentWord?.UpdateStartTimeText();

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void SetTimeToCurrentWord()
    {
        try
        {
            Global.MainWindowViewModel.AddToUndoList();
            TimeSpan currentTime = Global.Audio.CurrentTime;
            if (_currentWord?.PreviousWord != null && _currentLine?.PreviousLine != null &&
                _currentWord == _currentLine?.FirstWord && _currentWord.PreviousWord.EndTime > TimeSpan.Zero &&
                currentTime - _currentWord.PreviousWord.EndTime > TimeSpan.FromSeconds(1.5))
            {
                InsertEmptyLineBeforeCurrent(currentTime);
            }

            if (_currentWord?.PreviousWord != null && (_currentWord.PreviousWord.EndTime == TimeSpan.Zero ||
                                                       currentTime - _currentWord.PreviousWord.EndTime <
                                                       TimeSpan.FromSeconds(0.5)))
            {
                _currentWord.PreviousWord.EndTime = currentTime;
            }

            if (_currentWord != null)
            {
                _currentWord.StartTime = currentTime;

                if (_currentWord == FirstLine?.FirstWord && currentTime > TimeSpan.Zero)
                {
                    InsertEmptyLineAtBeginning(currentTime);
                }
            }

            SelectNextWord();

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void SelectWord(LyricsWord? word)
    {
        try
        {
            _currentWord?.IsSelected = false;

            _currentWord = word;
            _currentLine = word?.Line;
            _currentWord?.IsSelected = true;

            Global.MainWindowViewModel.CheckScrollView();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
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
            MessageBox.Show(ex.Message);
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
            MessageBox.Show(ex.Message);
        }
    }

    internal void SelectNextLine()
    {
        try
        {
            if (_currentLine?.NextLine == null)
            {
                return;
            }

            SelectWord(_currentLine.NextLine?.FirstWord);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void SelectPreviousLine()
    {
        try
        {
            if (_currentLine?.PreviousLine == null)
            {
                return;
            }

            SelectWord(_currentLine?.PreviousLine?.FirstWord);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void MoveWordsToNewLine()
    {
        try
        {
            if (_currentWord == _currentLine?.FirstWord || _currentWord?.PreviousWord == null || _currentLine == null)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            LyricsLine newLine = new(this);
            if (_currentLine.NextLine != null)
            {
                LyricsLines.Insert(LyricsLines.IndexOf(_currentLine.NextLine), newLine);
            }
            else
            {
                LyricsLines.Add(newLine);
            }


            if (_currentLine.LastWord != null)
            {
                LyricsWord word = _currentLine.LastWord;
                do
                {
                    _currentLine.RemoveWord(word);
                    newLine.InsertWord(0, word);
                    word.Line = newLine;
                    if (word.PreviousWord == null)
                    {
                        break;
                    }

                    word = word.PreviousWord;
                } while (word != _currentWord.PreviousWord);
            }

            _currentLine = newLine;
            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void MoveLineToPrevious()
    {
        try
        {
            if (_currentWord != _currentLine?.FirstWord || _currentWord?.PreviousWord == null ||
                _currentLine?.PreviousLine == null)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            LyricsLine deletedLine = _currentLine;
            LyricsLine previousLine = _currentLine.PreviousLine;

            LyricsWord word = _currentWord;
            while (word.Line == _currentLine)
            {
                previousLine.AddWord(word);
                deletedLine.RemoveWord(word);
                word.Line = previousLine;
                if (word.NextWord == null)
                {
                    break;
                }

                word = word.NextWord;
            }

            LyricsLines.Remove(deletedLine);

            _currentLine = previousLine;

            RemoveEmptyWords();

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal void MoveNextLineToCurrent()
    {
        try
        {
            if (_currentWord != _currentLine?.LastWord || _currentWord?.NextWord == null ||
                _currentLine?.NextLine == null)
            {
                return;
            }

            Global.MainWindowViewModel.AddToUndoList();
            LyricsLine nextLine = _currentLine.NextLine;
            LyricsWord word = _currentWord.NextWord;
            while (word.Line == nextLine)
            {
                _currentLine.AddWord(word);
                nextLine.RemoveWord(word);
                word.Line = _currentLine;
                if (word.NextWord == null)
                {
                    break;
                }

                word = word.NextWord;
            }

            LyricsLines.Remove(nextLine);

            RemoveEmptyWords();

            Global.MainWindowViewModel.IsSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal string GetLyricsText(out int caretIndex)
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
            MessageBox.Show(ex.Message);
            return "";
        }

        return text;
    }

    internal void SelectFirstLine()
    {
        try
        {
            _currentLine = FirstLine;
            SelectWord(_currentLine?.FirstWord);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    internal TimeSpan GetCurrentWordStartTime()
    {
        if (_currentWord == null)
        {
            return TimeSpan.Zero;
        }

        LyricsWord word = _currentWord;

        while (word.StartTime == TimeSpan.Zero && word != FirstLine?.FirstWord)
        {
            if (word.PreviousWord != null)
            {
                word = word.PreviousWord;
            }
        }

        TimeSpan timeSpan = word.Line.StartTime;
        return timeSpan;
    }

    internal void ChangePlayingWord()
    {
        if (_currentPlayingWord != null && Global.Audio.CurrentTime >= _currentPlayingWord.StartTime &&
            Global.Audio.CurrentTime <= _currentPlayingWord.EndTime)
        {
            return;
        }

        if (_currentPlayingWord != null)
        {
            _currentPlayingWord.IsPlaying = false;
            _currentPlayingWord = null;
        }

        LyricsWord? word = FirstLine?.FirstWord;

        while (word != null)
        {
            if (Global.Audio.CurrentTime >= word.StartTime && Global.Audio.CurrentTime <= word.EndTime)
            {
                word.IsPlaying = true;
                _currentPlayingWord = word;
                break;
            }

            word = word.NextWord;
        }
    }

    internal void ChangeStartTimeOfCurrentWord(double seconds)
    {
        Global.MainWindowViewModel.AddToUndoList();
        ChangeStartTimeOfWord(_currentWord, seconds);
        Global.MainWindowViewModel.IsSaved = false;
    }

    internal void ChangeStartingTimeOfAllWordsFromCurrent(double seconds)
    {
        Global.MainWindowViewModel.AddToUndoList();
        LyricsWord? word = _currentWord;
        do
        {
            ChangeStartTimeOfWord(word, seconds);
            word = word?.NextWord;
        } while (word != null);

        Global.MainWindowViewModel.IsSaved = false;
    }

    private static void ChangeStartTimeOfWord(LyricsWord? word, double seconds)
    {
        if (word != null && word.StartTime < TimeSpan.FromSeconds(seconds))
        {
            seconds = word.StartTime.TotalSeconds;
        }

        word?.PreviousWord?.EndTime = word.PreviousWord.EndTime.Add(TimeSpan.FromSeconds(seconds));

        word?.StartTime = word.StartTime.Add(TimeSpan.FromSeconds(seconds));
    }

    [GeneratedRegex(@"(?<!\d)\.(?!\d)")]
    private static partial Regex DotNotBetweenNumbersRegex();

    [GeneratedRegex(@"\[([0-9]{2})\:([0-9]{2})\.([0-9]{2})\](.*)")]
    private static partial Regex PatternLineRegex();

    [GeneratedRegex(@"(.*?)\<([0-9]{2})\:([0-9]{2})\.([0-9]{2})\>")]
    private static partial Regex PatternWordEndRegex();

    [GeneratedRegex(@"^\<([0-9]{2})\:([0-9]{2})\.([0-9]{2})\>(.+)")]
    private static partial Regex PatternWordStartRegex();

    private void ParseWordTimes(string word, LyricsLine lyricsLine, TimeSpan lineStartTime, int selectedWordIndex)
    {
        TimeSpan startTime = lineStartTime;
        Regex regex = PatternWordStartRegex();
        Match match = regex.Match(word);
        string restOfWord = word;
        if (match.Success)
        {
            startTime = new TimeSpan(0, 0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value) * 10);
            restOfWord = match.Groups[4].Value;
        }
        else if (lyricsLine.LastWord != null)
        {
            startTime = lyricsLine.LastWord.EndTime;
        }

        regex = PatternWordEndRegex();
        MatchCollection matches = regex.Matches(restOfWord);
        if (matches.Count == 0)
        {
            LyricsWord lyricsWord = new(lyricsLine);
            lyricsLine.AddWord(lyricsWord);

            lyricsWord.StartTime = startTime;
            lyricsWord.EndTime = TimeSpan.Zero;
            lyricsWord.Word = restOfWord;

            lyricsWord.CheckProperTime();

            if (WordCount == selectedWordIndex)
            {
                SelectWord(lyricsWord);
            }
        }
        else
        {
            foreach (Match match1 in matches)
            {
                TimeSpan endTime = new(0, 0, int.Parse(match1.Groups[2].Value), int.Parse(match1.Groups[3].Value),
                    int.Parse(match1.Groups[4].Value) * 10);
                restOfWord = match1.Groups[1].Value;

                List<string> subWords = [.. restOfWord.Split('|')];

                for (int i = 0; i < subWords.Count; i++)
                {
                    string subWord = subWords[i];
                    LyricsWord lyricsWord = new(lyricsLine);
                    lyricsLine.AddWord(lyricsWord);

                    lyricsWord.EndTime = endTime;
                    lyricsWord.StartTime = startTime;
                    startTime = lyricsWord.EndTime;

                    if (string.IsNullOrWhiteSpace(restOfWord))
                    {
                        subWord = "";
                    }

                    lyricsWord.Word = subWord;
                    if (match1 != matches[^1] || i < subWords.Count - 1)
                    {
                        lyricsWord.IsPartOfWord = true;
                    }

                    lyricsWord.CheckProperTime();

                    if (WordCount == selectedWordIndex)
                    {
                        SelectWord(lyricsWord);
                    }
                }
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RemoveEmptyWords()
    {
        LyricsWord? word = FirstLine?.FirstWord;

        if (word == null)
        {
            return;
        }

        do
        {
            if (string.IsNullOrWhiteSpace(word.Word) && word.Line.FirstWord != word.Line.LastWord)
            {
                RemoveWord(word);
            }

            word = word.NextWord;
        } while (word != null);
    }

    private void InsertEmptyLineBeforeCurrent(TimeSpan timeSpan)
    {
        if (_currentWord?.PreviousWord == null || _currentLine?.PreviousLine == null)
        {
            return;
        }

        LyricsLine newLine = new(this);
        LyricsLines.Insert(LyricsLines.IndexOf(_currentLine), newLine);
        LyricsWord newWord = new(newLine) {Word = ""};
        newLine.AddWord(newWord);
        if (newWord.PreviousWord != null)
        {
            newWord.StartTime = newWord.PreviousWord.EndTime;
        }

        newWord.EndTime = timeSpan;
    }

    private void InsertEmptyLineAtBeginning(TimeSpan timeSpan)
    {
        LyricsLine newLine = new(this);
        LyricsLines.Insert(0, newLine);
        LyricsWord newWord = new(newLine) {Word = ""};

        newLine.AddWord(newWord);
        newWord.EndTime = timeSpan;
    }

    private void RemoveWord(LyricsWord word)
    {
        word.Line.RemoveWord(word);
        if (word.Line.Words.Count == 0)
        {
            LyricsLines.Remove(word.Line);
        }
    }
}