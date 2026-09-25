using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Do_Re_Mi_Lyrics.ViewModels;

namespace Do_Re_Mi_Lyrics.Views;

public partial class AboutWindow : Window
{
    private readonly AboutWindowViewModel _viewModel;

    public AboutWindow()
    {
        InitializeComponent();
        _viewModel = new AboutWindowViewModel(WebBrowser);
        DataContext = _viewModel;
    }

    private void LicenseClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowLicense();
    }

    private void ChangelogClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowChangelog();
    }

    private void HelpClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowHelp();
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Url_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenUrl("https://github.com/blueberry-bytes/do-re-mi-lyrics");
    }

    private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenUrl("mailto:lukasz.przestrzelski@gmail.com");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});
            }
            else
            {
                throw;
            }
        }
    }
}