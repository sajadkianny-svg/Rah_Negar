using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Utils;

/// <summary>
/// ثبت خطاها در فایل متنی
/// </summary>
public static class ErrorLogger
{
    public static void Log(Exception ex, string source)
    {
        try
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string filePath = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");

            string message =
                "==============================" + Environment.NewLine +
                $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
                $"Source: {source}" + Environment.NewLine +
                $"Message: {ex.Message}" + Environment.NewLine +
                $"Details: {ex}" + Environment.NewLine;

            File.AppendAllText(filePath, message + Environment.NewLine);
        }
        catch
        {
            // اگر ثبت لاگ هم خطا داد، برنامه نباید کرش کند
        }
    }
}