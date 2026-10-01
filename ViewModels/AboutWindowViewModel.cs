using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;
using Do_Re_Mi_Lyrics.Helper;
using Do_Re_Mi_Lyrics.Models;

namespace Do_Re_Mi_Lyrics.ViewModels;

public class AboutWindowViewModel : INotifyPropertyChanged
{
    private readonly WebBrowser _webBrowser;

    public AboutWindowViewModel(WebBrowser webBrowser)
    {
        _webBrowser = webBrowser;

        ShowHelp();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsChangelogShown => CurrentPage == AboutPage.Changelog;
    public bool IsHelpShown => CurrentPage == AboutPage.Help;
    public bool IsLicenseShown => CurrentPage == AboutPage.License;

    public string NameVersion =>
        $"{Assembly.GetEntryAssembly()?.GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

    private AboutPage? CurrentPage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(IsChangelogShown));
            OnPropertyChanged(nameof(IsHelpShown));
            OnPropertyChanged(nameof(IsLicenseShown));
        }
    }

    public void ShowLicense()
    {
        ShowPage(AboutPage.License, "license.txt");
    }

    public void ShowChangelog()
    {
        ShowPage(AboutPage.Changelog, "changelog.txt");
    }

    public void ShowHelp()
    {
        ShowPage(AboutPage.Help, "help.txt");
    }

    public static void OpenLogFolder()
    {
        Directory.CreateDirectory(ErrorLog.LogDirectoryPath);
        Process.Start(new ProcessStartInfo(ErrorLog.LogDirectoryPath) {UseShellExecute = true});
    }

    private static string GetEmbeddedResource(string namespaceName, string filename)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = namespaceName + "." + filename;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return "";
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        string result = reader.ReadToEnd();
        return result;
    }

    private void ShowPage(AboutPage page, string filename)
    {
        if (CurrentPage == page)
        {
            return;
        }

        CurrentPage = page;
        _webBrowser.NavigateToString(GetEmbeddedResource("Do_Re_Mi_Lyrics.TextResources", filename));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}