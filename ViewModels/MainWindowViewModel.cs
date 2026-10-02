using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Do_Re_Mi_Lyrics.Helper;
using Do_Re_Mi_Lyrics.Models;
using Do_Re_Mi_Lyrics.Properties;
using Do_Re_Mi_Lyrics.Views;
using Microsoft.Win32;

namespace Do_Re_Mi_Lyrics.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private const double LineRealignPadding = 0.25;
    private const double PlayLeadBeforeWordSeconds = 1.5;
    private const double RealignSeparationContext = 2.0;

    internal double LineHeight;
    internal double ScrollViewerHeight;

    private readonly DispatcherTimer
        _playTimer = new(DispatcherPriority.Send) {Interval = new TimeSpan(0, 0, 0, 0, 10)};

    private readonly List<Lyrics> _redoList = [];
    private readonly Stopwatch _synchroStopwatch = new();
    private readonly DispatcherTimer _synchroTimer = new() {Interval = TimeSpan.FromSeconds(1)};
    private readonly List<Lyrics> _undoList = [];
    private readonly Window _window;
    private CancellationTokenSource? _synchroCancellation;

    public MainWindowViewModel(Window window)
    {
        _window = window;
        _playTimer.Tick += PlayTimerTick;
        _synchroTimer.Tick += (_, _) => OnPropertyChanged(nameof(SynchroElapsedText));
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
            IsLyricsInAudioFile = Settings.Default.IsLyricsInAudioFile;
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

    public string SynchroElapsedText => _synchroStopwatch.Elapsed.ToString(@"m\:ss");

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

    public bool IsLyricsInAudioFile { get; private set; }

    public bool IsSaved
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool IsSynchronizing
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

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

    public string SynchroStatusText
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    private bool IsLyricsFileLoaded => LyricsFilePath != "Open or paste lyrics";

    public void OpenAudioFile()
    {
        try
        {
            ExitEditMode();
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
            OpenLyricsFromAudioFile();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    public void OpenLyricsFile()
    {
        try
        {
            ExitEditMode();
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

            SetLyricsSource(ofd.FileName, false);
            _undoList.Clear();
            ParseLyricsFromFile();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    public bool SaveLyrics()
    {
        try
        {
            ExitEditMode();
            if (!IsLyricsFileLoaded)
            {
                return SaveLyricsToNewFile();
            }

            string text = Lyrics.GetLyricsTextWithTimestamps(out _);
            if (IsLyricsInAudioFile)
            {
                Audio.ReleaseFileWhile(LyricsFilePath, () => AudioFileLyrics.Write(LyricsFilePath, text));
            }
            else
            {
                File.WriteAllText(LyricsFilePath, text, Encoding.UTF8);
            }

            IsSaved = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
            return false;
        }

        return true;
    }

    public bool SaveLyricsToNewFile()
    {
        try
        {
            ExitEditMode();
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

            string text = Lyrics.GetLyricsTextWithTimestamps(out _);
            File.WriteAllText(sfd.FileName, text, Encoding.UTF8);
            SetLyricsSource(sfd.FileName, false);
            IsSaved = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
            return false;
        }

        return true;
    }

    public bool SaveLyricsToAudioFile()
    {
        try
        {
            ExitEditMode();
            if (!IsAudioFileLoaded)
            {
                MessageBox.Show(_window, "Open an audio file first.", "Save lyrics to audio file");
                return false;
            }

            string audioFilePath = AudioFilePath;
            string text = Lyrics.GetLyricsTextWithTimestamps(out _);
            Audio.ReleaseFileWhile(audioFilePath, () => AudioFileLyrics.Write(audioFilePath, text));
            SetLyricsSource(audioFilePath, true);
            IsSaved = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
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
            ExitEditMode();
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
            ErrorLog.Show(ex);
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

        PlaySliderPosition =
            (long) (timeSpan - TimeSpan.FromSeconds(PlayLeadBeforeWordSeconds * PlayTempo)).TotalMilliseconds;
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
        IsLyricsInAudioFile = false;
        Settings.Default.LyricsFilePath = "";
        Settings.Default.IsLyricsInAudioFile = false;
        Settings.Default.Save();
        IsSaved = true;
    }

    public void ParseLyricsFromClipboard()
    {
        if (!CheckIfSaved())
        {
            return;
        }

        string text = Lyrics.CleanPastedText(Clipboard.GetText());
        Lyrics.ParseLyrics(text);
    }

    public void ShowAboutWindow()
    {
        AboutWindow aboutWindow = new() {Owner = _window};
        aboutWindow.ShowDialog();
    }

    public async void AutomaticSynchro()
    {
        try
        {
            await RunPythonTaskAsync(SynchronizeAllLinesAsync);
        }
        catch (Exception e)
        {
            ErrorLog.Show(e);
        }
    }

    public async void RealignCurrentLine()
    {
        try
        {
            if (IsSynchronizing || !IsAudioFileLoaded)
            {
                return;
            }

            if (Lyrics.CurrentLine is not {IsEmpty: false, LastWord: { } lastWord} line)
            {
                return;
            }

            double totalSeconds = Audio.TotalTime.TotalSeconds;
            double lineEnd = (lastWord.EndTime ?? line.NextLine?.StartTime)?.TotalSeconds ?? totalSeconds;
            if (lineEnd <= line.StartTime.TotalSeconds)
            {
                MessageBox.Show(_window, "Set the start of this line and of the next line first.",
                    "Synchronization of the current line");
                return;
            }

            WhisperLine block = WordsMatching.CreateLineBlock(line,
                Math.Max(0, line.StartTime.TotalSeconds - LineRealignPadding),
                Math.Min(totalSeconds, lineEnd + LineRealignPadding));
            if (string.IsNullOrWhiteSpace(block.Text))
            {
                return;
            }

            await RunPythonTaskAsync((vocalsPath, cancellationToken) =>
                RealignLineAsync(line, block, vocalsPath, cancellationToken));
        }
        catch (Exception e)
        {
            ErrorLog.Show(e);
        }
    }

    public void CancelSynchro()
    {
        _synchroCancellation?.Cancel();
    }

    public void ChangeButtonToPlay()
    {
        PlayPauseText = "Play (Space)";
        PlayPauseIconPath = @"..\Images\play.png";
    }

    public void ChangeButtonToPause()
    {
        PlayPauseText = "Pause (Space)";
        PlayPauseIconPath = @"..\Images\pause.png";
    }

    public void StartTimer()
    {
        _playTimer.Start();
    }

    public void StopTimer()
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(PlaySliderPosition));
        _playTimer.Stop();
    }

    public void RefreshPlayPosition()
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(PlaySliderPosition));
        Lyrics.ChangePlayingWord();
    }

    public void ExitEditMode()
    {
        if (IsEditMode)
        {
            ChangeEditMode();
        }
    }

    public void ChangeEditMode()
    {
        IsEditMode = !IsEditMode;
        if (IsEditMode)
        {
            LyricsText = Lyrics.GetLyricsTextWithTimestamps(out int caretIndex);
            CaretIndex = caretIndex;
        }
        else
        {
            string cutLyricsText = LyricsText.Remove(CaretIndex);
            Lyrics tempLyrics = new();
            tempLyrics.ParseLyrics(cutLyricsText, 0, true);
            int wordIndex = tempLyrics.WordCount;
            if (!cutLyricsText.EndsWith(' '))
            {
                wordIndex--;
            }

            Lyrics.ParseLyrics(LyricsText, wordIndex);
            if (Lyrics.ApplyTimingCorrectionsWithUndo())
            {
                IsSaved = false;
            }
        }
    }

    public void AddToUndoList()
    {
        if (_undoList.Count > 0 && Lyrics.GetLyricsTextWithTimestamps(out int _) ==
            _undoList[0].GetLyricsTextWithTimestamps(out int _))
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

    public void Undo()
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

    public void Redo()
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

    public void CheckScrollView()
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
            ErrorLog.Show(ex);
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task RunPythonTaskAsync(Func<string, CancellationToken, Task> runAsync)
    {
        if (IsSynchronizing || !IsAudioFileLoaded)
        {
            return;
        }

        ExitEditMode();

        bool isPythonEnvironmentReady = Whisper.IsPythonEnvironmentReady;

        if (!isPythonEnvironmentReady && MessageBox.Show(_window,
                "Automatic synchronization needs to download and install its components once " +
                "(about 3 GB, it can take several minutes). Continue?", "Automatic synchronization",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Audio.Pause();

        using CancellationTokenSource cancellation = new();
        _synchroCancellation = cancellation;
        SynchroStatusText = "Starting...";
        IsSynchronizing = true;
        _synchroStopwatch.Restart();
        _synchroTimer.Start();
        OnPropertyChanged(nameof(SynchroElapsedText));
        string vocalsPath = Path.Combine(Path.GetTempPath(), $"do-re-mi-vocals-{Guid.NewGuid():N}.wav");

        try
        {
            if (!isPythonEnvironmentReady)
            {
                SynchroStatusText = "First run setup: Starting...";
                await Whisper.PreparePythonEnvironmentAsync(
                    new Progress<string>(x => SynchroStatusText = $"First run setup: {x}"), cancellation.Token);
            }

            await runAsync(vocalsPath, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            ErrorLog.Show(e);
        }
        finally
        {
            Lyrics.CheckProperTimes();
            DeleteTemporaryFile(vocalsPath);
            _synchroTimer.Stop();
            _synchroStopwatch.Stop();
            _synchroCancellation = null;
            IsSynchronizing = false;
        }
    }

    private async Task SynchronizeAllLinesAsync(string vocalsPath, CancellationToken cancellationToken)
    {
        bool isMatched = false;

        try
        {
            SynchroStatusText = "Stage 1/3: Starting...";
            await Whisper.SeparateVocalsAsync(AudioFilePath, vocalsPath,
                new Progress<string>(x => SynchroStatusText = $"Stage 1/3: {x}"), cancellationToken);

            SynchroStatusText = "Stage 2/3: Starting...";
            WhisperRoughResult roughResult = await Whisper.RunWhisperRoughAsync(vocalsPath,
                new Progress<string>(x => SynchroStatusText = $"Stage 2/3: {x}"), cancellationToken);
            AddToUndoList();
            List<WhisperLine> alignBlocks = WordsMatching.MatchLyrics(roughResult.Words, Lyrics, roughResult.Duration);
            isMatched = true;
            IsSaved = false;

            if (alignBlocks.Count > 0)
            {
                SynchroStatusText = "Stage 3/3: Starting...";
                WhisperAlignResult alignResult = await Whisper.RunWhisperAlignAsync(vocalsPath, alignBlocks,
                    new Progress<string>(x => SynchroStatusText = $"Stage 3/3: {x}"), cancellationToken);
                WordsMatching.ApplyAlignResult(alignBlocks, alignResult);
            }
        }
        finally
        {
            if (isMatched)
            {
                Lyrics.CloseShortGapsAndInsertEmptyLines();
            }
        }
    }

    private async Task RealignLineAsync(LyricsLine line, WhisperLine block, string vocalsPath,
        CancellationToken cancellationToken)
    {
        double separationStart = Math.Max(0, block.Start - RealignSeparationContext);
        double separationEnd = Math.Min(Audio.TotalTime.TotalSeconds, block.End + RealignSeparationContext);

        SynchroStatusText = "Stage 1/2: Starting...";
        await Whisper.SeparateVocalsAsync(AudioFilePath, vocalsPath, separationStart, separationEnd,
            new Progress<string>(x => SynchroStatusText = $"Stage 1/2: {x}"), cancellationToken);

        SynchroStatusText = "Stage 2/2: Starting...";
        WhisperLine separatedBlock = new()
        {
            Text = block.Text, Start = block.Start - separationStart, End = block.End - separationStart
        };
        WhisperAlignResult alignResult = await Whisper.RunWhisperAlignAsync(vocalsPath, [separatedBlock],
            new Progress<string>(x => SynchroStatusText = $"Stage 2/2: {x}"), cancellationToken);

        foreach (WhisperWordDto word in alignResult.Words)
        {
            word.Start += separationStart;
            word.End += separationStart;
        }

        AddToUndoList();
        WordsMatching.ApplyAlignResult([block], alignResult);
        Lyrics.ApplyRealignedLine(line);
        IsSaved = false;
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
        string text = IsLyricsInAudioFile ? AudioFileLyrics.Read(LyricsFilePath) : File.ReadAllText(LyricsFilePath);
        Lyrics.ParseLyrics(text);
        IsSaved = !Lyrics.ApplyTimingCorrectionsWithUndo();
    }

    private void OpenLyricsFromAudioFile()
    {
        string audioFilePath = AudioFilePath;
        if (string.IsNullOrWhiteSpace(AudioFileLyrics.Read(audioFilePath)))
        {
            if (IsLyricsInAudioFile)
            {
                NewLyrics();
                return;
            }

            if (Lyrics.WordCount == 0)
            {
                return;
            }

            NoLyricsInAudioWindowViewModel noLyricsInAudioViewModel = new();
            NoLyricsInAudioWindow noLyricsInAudioWindow = new(noLyricsInAudioViewModel) {Owner = _window};
            noLyricsInAudioWindow.ShowDialog();
            switch (noLyricsInAudioViewModel.Choice)
            {
                case NoLyricsInAudioChoice.Clear:
                    NewLyrics();
                    break;
                case NoLyricsInAudioChoice.Open:
                    OpenLyricsFile();
                    break;
                case NoLyricsInAudioChoice.Keep:
                    break;
            }

            return;
        }

        if (!CheckIfSaved())
        {
            return;
        }

        SetLyricsSource(audioFilePath, true);
        _undoList.Clear();
        ParseLyricsFromFile();
    }

    private void SetLyricsSource(string filePath, bool isLyricsInAudioFile)
    {
        LyricsFilePath = filePath;
        IsLyricsInAudioFile = isLyricsInAudioFile;
        Settings.Default.LyricsFilePath = filePath;
        Settings.Default.IsLyricsInAudioFile = isLyricsInAudioFile;
        if (!isLyricsInAudioFile)
        {
            Settings.Default.LyricsFilesPath = Path.GetDirectoryName(filePath);
        }

        Settings.Default.Save();
    }
}
