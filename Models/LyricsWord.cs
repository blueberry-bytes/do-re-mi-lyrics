using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Do_Re_Mi_Lyrics.Models;

public class LyricsWord(LyricsLine line) : INotifyPropertyChanged
{
    private const long TimePrecisionTicks = TimeSpan.TicksPerMillisecond * 10;

    internal LyricsLine Line = line;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EndTimeText =>
        EndTime is { } endTime && !(NextWordInLine == null && Line.NextLine?.StartTime == endTime)
            ? FormatTime(endTime)
            : "";

    public string StartTimeText =>
        StartTime is { } startTime && startTime != (PreviousWordInLine is { } previousWord
            ? previousWord.EndTime
            : Line.StartTime)
            ? FormatTime(startTime)
            : "";

    public bool IsNotProperTime
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsPartOfWord
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsPlaying
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string Word
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    internal TimeSpan? EffectiveEndTime =>
        EndTime ?? (NextWordInLine is { } nextWord ? nextWord.StartTime : Line.NextLine?.StartTime);

    internal TimeSpan? EffectiveStartTime =>
        StartTime ?? (PreviousWordInLine is { } previousWord ? previousWord.EndTime : Line.StartTime);

    internal bool IsFirstInLine => Line.FirstWord == this;

    internal LyricsWord? NextWord
    {
        get
        {
            if (NextWordInLine is { } nextWord)
            {
                return nextWord;
            }

            for (LyricsLine? nextLine = Line.NextLine; nextLine != null; nextLine = nextLine.NextLine)
            {
                if (nextLine.FirstWord is { } firstWord)
                {
                    return firstWord;
                }
            }

            return null;
        }
    }

    internal LyricsWord? NextWordInLine
    {
        get
        {
            int index = Line.Words.IndexOf(this);
            return index >= 0 && index < Line.Words.Count - 1 ? Line.Words[index + 1] : null;
        }
    }

    internal LyricsWord? PreviousWord
    {
        get
        {
            if (PreviousWordInLine is { } previousWord)
            {
                return previousWord;
            }

            for (LyricsLine? previousLine = Line.PreviousLine;
                 previousLine != null;
                 previousLine = previousLine.PreviousLine)
            {
                if (previousLine.LastWord is { } lastWord)
                {
                    return lastWord;
                }
            }

            return null;
        }
    }

    internal LyricsWord? PreviousWordInLine
    {
        get
        {
            int index = Line.Words.IndexOf(this);
            return index > 0 ? Line.Words[index - 1] : null;
        }
    }

    internal TimeSpan? EndTime
    {
        get;
        set
        {
            field = RoundTime(value);
            Line.RefreshTimesAround();
        }
    }

    internal TimeSpan? StartTime
    {
        get;
        set
        {
            field = RoundTime(value);
            Line.RefreshTimesAround();
        }
    }

    public override string ToString()
    {
        string separator = !IsPartOfWord ? " " :
            EndTimeText == "" && NextWordInLine?.Word.StartsWith('-') != true ? "|" : "";
        return $"{StartTimeText}{Word}{EndTimeText}{separator}";
    }

    public LyricsWord Clone(LyricsLine lyricsLine)
    {
        LyricsWord lyricsWord = new(lyricsLine)
        {
            IsNotProperTime = IsNotProperTime,
            IsPartOfWord = IsPartOfWord,
            StartTime = StartTime,
            EndTime = EndTime,
            IsPlaying = IsPlaying,
            IsSelected = IsSelected,
            Word = Word
        };
        return lyricsWord;
    }

    internal static TimeSpan? RoundTime(TimeSpan? time)
    {
        return time is { } value ? new TimeSpan(Math.Max(0, value.Ticks - value.Ticks % TimePrecisionTicks)) : null;
    }

    internal void CheckProperTime()
    {
        TimeSpan? startTime = EffectiveStartTime;
        IsNotProperTime = EffectiveEndTime < startTime || startTime < PreviousWord?.EffectiveEndTime;
    }

    internal void RefreshTimes()
    {
        OnPropertyChanged(nameof(StartTimeText));
        OnPropertyChanged(nameof(EndTimeText));
        CheckProperTime();
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.ToString(@"\<mm\:ss\.ff\>");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}