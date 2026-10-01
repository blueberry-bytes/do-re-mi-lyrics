using System.Windows;
using Do_Re_Mi_Lyrics.ViewModels;

namespace Do_Re_Mi_Lyrics.Views;

public partial class NoLyricsInAudioWindow : Window
{
    private readonly NoLyricsInAudioWindowViewModel _viewModel;

    public NoLyricsInAudioWindow(NoLyricsInAudioWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
        Close();
    }

    private void OpenClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Open();
        Close();
    }

    private void KeepClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Keep();
        Close();
    }
}