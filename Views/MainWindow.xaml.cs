using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Do_Re_Mi_Lyrics.Helper;
using Do_Re_Mi_Lyrics.Models;
using Do_Re_Mi_Lyrics.ViewModels;

namespace Do_Re_Mi_Lyrics.Views;

public partial class MainWindow

{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(this);
        DataContext = _viewModel;
    }

    private void OpenAudioClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenAudioFile();
    }

    private void OpenLyricsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenLyricsFile();
    }

    private void SaveLyricsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveLyrics();
    }

    private void PlayPauseClick(object sender, RoutedEventArgs e)
    {
        _viewModel.PlayOrPause();
    }

    private void RewindClick(object sender, RoutedEventArgs e)
    {
        Global.Audio.Rewind();
    }

    private void StopClick(object sender, RoutedEventArgs e)
    {
        Global.Audio.Stop();
    }

    private void ForwardClick(object sender, RoutedEventArgs e)
    {
        Global.Audio.FastForward();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.IsSynchronizing)
        {
            if (e.Key == Key.Escape)
            {
                _viewModel.CancelSynchro();
            }

            e.Handled = true;
            return;
        }

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (e.Key)
        {
            case Key.F1 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.ShowAboutWindow();
                break;
            case Key.F2 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.NewLyrics();
                break;
            case Key.F3 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.OpenAudioFile();
                break;
            case Key.F4 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.OpenLyricsFile();
                break;
            case Key.F5 when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SetEndTimeOfPreviousWord();
                break;
            case Key.F6 when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SetTimeToCurrentWord();
                break;
            case Key.F8 when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.RemoveStartTime();
                break;
            case Key.F11 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.AutomaticSynchro();
                break;
            case Key.F11 when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.RealignCurrentLine();
                break;
            case Key.F12 when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.ChangeEditMode();
                break;
            case Key.S when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                _viewModel.SaveLyricsToNewFile();
                break;
            case Key.A when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                _viewModel.SaveLyricsToAudioFile();
                break;
            case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.SaveLyrics();
                break;
            case Key.V when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.ParseLyricsFromClipboard();
                break;
            case Key.Z when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.Redo();
                break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.Undo();
                break;
            case Key.Left when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Global.Audio.Rewind();
                break;
            case Key.Right when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Global.Audio.FastForward();
                break;
            case Key.Left when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SelectPreviousWord();
                break;
            case Key.Right when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SelectNextWord();
                break;
            case Key.Up when Keyboard.Modifiers == ModifierKeys.Control:
                Global.Audio.SetTempoUp();
                break;
            case Key.Down when Keyboard.Modifiers == ModifierKeys.Control:
                Global.Audio.SetTempoDown();
                break;
            case Key.Up when Keyboard.Modifiers == ModifierKeys.Shift:
                Global.Audio.SetVolumeUp();
                break;
            case Key.Down when Keyboard.Modifiers == ModifierKeys.Shift:
                Global.Audio.SetVolumeDown();
                break;
            case Key.Up when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SelectPreviousLine();
                break;
            case Key.Down when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.SelectNextLine();
                break;
            case Key.OemMinus when Keyboard.Modifiers == ModifierKeys.Control:
            case Key.Subtract when Keyboard.Modifiers == ModifierKeys.Control:
                Global.Lyrics.ChangeStartingTimeOfAllWordsFromCurrent(-0.2);
                break;
            case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.Control:
            case Key.Add when Keyboard.Modifiers == ModifierKeys.Control:
                Global.Lyrics.ChangeStartingTimeOfAllWordsFromCurrent(0.2);
                break;
            case Key.Subtract when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.ChangeStartTimeOfCurrentWord(-0.2);
                break;
            case Key.Add when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.ChangeStartTimeOfCurrentWord(0.2);
                break;
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.Control:
                _viewModel.MovePlaySliderToWord();
                break;
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.MoveWordsToNewLine();
                break;
            case Key.Back when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.MoveLineToPrevious();
                break;
            case Key.Delete when Keyboard.Modifiers == ModifierKeys.None:
                Global.Lyrics.MoveNextLineToCurrent();
                break;
            case Key.Space when Keyboard.Modifiers == ModifierKeys.None:
                _viewModel.PlayOrPause();
                break;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_viewModel.CheckIfSaved())
        {
            e.Cancel = true;
        }
        else
        {
            _viewModel.CancelSynchro();
        }

        Global.Audio.Dispose();

        if (!File.Exists($"{Path.GetTempPath()}temp.wav"))
        {
            return;
        }

        try
        {
            File.Delete($"{Path.GetTempPath()}temp.wav");
        }
        catch
        {
        }
    }

    private void Word_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            LyricsWord word;
            switch (sender)
            {
                case Border wordBorder:
                    word = (LyricsWord) wordBorder.DataContext;
                    break;
                case TextBlock wordTextBlock:
                    word = (LyricsWord) wordTextBlock.DataContext;
                    break;
                default:
                    return;
            }

            Global.Lyrics.SelectWord(word);
            if (e.ClickCount == 2)
            {
                _viewModel.MovePlaySliderToWord();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Show(ex);
        }
    }

    private void SaveLyricsAsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveLyricsToNewFile();
    }

    private void SaveLyricsToAudioFileClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveLyricsToAudioFile();
    }

    private void NewLyricsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.NewLyrics();
    }

    private void HelpClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowAboutWindow();
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ChangeEditMode();
    }

    private void AutomaticSynchroClick(object sender, RoutedEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            _viewModel.RealignCurrentLine();
        }
        else
        {
            _viewModel.AutomaticSynchro();
        }
    }

    private void CancelSynchroClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelSynchro();
    }

    private void UndoClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Undo();
    }

    private void RedoClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Redo();
    }

    private void ScrollViewer_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.ScrollViewerHeight = e.NewSize.Height;
    }

    private void StackPanel_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.LineHeight = e.NewSize.Height;
    }
}