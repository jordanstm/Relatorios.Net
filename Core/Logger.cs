using System;
using System.IO;

namespace Relatorio.Core
{
    public static class Logger
    {
        private const string LogFile = "logs.txt";

        public static void Log(string message, Exception? ex = null)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (ex != null)
                {
                    logEntry += Environment.NewLine + $"Exception: {ex.Message}";
                    logEntry += Environment.NewLine + $"Stack Trace: {ex.StackTrace}";
                }
                logEntry += Environment.NewLine + new string('-', 50) + Environment.NewLine;

                File.AppendAllText(LogFile, logEntry);
            }
            catch
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to log to file: {message}");
            }
        }

        public static void LogError(string context, Exception ex)
        {
            Log($"ERROR in {context}", ex);
        }
    }
}
