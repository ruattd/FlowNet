using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
    private static readonly Dictionary<string, IFlowTask> _FlowTasks = new()
    {
        ["flow:run"] = new FlowRunTask(),
    };

    partial class Internal
    {
        public static void RegisterTask(string globalIdentifier, IFlowTask task,
            params IEnumerable<(string? before, string? after, int priority)> runConfigs)
        {
            if (_FlowTasks.ContainsKey(globalIdentifier))
                throw new InvalidOperationException($"Task with identifier '{globalIdentifier}' has already existed.");
            foreach (var (before, after, priority) in runConfigs)
                FlowRunTask.AddConfig(globalIdentifier, before, after, priority);
            _FlowTasks[globalIdentifier] = task;
        }
    }
}

file sealed class FlowRunTask : IFlowTask
{
    private readonly record struct FlowTaskAutoRunConfig(
        string Identifier,
        string? Before,
        string? After,
        int Priority)
    {
    }

    private static readonly List<FlowTaskAutoRunConfig> _TaskAutoRunConfigs = [];

    public static void AddConfig(string identifier, string? before, string? after, int priority)
        => _TaskAutoRunConfigs.Add(new FlowTaskAutoRunConfig(identifier, before, after, priority));

    private static bool _isInvoked = false;
    private static readonly object _InvokeLock = new();

    private static async Task Run()
    {
        // draw map
    }

    public Task<TReturn> Invoke<TReturn, TArgument>(TArgument argument, FlowTaskInvokingInfo _)
    {
        if (argument is not None) throw new InvalidCastException("Argument is not supported by 'flow:run'");
        if (default(None) is not TReturn) throw new InvalidCastException("Return value is not supported by 'flow:run'");
        lock (_InvokeLock)
        {
            if (_isInvoked)
            {
                if (!Flow.AllowMultipleAutoRunInvoking)
                    throw new InvalidOperationException("Multiple invoking of 'flow:run' is prohibited");
            }
            else _isInvoked = true;
        }
        return Run().ContinueWith(_ => default(TReturn)!);
    }
}
