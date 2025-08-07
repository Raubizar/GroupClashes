using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GroupClashes
{
    /// <summary>
    /// Comprehensive logging system for GroupClashes plugin
    /// Provides automatic user action tracking and issue identification
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GroupClashes", "Logs");
        
        private static readonly string LogFileName = $"GroupClashes_{DateTime.Now:yyyyMMdd}.log";
        private static readonly string LogFilePath = Path.Combine(LogDirectory, LogFileName);
        
        private static readonly object LogLock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the logging system
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Create log directory if it doesn't exist
                Directory.CreateDirectory(LogDirectory);
                
                // Write session start header
                WriteToFile($"=== GroupClashes Plugin Session Started ===");
                WriteToFile($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                WriteToFile($"Navisworks Version: {GetNavisworksVersion()}");
                WriteToFile($"Plugin Version: 1.1.4");
                WriteToFile($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                WriteToFile($"Process ID: {Process.GetCurrentProcess().Id}");
                WriteToFile("================================================");
                
                _isInitialized = true;
                LogInfo("Logger initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize logger: {ex.Message}");
            }
        }

        /// <summary>
        /// Log informational messages
        /// </summary>
        public static void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            LogMessage("INFO", message, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Log warning messages
        /// </summary>
        public static void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            LogMessage("WARN", message, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Log error messages
        /// </summary>
        public static void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            LogMessage("ERROR", message, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Log error with exception details
        /// </summary>
        public static void LogError(string message, Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var errorMessage = $"{message} | Exception: {ex.GetType().Name} - {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}";
            }
            errorMessage += $" | Stack: {ex.StackTrace}";
            LogMessage("ERROR", errorMessage, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Log user actions for tracking workflow patterns
        /// </summary>
        public static void LogUserAction(string action, string details = "", [CallerMemberName] string memberName = "")
        {
            var message = $"USER_ACTION: {action}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | Details: {details}";
            }
            LogMessage("ACTION", message, memberName);
        }

        /// <summary>
        /// Log performance metrics
        /// </summary>
        public static void LogPerformance(string operation, TimeSpan duration, string details = "")
        {
            var message = $"PERFORMANCE: {operation} completed in {duration.TotalMilliseconds:F2}ms";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            LogMessage("PERF", message);
        }

        /// <summary>
        /// Log transaction operations for debugging transaction conflicts
        /// </summary>
        public static void LogTransaction(string operation, string transactionName, string details = "")
        {
            var message = $"TRANSACTION: {operation} - '{transactionName}'";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            LogMessage("TXN", message);
        }

        /// <summary>
        /// Log UI thread operations to identify threading issues
        /// </summary>
        public static void LogUIThread(string operation, bool isUIThread, string details = "")
        {
            var message = $"UI_THREAD: {operation} | IsUIThread: {isUIThread} | ThreadID: {Thread.CurrentThread.ManagedThreadId}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" | {details}";
            }
            LogMessage("UI", message);
        }

        /// <summary>
        /// Create a performance measurement scope
        /// </summary>
        public static IDisposable MeasurePerformance(string operation, string details = "")
        {
            return new PerformanceScope(operation, details);
        }

        /// <summary>
        /// Log session end
        /// </summary>
        public static void LogSessionEnd()
        {
            WriteToFile("=== GroupClashes Plugin Session Ended ===");
            WriteToFile($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteToFile("===============================================");
        }

        private static void LogMessage(string level, string message, string memberName = "", string filePath = "", int lineNumber = 0)
        {
            if (!_isInitialized) Initialize();

            var fileName = string.IsNullOrEmpty(filePath) ? "" : Path.GetFileNameWithoutExtension(filePath);
            var locationInfo = string.IsNullOrEmpty(memberName) ? "" : $" | {fileName}.{memberName}({lineNumber})";
            
            var logEntry = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{locationInfo}";
            
            WriteToFile(logEntry);
            
            // Also write to debug output for development
            Debug.WriteLine($"[GroupClashes] {logEntry}");
        }

        private static void WriteToFile(string message)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private static string GetNavisworksVersion()
        {
            try
            {
                return Autodesk.Navisworks.Api.Application.Version.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Performance measurement scope for automatic timing
        /// </summary>
        private class PerformanceScope : IDisposable
        {
            private readonly string _operation;
            private readonly string _details;
            private readonly Stopwatch _stopwatch;

            public PerformanceScope(string operation, string details)
            {
                _operation = operation;
                _details = details;
                _stopwatch = Stopwatch.StartNew();
                LogInfo($"Started performance measurement: {_operation}");
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                LogPerformance(_operation, _stopwatch.Elapsed, _details);
            }
        }
    }
}
