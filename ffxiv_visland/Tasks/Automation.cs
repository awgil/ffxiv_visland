using ECommons.DalamudServices;
using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace visland.Tasks;

// base class for automation tasks
// all tasks are cancellable, and all continuations are executed on the main thread (in framework update)
// tasks also support progress reporting
// note: it's assumed that any created task will be executed (either by calling Run directly or by passing to Automation.Start)
public abstract class AutoTask
{
    // debug context scope
    protected readonly struct DebugContext : IDisposable
    {
        private readonly AutoTask _ctx;
        private readonly int _depth;

        public DebugContext(AutoTask ctx, string name)
        {
            _ctx = ctx;
            _depth = _ctx._debugContext.Count;
            _ctx._debugContext.Add(name);
            _ctx.Log("Scope enter");
        }

        public void Dispose()
        {
            _ctx.Log($"Scope exit (depth={_depth}, cur={_ctx._debugContext.Count - 1})");
            if (_depth < _ctx._debugContext.Count)
                _ctx._debugContext.RemoveRange(_depth, _ctx._debugContext.Count - _depth);
        }

        public void Rename(string newName)
        {
            _ctx.Log($"Transition to {newName} @ {_depth}");
            if (_depth < _ctx._debugContext.Count)
                _ctx._debugContext[_depth] = newName;
        }
    }

    public string Status { get; protected set; } = ""; // user-facing status string
    private readonly CancellationTokenSource _cts = new();
    private readonly List<string> _debugContext = [];
    //internal bool Paused
    //{
    //    get => _paused;
    //    set
    //    {
    //        if (_paused == value)
    //            return;

    //        Log($"Paused set to {value}");
    //        _paused = value;
    //        if (_paused)
    //        {
    //            // Cancel existing pause token
    //            Log("Cancelling pause token");
    //            _pauseCts?.Cancel();
    //            _pauseCts?.Dispose();
    //            _pauseCts = null;
    //        }
    //        else
    //        {
    //            // Create new token for resuming
    //            Log("Creating new pause token");
    //            _pauseCts = new();
    //        }
    //    }
    //}
    internal bool Paused { get; set; } = false;
    //private bool _paused;
    private CancellationTokenSource? _pauseCts = new();

    // Helper method to get a linked token that cancels on either pause or task cancellation
    protected CancellationToken GetLinkedToken()
    {
        _pauseCts ??= new();
        var token = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _pauseCts.Token).Token;
        Log($"Creating linked token (Cancelled: {token.IsCancellationRequested})");
        return token;
    }

    public void Cancel()
    {
        _cts.Cancel();
        _pauseCts?.Cancel();
    }

    public void Run(Action completed, Action? OnCompleted = null)
    {
        Svc.Framework.Run(async () =>
        {
            var task = Execute(_cts.Token);
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); // we don't really care about cancelation...
            if (task.IsFaulted)
                PluginLog.Warning($"Task ended with error: {task.Exception}");
            completed();
            OnCompleted?.Invoke();
            _cts.Dispose();
        }, _cts.Token);
    }

    // implementations are typically expected to be async (coroutines)
    public abstract Task Execute(CancellationToken token);

    protected CancellationToken CancelToken => _cts.Token;

    // wait for a few frames
    protected Task NextFrame(int numFramesToWait = 1)
    {
        //var token = GetLinkedToken();
        //Log($"NextFrame with token (Cancelled: {token.IsCancellationRequested})"); // Debug log
        return Svc.Framework.DelayTicks(numFramesToWait, _cts.Token);
    }

    /// <summary>
    /// Wait until condition function returns false, checking once every N frames
    /// </summary>
    protected async Task WaitWhile(Func<bool> condition, string scopeName, int checkFrequency = 1)
    {
        using var scope = BeginScope(scopeName);
        while (condition())
        {
            Log("waiting...");
            await NextFrame(checkFrequency);
        }
    }

    /// <summary>
    /// Wait until condition function returns true, checking once every N frames
    /// </summary>
    protected async Task WaitUntil(Func<bool> condition, string scopeName, int checkFrequency = 1) => await WaitWhile(() => !condition(), scopeName, checkFrequency);

    /// <summary>
    /// Wait until a condition function returns true, then wait until it returns false.
    /// </summary>
    /// <remarks> Meant for functions like checking if an ipc is busy then checking til it's not. </remarks>
    protected async Task WaitUntilThenFalse(Func<bool> condition, string scopeName, int checkFrequency = 1)
    {
        using var scope = BeginScope(scopeName);
        await WaitUntil(() => condition(), scopeName, checkFrequency);
        await WaitWhile(() => condition(), scopeName, checkFrequency);
    }

    protected void Log(string message) => PluginLog.Debug($"[{GetType().Name}] [{string.Join(" > ", _debugContext)}] {message}");

    // start a new debug context; should be disposed, so usually should be assigned to RAII variable
    protected DebugContext BeginScope(string name) => new(this, name);

    // abort a task unconditionally
    protected void Error(string message)
    {
        Log($"Error: {message}");
        throw new Exception($"[{GetType().Name}] [{string.Join(" > ", _debugContext)}] {message}");
    }

    // abort a task if condition is true
    protected void ErrorIf(bool condition, string message)
    {
        if (condition)
            Error(message);
    }
}

// utility that allows concurrently executing only one task; starting a new task if one is already in progress automatically cancels olds one
public sealed class Automation : IDisposable
{
    public AutoTask? CurrentTask { get; private set; }

    public bool Running => CurrentTask != null;
    public bool Paused => CurrentTask?.Paused ?? false;
    public string StatusString => Paused ? "Paused" : CurrentTask?.Status ?? "Idle";
    private CancellationTokenSource? _cts;
    private bool _paused;

    public void Dispose() => Stop();

    public async Task ExecuteTask(AutoTask t)
    {
        // deal with any pre-existing task(s)
        CurrentTask = t;
        if (!_paused)
        {
            using var cts = _cts = new();
            await t.Execute(cts.Token);
        }
    }

    public void Pause()
    {
        if (_paused) return;
        _paused = true;
        _cts?.Cancel();
        _cts = null;
    }

    public async Task Resume()
    {
        if (!_paused) return;
        _paused = false;
        if (CurrentTask != null)
        {
            using var cts = _cts = new();
            await CurrentTask.Execute(cts.Token);
        }
    }

    // stop executing any running task
    // this requires tasks to cooperate by checking the token
    public void Stop()
    {
        CurrentTask?.Cancel();
        CurrentTask = null;
    }

    // if any other task is running, it's cancelled
    public void Start(AutoTask task, Action? OnCompleted = null)
    {
        Stop();
        CurrentTask = task;
        task.Run(() =>
        {
            if (CurrentTask == task)
                CurrentTask = null;
            // else: some other task is now executing
        }, OnCompleted);
    }
}

public readonly record struct OnDispose(Action A) : IDisposable
{
    public void Dispose() => A();
}
