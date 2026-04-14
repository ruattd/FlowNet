using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
    private static readonly Dictionary<string, IFlowTask> _FlowTasks = [];

    public static Task InvokeTask(string globalIdentifier)
        => InvokeTask<None, None>(globalIdentifier, default);

    public static Task<TReturn> InvokeTask<TReturn>(string globalIdentifier)
        => InvokeTask<TReturn, None>(globalIdentifier, default);

    public static Task InvokeTask<TArgument>(string globalIdentifier, TArgument argument)
        => InvokeTask<None, TArgument>(globalIdentifier, argument);

    public static Task<TReturn> InvokeTask<TReturn, TArgument>(string globalIdentifier, TArgument argument)
    {
        return _FlowTasks.TryGetValue(globalIdentifier, out var task)
            ? task.Invoke<TReturn, TArgument>(argument)
            : throw new KeyNotFoundException($"There is no task with identifier '{globalIdentifier}'.");
    }

    partial class Internal
    {
        public static void RegisterTask(string globalIdentifier, IFlowTask task)
        {
            if (_FlowTasks.ContainsKey(globalIdentifier))
                throw new InvalidOperationException($"Task with identifier '{globalIdentifier}' has already existed.");
            _FlowTasks[globalIdentifier] = task;
        }
    }
}

public interface IFlowTask
{
    public Task<TReturn> Invoke<TReturn, TArgument>(TArgument argument);
}
