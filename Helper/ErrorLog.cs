using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Do_Re_Mi_Lyrics.Helper;

internal static class ErrorLog
{
    private const long MaxLogFileBytes = 1024 * 1024;

    internal static readonly string LogDirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Do-Re-Mi Lyrics",
            "Logs");

    internal static readonly string LogFilePath = Path.Combine(LogDirectoryPath, "errors.log");

    internal static void Show(Exception exception)
    {
        Write(exception);
        MessageBox.Show($"{exception.Message}\n\nDetails were saved to:\n{LogFilePath}", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    internal static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MaxLogFileBytes)
            {
                File.Move(LogFilePath, Path.ChangeExtension(LogFilePath, ".old.log"), true);
            }

            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
            File.AppendAllText(LogFilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{version}{Environment.NewLine}{exception}{Environment.NewLine}" +
                Environment.NewLine, Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
