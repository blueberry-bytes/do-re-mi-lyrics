using System;
using System.IO;
using System.Windows;
using NAudio.Wave;
using Do_Re_Mi_Lyrics.Helper;
using SoundTouch.Net.NAudioSupport;

namespace Do_Re_Mi_Lyrics.Models;

public class Audio : IDisposable
{
    private const double SkipIntervalSeconds = 1;
    private SoundTouchWaveStream? _processorStream;
    private WaveStream? _reader;
    private double _tempo;
    private float _volume;
    private WaveChannel32? _waveChannel;

    private WaveOutEvent _waveOut = new() {DesiredLatency = 100};

    public string CurrentTimeText => (_waveChannel?.CurrentTime ?? TimeSpan.Zero).ToString(@"mm\:ss\.ff");

    internal TimeSpan TotalTime => _waveChannel?.TotalTime ?? TimeSpan.Zero;

    internal TimeSpan CurrentTime
    {
        get =>
            TimeSpan.FromMilliseconds((_waveChannel?.CurrentTime ?? TimeSpan.Zero).TotalMilliseconds -
                                      (_waveChannel?.CurrentTime ?? TimeSpan.Zero).TotalMilliseconds % 10);
        set => _waveChannel?.CurrentTime = value;
    }

    internal double Tempo
    {
        get => _processorStream?.Tempo ?? 1;
        set
        {
            _tempo = value;
            _processorStream?.Tempo = value;
        }
    }

    internal float Volume
    {
        get => _waveChannel?.Volume ?? 1;
        set
        {
            _volume = value;
            _waveChannel?.Volume = value;
        }
    }

    public void Dispose()
    {
        _processorStream?.Dispose();
        _waveChannel?.Dispose();
        _waveOut.Dispose();
    }

    internal void Rewind()
    {
        try
        {
            if (_waveChannel == null)
            {
                return;
            }

            TimeSpan skip = GetSkipTime();
            if (_waveChannel.CurrentTime < skip)
            {
                _waveChannel.Position = 0;
            }
            else
            {
                _waveChannel.CurrentTime -= skip;
            }

            Global.MainWindowViewModel.RefreshPlayPosition();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void FastForward()
    {
        try
        {
            if (_waveChannel == null)
            {
                return;
            }

            TimeSpan skip = GetSkipTime();
            if (_waveChannel.CurrentTime > _waveChannel.TotalTime - skip)
            {
                _waveChannel.CurrentTime = _waveChannel.TotalTime;
                Pause();
            }
            else
            {
                _waveChannel.CurrentTime += skip;
            }

            Global.MainWindowViewModel.RefreshPlayPosition();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void Stop()
    {
        try
        {
            if (_processorStream == null)
            {
                return;
            }

            _waveOut.Stop();
            Global.MainWindowViewModel.StopTimer();
            if (_processorStream.CanSeek)
            {
                _processorStream.Position = 0;
                Global.MainWindowViewModel.PlaySliderPosition = 0;
            }

            _processorStream.Flush();
            Global.MainWindowViewModel.ChangeButtonToPlay();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }


    internal void SetTempoUp()
    {
        try
        {
            if (!Global.MainWindowViewModel.IsAudioFileLoaded || _processorStream == null)
            {
                return;
            }

            if (Global.MainWindowViewModel.PlayTempo < 2)
            {
                Global.MainWindowViewModel.PlayTempo += 0.1;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SetTempoDown()
    {
        try
        {
            if (!Global.MainWindowViewModel.IsAudioFileLoaded || _processorStream == null)
            {
                return;
            }

            if (Global.MainWindowViewModel.PlayTempo > 0.1)
            {
                Global.MainWindowViewModel.PlayTempo -= 0.1;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SetVolumeUp()
    {
        try
        {
            if (!Global.MainWindowViewModel.IsAudioFileLoaded || _waveChannel == null)
            {
                return;
            }

            if (Global.MainWindowViewModel.PlayVolume < 1)
            {
                Global.MainWindowViewModel.PlayVolume += 0.1f;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void SetVolumeDown()
    {
        try
        {
            if (!Global.MainWindowViewModel.IsAudioFileLoaded || _waveChannel == null)
            {
                return;
            }

            if (Global.MainWindowViewModel.PlayVolume > 0.1)
            {
                Global.MainWindowViewModel.PlayVolume -= 0.1f;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void Play()
    {
        try
        {
            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                return;
            }

            _waveOut.Play();
            Global.MainWindowViewModel.StartTimer();
            Global.MainWindowViewModel.ChangeButtonToPause();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void Pause()
    {
        try
        {
            if (_waveOut.PlaybackState != PlaybackState.Playing)
            {
                return;
            }

            _waveOut.Pause();
            Global.MainWindowViewModel.StopTimer();
            Global.MainWindowViewModel.ChangeButtonToPlay();
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    internal void ReleaseFileWhile(string filePath, Action action)
    {
        bool isFileOpened = _reader is AudioFileReader && string.Equals(Path.GetFullPath(filePath),
            Path.GetFullPath(Global.MainWindowViewModel.AudioFilePath), StringComparison.OrdinalIgnoreCase);
        if (!isFileOpened)
        {
            action();
            return;
        }

        bool wasPlaying = _waveOut.PlaybackState == PlaybackState.Playing;
        Pause();
        TimeSpan currentTime = CurrentTime;
        CloseWaveOut();
        try
        {
            action();
        }
        finally
        {
            OpenAudio();
            CurrentTime = currentTime;
            Global.MainWindowViewModel.RefreshPlayPosition();
            if (wasPlaying)
            {
                Play();
            }
        }
    }

    internal void OpenAudio()
    {
        CloseWaveOut();

        try
        {
            if (Path.GetExtension(Global.MainWindowViewModel.AudioFilePath) == ".flac")
            {
                using MediaFoundationReader mediaFoundationReader = new(Global.MainWindowViewModel.AudioFilePath);
                WaveFormat outFormat = new(44100, mediaFoundationReader.WaveFormat.Channels);

                using MediaFoundationResampler resampler = new(mediaFoundationReader, outFormat);
                WaveFileWriter.CreateWaveFile($"{Path.GetTempPath()}temp.wav", resampler);
                _reader = new AudioFileReader($"{Path.GetTempPath()}temp.wav");
            }
            else
            {
                _reader = new AudioFileReader(Global.MainWindowViewModel.AudioFilePath);
            }

            _waveChannel = new WaveChannel32(_reader) {PadWithZeroes = false, Volume = _volume};


            _processorStream = new SoundTouchWaveStream(_waveChannel) {Tempo = _tempo};
            _waveOut = new WaveOutEvent {DesiredLatency = 100};

            _waveOut.Init(_processorStream);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    private TimeSpan GetSkipTime()
    {
        return TimeSpan.FromSeconds(SkipIntervalSeconds * Tempo);
    }

    private void CloseWaveOut()
    {
        _waveOut.Stop();
        _waveOut.Dispose();
        Global.MainWindowViewModel.StopTimer();
        Global.MainWindowViewModel.ChangeButtonToPlay();

        _processorStream?.Dispose();
        _waveChannel?.Dispose();
        _processorStream = null;
        _waveChannel = null;
        _reader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        if (sender == _waveOut && _waveOut.PlaybackState == PlaybackState.Stopped)
        {
            Stop();
        }
    }
}