using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Rah_Negar.Infrastructure.Foundation.Logging;

namespace Rah_Negar.Utils;

/// <summary>
/// ثبت خطاها در فایل متنی
/// </summary>
public static class ErrorLogger
{
    private const long MaxLogFileBytes = 2 * 1024 * 1024;
    private static int _lastWriteSucceeded;

    public static bool LastWriteSucceeded => Volatile.Read(ref _lastWriteSucceeded) == 1;

    public static void Log(Exception ex, string source)
        => TryLog(ex, source, Rah_Negar.Infrastructure.ApplicationData.ApplicationDataPaths.Default.LogsDirectory);

    public static bool TryLog(Exception ex, string source, string logDir)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDir);
        Interlocked.Exchange(ref _lastWriteSucceeded, 0);

        try
        {
            Directory.CreateDirectory(logDir);

            string filePath = Path.Combine(logDir, $"log_{DateTime.UtcNow:yyyyMMdd}.txt");
            RotateIfNeeded(filePath);

            string message =
                "==============================" + Environment.NewLine +
                $"TimeUtc: {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}" + Environment.NewLine +
                $"Source: {source}" + Environment.NewLine +
                $"ExceptionType: {ex.GetType().Name}" + Environment.NewLine +
                $"Message: {RedactText(ex.Message)}" + Environment.NewLine;

            File.AppendAllText(filePath, message + Environment.NewLine);
            Interlocked.Exchange(ref _lastWriteSucceeded, 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaxLogFileBytes)
            return;

        string archive = filePath + ".1";
        if (File.Exists(archive))
            File.Delete(archive);
        File.Move(filePath, archive);
    }

    public static string RedactText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        const string sensitiveKeys = "password|pass|hash|salt|secret|token|credential|recovery|signature|private";
        return Regex.Replace(value,
            $"(?i)(?<key>{sensitiveKeys})\\s*[:=]\\s*[^;\\r\\n, ]+",
            "${key}=[REDACTED]")
            .Trim();
    }
}
