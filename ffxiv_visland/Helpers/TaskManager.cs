using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace visland.Helpers;

public sealed class TaskManager : IDisposable {
    private readonly Queue<TaskStep> _queue = new();
    private TaskStep? _current;
    private long _stepStartMs;
    private bool _disposed;

    public bool AbortOnTimeout { get; set; } = true;
    public int TimeLimitMS { get; set; } = 20000;

    public bool IsBusy => _current != null || _queue.Count > 0;
    public string? CurrentStepName => _current?.Name;

    public TaskManager() {
        Service.Framework.Update += OnFrameworkUpdate;
    }

    public void Enqueue(Func<bool> action, string? name = null) => _queue.Enqueue(new TaskStep(action, name));

    public void Clear() {
        _queue.Clear();
        _current = null;
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        Service.Framework.Update -= OnFrameworkUpdate;
        Clear();
    }

    private void OnFrameworkUpdate(IFramework framework) => Tick();

    private void Tick() {
        if (_current == null && _queue.TryDequeue(out var next)) {
            _current = next;
            _stepStartMs = Environment.TickCount64;
        }

        if (_current == null)
            return;

        try {
            if (_current.Action()) {
                _current = null;
                return;
            }

            if (AbortOnTimeout && Environment.TickCount64 - _stepStartMs > TimeLimitMS) {
                Service.Log.Warning($"TaskManager step '{_current.Name ?? "unnamed"}' timed out after {TimeLimitMS}ms");
                Clear();
            }
        }
        catch (Exception ex) {
            Service.Log.Error(ex, $"TaskManager step '{_current?.Name ?? "unnamed"}' failed");
            Clear();
        }
    }

    private sealed record TaskStep(Func<bool> Action, string? Name);
}
