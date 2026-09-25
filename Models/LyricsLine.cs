using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Do_Re_Mi_Lyrics.Models;

public class LyricsLine(Lyrics lyrics) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public TimeSpan StartTime => FirstWord?.StartTime ?? PreviousLine?.LastWord?.EndTime ?? TimeSpan.Zero;

    public string StartTimeText => StartTime.ToString(@"\[mm\:ss\.ff\]");

    public string Text => Words.Aggregate("", (current, word) => $"{current}{word.Word} ");

    public bool IsNotProperTime
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsTooShortTime
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
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
        IsNotProperTime = StartTime - PreviousLine?.LastWord?.EndTime < TimeSpan.Zero;
        IsTooShortTime = !string.IsNullOrWhiteSpace(FirstWord?.Word) &&
                         StartTime - PreviousLine?.StartTime < TimeSpan.FromSeconds(1.5) &&
                         lyrics.FirstLine?.NextLine != this;
    }

    internal void AddWord(LyricsWord word)
    {
        Words.Add(word);
        UpdateStartTimeText();
    }

    internal void InsertWord(int index, LyricsWord word)
    {
        Words.Insert(index, word);
        UpdateStartTimeText();
    }

    internal void RemoveWord(LyricsWord word)
    {
        Words.Remove(word);
        UpdateStartTimeText();
    }

    internal void UpdateStartTimeText()
    {
        OnPropertyChanged(nameof(StartTimeText));
    }

    internal LyricsLine Clone(Lyrics lyricsToClone)
    {
        LyricsLine lyricsLine = new(lyricsToClone) {IsNotProperTime = IsNotProperTime, IsTooShortTime = IsTooShortTime};
        foreach (LyricsWord lyricsWord in Words)
        {
            LyricsWord newLyricsWord = lyricsWord.Clone(lyricsLine);
            lyricsLine.Words.Add(newLyricsWord);
            newLyricsWord.CheckProperTime();
        }

        return lyricsLine;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}