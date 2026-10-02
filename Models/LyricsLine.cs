using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Do_Re_Mi_Lyrics.Models;

public class LyricsLine(Lyrics lyrics) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string LineText => Words.Aggregate("", (current, word) => $"{current}{word.Word} ");

    public string StartTimeText => StartTime.ToString(@"\[mm\:ss\.ff\]");

    public bool IsNotProperTime
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan StartTime
    {
        get;
        set
        {
            field = LyricsWord.RoundTime(value) ?? TimeSpan.Zero;
            RefreshTimesAround();
        }
    }

    public ObservableCollection<LyricsWord> Words
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    internal LyricsWord? FirstWord => Words.Count > 0 ? Words[0] : null;
    internal bool IsEmpty => Words.Count == 0;
    internal LyricsWord? LastWord => Words.Count > 0 ? Words[^1] : null;

    internal LyricsLine? NextLine =>
        lyrics.LastLine == this || lyrics.LastLine == null || lyrics.LyricsLines.IndexOf(this) == -1
            ? null
            : lyrics.LyricsLines[lyrics.LyricsLines.IndexOf(this) + 1];

    internal LyricsLine? PreviousLine =>
        lyrics.FirstLine == this || lyrics.FirstLine == null || lyrics.LyricsLines.IndexOf(this) == -1
            ? null
            : lyrics.LyricsLines[lyrics.LyricsLines.IndexOf(this) - 1];

    public string ToString(out int caretIndex)
    {
        caretIndex = 0;
        string result = StartTimeText;
        foreach (LyricsWord word in Words)
        {
            if (word.IsSelected)
            {
                caretIndex = result.Length;
            }

            result += word;
        }

        return result;
    }

    internal void CheckProperTime()
    {
        IsNotProperTime = StartTime < PreviousLine?.StartTime || StartTime < PreviousLine?.LastWord?.EndTime;
    }

    internal void AddWord(LyricsWord word)
    {
        Words.Add(word);
        RefreshTimesAround();
    }

    internal void InsertWord(int index, LyricsWord word)
    {
        Words.Insert(index, word);
        RefreshTimesAround();
    }

    internal void RemoveWord(LyricsWord word)
    {
        Words.Remove(word);
        RefreshTimesAround();
    }

    internal void RefreshTimes()
    {
        OnPropertyChanged(nameof(StartTimeText));
        CheckProperTime();
        foreach (LyricsWord word in Words)
        {
            word.RefreshTimes();
        }
    }

    internal void RefreshTimesAround()
    {
        PreviousLine?.RefreshTimes();
        RefreshTimes();
        NextLine?.RefreshTimes();
    }

    internal LyricsLine Clone(Lyrics lyricsToClone)
    {
        LyricsLine lyricsLine = new(lyricsToClone) {StartTime = StartTime, IsNotProperTime = IsNotProperTime};
        foreach (LyricsWord lyricsWord in Words)
        {
            lyricsLine.Words.Add(lyricsWord.Clone(lyricsLine));
        }

        return lyricsLine;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}