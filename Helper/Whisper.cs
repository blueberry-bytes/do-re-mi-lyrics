using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Do_Re_Mi_Lyrics.Models;

namespace Do_Re_Mi_Lyrics.Helper;

public static class Whisper
{
    private const string InstalledRequirementsFileName = "installed-requirements.txt";
    private const string RequirementsFileName = "python-requirements.txt";
    private const string ResultPrefix = "RESULT_JSON:";
    private const string StatusPrefix = "STATUS:";

    private static readonly string BundledPythonDirectory = Path.Combine(AppContext.BaseDirectory, "Python");

    private static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Do-Re-Mi Lyrics");

    private static readonly string ModelsDirectory = Path.Combine(DataDirectory, "Models");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string PythonDirectory = Path.Combine(DataDirectory, "Python");

    private static readonly UTF8Encoding Utf8 = new(false);

    public static bool IsPythonEnvironmentReady
    {
        get
        {
            string installedRequirementsPath = Path.Combine(PythonDirectory, InstalledRequirementsFileName);
            string requirementsPath = Path.Combine(AppContext.BaseDirectory, RequirementsFileName);

            return File.Exists(Path.Combine(PythonDirectory, "python.exe")) && File.Exists(installedRequirementsPath) &&
                   File.ReadAllText(installedRequirementsPath) == File.ReadAllText(requirementsPath);
        }
    }

    public static async Task PreparePythonEnvironmentAsync(IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Copying Python...");
        CopyBundledPython();

        await RunScriptAsync("setup-python.py", [], null, progress, cancellationToken);

        File.Copy(Path.Combine(AppContext.BaseDirectory, RequirementsFileName),
            Path.Combine(PythonDirectory, InstalledRequirementsFileName), true);
    }

    public static async Task SeparateVocalsAsync(string audioPath, string vocalsPath, IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await RunScriptAsync("separate-vocals.py", [audioPath, vocalsPath], null, progress, cancellationToken);
    }

    public static async Task SeparateVocalsAsync(string audioPath, string vocalsPath, double start, double end,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        await RunScriptAsync("separate-vocals.py", [
            audioPath, vocalsPath, start.ToString(CultureInfo.InvariantCulture),
            end.ToString(CultureInfo.InvariantCulture)
        ], null, progress, cancellationToken);
    }

    public static async Task<WhisperRoughResult> RunWhisperRoughAsync(string audioPath, IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        string json = await RunScriptAsync("whisper-rough.py", [audioPath], null, progress, cancellationToken);

        WhisperRoughResult? result = JsonSerializer.Deserialize<WhisperRoughResult>(json, Options);

        return result ?? throw new InvalidOperationException("Could not deserialize Whisper result.");
    }

    public static async Task<WhisperAlignResult> RunWhisperAlignAsync(string audioPath,
        IReadOnlyList<WhisperLine> lines, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        string jsonInput = JsonSerializer.Serialize(lines, Options);

        string json = await RunScriptAsync("whisper-align.py", [audioPath], jsonInput, progress, cancellationToken);

        WhisperAlignResult? result = JsonSerializer.Deserialize<WhisperAlignResult>(json, Options);

        return result ?? throw new InvalidOperationException("Could not deserialize Whisper alignment result.");
    }

    private static async Task<string> RunScriptAsync(string scriptName, IReadOnlyList<string> arguments, string? input,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ProcessStartInfo psi = new()
        {
            FileName = Path.Combine(PythonDirectory, "python.exe"),
            UseShellExecute = false,
            RedirectStandardInput = input != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = input != null ? Utf8 : null,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8
        };

        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["HF_HOME"] = Path.Combine(ModelsDirectory, "huggingface");
        psi.Environment["TORCH_HOME"] = Path.Combine(ModelsDirectory, "torch");
        psi.Environment["PIP_NO_CACHE_DIR"] = "1";

        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, scriptName));

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using Process process = new();
        process.StartInfo = psi;

        process.Start();

        await using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
        });

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderrTask = ReadStandardErrorAsync(process.StandardError, progress);

        if (input != null)
        {
            try
            {
                await process.StandardInput.WriteAsync(input);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        await process.WaitForExitAsync(CancellationToken.None);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{scriptName} failed with exit code {process.ExitCode}.\n{stderr}");
        }

        string? jsonLine = stdout.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith(ResultPrefix));

        if (jsonLine is null)
        {
            throw new InvalidOperationException(
                $"{scriptName} did not return {ResultPrefix}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        return jsonLine[ResultPrefix.Length..];
    }

    private static void CopyBundledPython()
    {
        Directory.CreateDirectory(PythonDirectory);

        foreach (string file in Directory.GetFiles(BundledPythonDirectory))
        {
            File.Copy(file, Path.Combine(PythonDirectory, Path.GetFileName(file)), true);
        }
    }

    private static async Task<string> ReadStandardErrorAsync(StreamReader reader, IProgress<string>? progress)
    {
        StringBuilder stderr = new();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith(StatusPrefix))
            {
                progress?.Report(line[StatusPrefix.Length..]);
            }
            else
            {
                stderr.AppendLine(line);
            }
        }

        return stderr.ToString();
    }
}