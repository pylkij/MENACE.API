using System;
using System.Collections.Generic;

namespace Jiangyu.API;

/// <summary>
/// Provides frame-delayed and condition-based callback scheduling for mod code.
/// </summary>
public static class Defer
{
    private static readonly List<DelayedAction> _delayedActions = [];
    private static readonly List<ConditionalAction> _conditionalActions = [];

    /// <summary>
    /// Fires 30 frames after the Tactical scene loads. Subscribe to run code once the tactical session is ready.
    /// </summary>
    public static event Action TacticalReady;
    static Defer()
    {
        // Registers TacticalReady to fire 30 frames after the Tactical scene loads.
        GameState.SceneLoaded += scene =>
        {
            if (string.Equals(scene, "Tactical", StringComparison.OrdinalIgnoreCase))
                RunDelayed(30, () =>
                {
                    try { TacticalReady?.Invoke(); }
                    catch (Exception ex)
                    {
                        APILogger.ReportInternal("Defer.TacticalReady", "Event handler failed", ex);
                    }
                });
        };
    }

    /// <summary>
    /// Run a callback after a specified number of frames.
    /// </summary>
    public static void RunDelayed(int frames, Action callback)
    {
        if (callback == null) return;
        lock (_delayedActions)
        {
            _delayedActions.Add(new DelayedAction { FramesRemaining = frames, Callback = callback });
        }
    }

    /// <summary>
    /// Run a callback when a condition becomes true, polling once per frame.
    /// Gives up after maxAttempts frames.
    /// </summary>
    public static void RunWhen(string modId, Func<bool> condition, Action callback, int maxAttempts = 30)
    {
        if (condition == null || callback == null) return;
        lock (_conditionalActions)
        {
            _conditionalActions.Add(new ConditionalAction
            {
                ModId = modId,
                Condition = condition,
                Callback = callback,
                AttemptsRemaining = maxAttempts
            });
        }
    }

    internal static void ProcessUpdate()
    {
        // If no delayed actions specified, skip.
        if (_delayedActions.Count == 0 && _conditionalActions.Count == 0) return;

        // Process delayed actions
        lock (_delayedActions)
        {
            for (int i = _delayedActions.Count - 1; i >= 0; i--)
            {
                var action = _delayedActions[i];
                action.FramesRemaining--;
                if (action.FramesRemaining <= 0)
                {
                    _delayedActions.RemoveAt(i);
                    try { action.Callback(); }
                    catch (Exception ex)
                    {
                        APILogger.ReportInternal("Defer.RunDelayed", "Callback failed", ex);
                    }
                }
            }
        }

        // Process conditional actions
        lock (_conditionalActions)
        {
            for (int i = _conditionalActions.Count - 1; i >= 0; i--)
            {
                var action = _conditionalActions[i];
                action.AttemptsRemaining--;

                try
                {
                    if (action.Condition())
                    {
                        _conditionalActions.RemoveAt(i);
                        try { action.Callback(); }
                        catch (Exception ex)
                        {
                            APILogger.ReportInternal("Defer.RunWhen", "Callback failed", ex);
                        }
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    APILogger.WarnInternal("Defer.RunWhen", $"Condition threw (attempt {action.AttemptsRemaining} remaining): {ex.Message}");
                }

                if (action.AttemptsRemaining <= 0)
                {
                    APILogger.Info(action.ModId, "RunWhen condition expired — callback will not run");
                    _conditionalActions.RemoveAt(i);
                }
            }
        }
    }

    private class DelayedAction
    {
        public int FramesRemaining;
        public Action Callback;
    }

    private class ConditionalAction
    {
        public string ModId;
        public Func<bool> Condition;
        public Action Callback;
        public int AttemptsRemaining;
    }
}