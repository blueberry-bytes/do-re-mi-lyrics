using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;

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

    public string NameVersion =>
        $"{Assembly.GetEntryAssembly()?.GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

    public void ShowLicense()
    {
        _webBrowser.NavigateToString(GetEmbeddedResource("Do_Re_Mi_Lyrics.TextResources", "license.txt"));
    }

    public void ShowChangelog()
    {
        _webBrowser.NavigateToString(GetEmbeddedResource("Do_Re_Mi_Lyrics.TextResources", "changelog.txt"));
    }

    public void ShowHelp()
    {
        _webBrowser.NavigateToString(GetEmbeddedResource("Do_Re_Mi_Lyrics.TextResources", "help.txt"));
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}