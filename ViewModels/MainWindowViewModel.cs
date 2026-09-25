using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Do_Re_Mi_Lyrics.Models;
using Do_Re_Mi_Lyrics.Properties;
using Do_Re_Mi_Lyrics.Views;
using Microsoft.Win32;

namespace Do_Re_Mi_Lyrics.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    internal double LineHeight;
    internal double ScrollViewerHeight;

    private readonly DispatcherTimer
        _playTimer = new(DispatcherPriority.Send) {Interval = new TimeSpan(0, 0, 0, 0, 10)};

    private readonly List<Lyrics> _redoList = [];
    private readonly List<Lyrics> _undoList = [];
    private readonly Window _window;

    public MainWindowViewModel(Window window)
    {
        _window = window;
        _playTimer.Tick += PlayTimerTick;
        Global.Lyrics = new Lyrics();
        Global.Audio = new Audio();
        Global.MainWindowViewModel = this;
        PlayTempo = Settings.Default.Tempo;
        PlayVolume = Settings.Default.Volume;
        if (Settings.Default.AudioFilePath != "")
        {
            AudioFilePath = Settings.Default.AudioFilePath;
            Audio.OpenAudio();
            OnPropertyChanged(nameof(TotalTimeText));
            OnPropertyChanged(nameof(PlaySliderMaximum));
        }

        if (Settings.Default.LyricsFilePath != "" && File.Exists(Settings.Default.LyricsFilePath))
        {
            LyricsFilePath = Settings.Default.LyricsFilePath;
            ParseLyricsFromFile();
        }
        else
        {
            NewLyrics();
        }

        Lyrics.SelectFirstLine();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentTimeText => Global.Audio.CurrentTimeText;

    public bool IsAudioFileLoaded => AudioFilePath != "Open audio file";
    public bool IsRedoEnabled => _redoList.Count > 0;
    public bool IsUndoEnabled => _undoList.Count > 1;

    public int PlaySliderMaximum => (int) Audio.TotalTime.TotalMilliseconds;

    public string PlayTempoText => $"Tempo: {PlayTempo:0.0}x";
    public string PlayVolumeText => $"Volume: {PlayVolume * 100:0}%";

    public string TotalTimeText => Audio.TotalTime.ToString(@"mm\:ss");

    public Audio Audio
    {
        get => Global.Audio;
        set
        {
            Global.Audio = value;
            OnPropertyChanged();
        }
    }

    public string AudioFilePath
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAudioFileLoaded));
        }
    } = "Open audio file";

    public int CaretIndex
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsSaved
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public Lyrics Lyrics
    {
        get => Global.Lyrics;
        private set
        {
            Global.Lyrics = value;
            OnPropertyChanged();
        }
    }

    public string LyricsFilePath
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Open or paste lyrics";

    public string LyricsText
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    public string PlayPauseIconPath
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = @"..\Images\play.png";

    public string PlayPauseText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "Play (Space)";

    public long PlaySliderPosition
    {
        get => (int) Global.Audio.CurrentTime.TotalMilliseconds;
        set
        {
            Global.Audio.CurrentTime = TimeSpan.FromMilliseconds(value);

            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentTimeText));

            Lyrics.ChangePlayingWord();
        }
    }

    public double PlayTempo
    {
        get => Global.Audio.Tempo;
        set
        {
            Global.Audio.Tempo = value;
            Settings.Default.Tempo = value;
            Settings.Default.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayTempoText));
        }
    }

    public float PlayVolume
    {
        get => Global.Audio.Volume;
        set
        {
            Global.Audio.Volume = value;
            Settings.Default.Volume = value;
            Settings.Default.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayVolumeText));
        }
    }

    public double ScrollViewerPosition
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    private bool IsLyricsFileLoaded => LyricsFilePath != "Open or paste lyrics";

    public void OpenAudioFile()
    {
        try
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Audio (*.flac,*.mp3,*.wav)|*.flac;*.mp3;*.wav",
                Multiselect = false,
                InitialDirectory = Settings.Default.AudioFilesPath
            };
            if (!ofd.ShowDialog(_window)!.Value)
            {
                return;
            }

            AudioFilePath = ofd.FileName;
            Settings.Default.AudioFilePath = AudioFilePath;
            Settings.Default.AudioFilesPath = Path.GetDirectoryName(AudioFilePath);
            Settings.Default.Save();
            Audio.OpenAudio();
            OnPropertyChanged(nameof(TotalTimeText));
            OnPropertyChanged(nameof(PlaySliderMaximum));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    public void OpenLyricsFile()
    {
        try
        {
            if (!CheckIfSaved())
            {
                return;
            }

            OpenFileDialog ofd = new()
            {
                Filter = "Lyrics (*.lrc,*.txt)|*.lrc;*.txt",
                Multiselect = false,
                InitialDirectory = Settings.Default.LyricsFilesPath,
                FileName = IsAudioFileLoaded ? Path.GetFileNameWithoutExtension(AudioFilePath) : ""
            };
            if (!ofd.ShowDialog(_window)!.Value)
            {
                return;
            }

            LyricsFilePath = ofd.FileName;
            Settings.Default.LyricsFilePath = LyricsFilePath;
            Settings.Default.LyricsFilesPath = Path.GetDirectoryName(LyricsFilePath);
            Settings.Default.Save();
            _undoList.Clear();
            ParseLyricsFromFile();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    public bool SaveLyrics()
    {
        try
        {
            if (!IsLyricsFileLoaded)
            {
                return SaveLyricsToNewFile();
            }

            string text = Lyrics.GetLyricsText(out _);
            File.WriteAllText(LyricsFilePath, text, Encoding.UTF8);
            IsSaved = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
            return false;
        }

        return true;
    }

    public bool SaveLyricsToNewFile()
    {
        try
        {
            SaveFileDialog sfd = new()
            {
                Filter = "Lyrics (*.lrc)|*.lrc",
                InitialDirectory = Settings.Default.LyricsFilesPath,
                FileName = IsLyricsFileLoaded ? Path.GetFileNameWithoutExtension(LyricsFilePath) :
                    IsAudioFileLoaded ? Path.GetFileNameWithoutExtension(AudioFilePath) : ""
            };
            if (!sfd.ShowDialog(_window)!.Value)
            {
                return false;
            }

            string text = Lyrics.GetLyricsText(out _);
            File.WriteAllText(sfd.FileName, text, Encoding.UTF8);
            LyricsFilePath = sfd.FileName;
            Settings.Default.LyricsFilePath = LyricsFilePath;
            Settings.Default.LyricsFilesPath = Path.GetDirectoryName(LyricsFilePath);
            Settings.Default.Save();
            IsSaved = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
            return false;
        }

        return true;
    }


    public void PlayOrPause()
    {
        if (!IsAudioFileLoaded)
        {
            return;
        }

        if (PlayPauseText == "Play (Space)")
        {
            Audio.Play();
        }
        else
        {
            Audio.Pause();
        }
    }


    public bool CheckIfSaved()
    {
        try
        {
            if (IsSaved)
            {
                return true;
            }

            MessageBoxResult result =
                MessageBox.Show(_window, "Save changes?", "Changes", MessageBoxButton.YesNoCancel);
            return result switch
            {
                MessageBoxResult.Yes => IsLyricsFileLoaded ? SaveLyrics() : SaveLyricsToNewFile(),
                MessageBoxResult.Cancel => false,
                _ => true
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
            return false;
        }
    }


    public void MovePlaySliderToWord()
    {
        if (!IsAudioFileLoaded)
        {
            return;
        }

        TimeSpan timeSpan = Lyrics.GetCurrentWordStartTime();

        PlaySliderPosition = (long) (timeSpan - TimeSpan.FromSeconds(3 * PlayTempo)).TotalMilliseconds;
        Audio.Play();
    }

    public void NewLyrics()
    {
        if (!CheckIfSaved())
        {
            return;
        }

        Lyrics.ParseLyrics("");
        LyricsFilePath = "Open or paste lyrics";
        IsSaved = true;
    }

    public void ParseLyricsFromClipboard()
    {
        if (!CheckIfSaved())
        {
            return;
        }

        string text = Clipboard.GetText();
        Lyrics.ParseLyrics(text);
    }

    public void ShowAboutWindow()
    {
        AboutWindow aboutWindow = new() {Owner = _window};
        aboutWindow.ShowDialog();
    }

    internal void ChangeButtonToPlay()
    {
        PlayPauseText = "Play (Space)";
        PlayPauseIconPath = @"..\Images\play.png";
    }

    internal void ChangeButtonToPause()
    {
        PlayPauseText = "Pause (Space)";
        PlayPauseIconPath = @"..\Images\pause.png";
    }

    internal void StartTimer()
    {
        _playTimer.Start();
    }

    internal void StopTimer()
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(PlaySliderPosition));
        _playTimer.Stop();
    }

    internal void ChangeEditMode()
    {
        IsEditMode = !IsEditMode;
        if (IsEditMode)
        {
            LyricsText = Lyrics.GetLyricsText(out int caretIndex);
            CaretIndex = caretIndex;
        }
        else
        {
            string cutLyricsText = LyricsText.Remove(CaretIndex);
            Lyrics tempLyrics = new();
            tempLyrics.ParseLyrics(cutLyricsText, 0, true);
            int wordIndex = tempLyrics.WordCount;
            if (!cutLyricsText.EndsWith(" "))
            {
                wordIndex--;
            }

            Lyrics.ParseLyrics(LyricsText, wordIndex);
        }
    }

    internal void AddToUndoList()
    {
        if (_undoList.Count > 0 && Lyrics.GetLyricsText(out int _) == _undoList[0].GetLyricsText(out int _))
        {
            return;
        }

        if (IsCalledByConstructor())
        {
            return;
        }

        _undoList.Insert(0, Lyrics.Clone());
        while (_undoList.Count > 50)
        {
            _undoList.RemoveAt(49);
        }

        _redoList.Clear();

        OnPropertyChanged(nameof(IsUndoEnabled));
        OnPropertyChanged(nameof(IsRedoEnabled));
    }

    internal void Undo()
    {
        if (_undoList.Count < 2)
        {
            return;
        }

        _redoList.Insert(0, Lyrics);
        Lyrics = _undoList[0];
        _undoList.RemoveAt(0);
        OnPropertyChanged(nameof(IsUndoEnabled));
        OnPropertyChanged(nameof(IsRedoEnabled));
    }

    internal void Redo()
    {
        if (_redoList.Count < 1)
        {
            return;
        }

        _undoList.Insert(0, Lyrics);
        Lyrics = _redoList[0];
        _redoList.RemoveAt(0);
        OnPropertyChanged(nameof(IsUndoEnabled));
        OnPropertyChanged(nameof(IsRedoEnabled));
    }

    internal void CheckScrollView()
    {
        try
        {
            int index = Lyrics.CurrentLineIndex;
            int lineStartPosition = (int) (index * LineHeight);
            int lineEndPosition = (int) ((index + 1) * LineHeight);
            if (lineStartPosition < ScrollViewerPosition)
            {
                ScrollViewerPosition = lineStartPosition;
            }
            else if (lineEndPosition > ScrollViewerPosition + ScrollViewerHeight)
            {
                ScrollViewerPosition = lineEndPosition - ScrollViewerHeight;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private static bool IsCalledByConstructor()
    {
        return new StackTrace().GetFrames().Select(stackFrame => stackFrame.GetMethod()).Any(method =>
            method != null && method is {IsConstructor: true, DeclaringType.Name: nameof(MainWindowViewModel)});
    }


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void PlayTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(PlaySliderPosition));
        Lyrics.ChangePlayingWord();
    }

    private void ParseLyricsFromFile()
    {
        string text = File.ReadAllText(LyricsFilePath);
        Lyrics.ParseLyrics(text);
        IsSaved = true;
    }
}