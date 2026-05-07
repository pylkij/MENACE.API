using System;
using MelonLoader;

namespace Jiangyu.API;

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Central error reporting for Jiangyu API. Writes all output to MelonLoader's
/// Latest.log. Never throws. Tracks a running error count for in-game notification.
/// </summary>
public static class APILogger
{
    private static int _errorCount = 0;

    /// <summary>
    /// Total number of errors and fatals reported since startup.
    /// Suitable for driving a lightweight in-game notification.
    /// </summary>
    public static int ErrorCount => _errorCount;

    /// <summary>
    /// Reset the error count. Call when entering a new scene
    /// if you want per-scene error tracking.
    /// </summary>
    public static void ResetCount() => _errorCount = 0;

    // --- Public API for modders ---

    public static void Error(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Error, ex);

    public static void Warn(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Warning, ex);

    public static void Info(string modId, string message)
        => Write(modId, null, message, ErrorSeverity.Info, null);

    public static void Fatal(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Fatal, ex);

    // --- Internal API for Jiangyu.API itself ---

    internal static void ReportInternal(string context, string message, Exception ex = null)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Error, ex);

    internal static void WarnInternal(string context, string message)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Warning, null);

    internal static void InfoInternal(string context, string message)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Info, null);

    // --- Core writer ---

    private static void Write(string modId, string context, string message,
        ErrorSeverity severity, Exception ex)
    {
        try
        {
            var prefix = string.IsNullOrEmpty(context)
                ? $"[{modId}]"
                : $"[{modId}:{context}]";

            switch (severity)
            {
                case ErrorSeverity.Info:
                    MelonLogger.Msg($"{prefix} {message}");
                    break;
                case ErrorSeverity.Warning:
                    MelonLogger.Warning($"{prefix} {message}");
                    break;
                case ErrorSeverity.Error:
                case ErrorSeverity.Fatal:
                    MelonLogger.Error($"{prefix} {message}");
                    if (ex != null)
                        MelonLogger.Error($"{prefix} {ex}");
                    _errorCount++;
                    break;
            }
        }
        catch
        {
            // Never crash from error reporting
        }
    }
}