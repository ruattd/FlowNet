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

    /// <summary>
    /// 所有已注册的 Flow 任务的全局标识
    /// </summary>
    public static IReadOnlyCollection<string> TaskIdentifiers => _FlowTasks.Keys;

    /// <summary>
    /// 允许多次调用 <c>flow:run</c> 任务 (默认值: <see langword="false"/>)
    /// </summary>
    public static bool AllowMultipleAutoRunInvoking { get; set; } = false;

    /// <summary>
    /// 启用任务调用信息 (默认值: <see langword="false"/>)
    /// </summary>
    public static bool EnableTaskInvokingInfo { get; set; } = false;

    /// <summary>
    /// 调用既无参数也无返回值的 Flow 任务，详情参考 <see cref="InvokeTask{TReturn, TArgument}(string, TArgument)"/>。
    /// </summary>
    public static Task InvokeTask(string globalIdentifier)
        => InvokeTask<None, None>(globalIdentifier, default);

    /// <summary>
    /// 调用无参数的 Flow 任务，详情参考 <see cref="InvokeTask{TReturn, TArgument}(string, TArgument)"/>。
    /// </summary>
    public static Task<TReturn> InvokeTask<TReturn>(string globalIdentifier)
        => InvokeTask<TReturn, None>(globalIdentifier, default);

    /// <summary>
    /// 调用无返回值的 Flow 任务，详情参考 <see cref="InvokeTask{TReturn, TArgument}(string, TArgument)"/>。
    /// </summary>
    public static Task InvokeTask<TArgument>(string globalIdentifier, TArgument argument)
        => InvokeTask<None, TArgument>(globalIdentifier, argument);

    /// <summary>
    /// 调用 Flow 任务，传入参数并获取返回值。<br/>
    /// <b>NOTE</b>: 没有返回值或没有参数时用默认的 <see cref="None"/> 类型替代。
    /// </summary>
    /// <param name="globalIdentifier">全局标识，即包含作用域前缀的完整标识符</param>
    /// <param name="argument">任务参数，若大于一个，则该值应传入 <see cref="ValueTuple"/></param>
    /// <typeparam name="TReturn">返回值类型</typeparam>
    /// <typeparam name="TArgument">参数类型</typeparam>
    /// <returns>任务返回值</returns>
    /// <exception cref="KeyNotFoundException">不存在指定全局标识的任务</exception>
    public static Task<TReturn> InvokeTask<TReturn, TArgument>(string globalIdentifier, TArgument argument)
    {
        return Internal.InvokeTask<TReturn, TArgument>(globalIdentifier, argument);
    }

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

        public static Task<TReturn> InvokeTask<TReturn, TArgument>(string globalIdentifier,
            TArgument argument, FlowTaskInvokingInfo invokingInfo = default)
        {
            if (invokingInfo == default) invokingInfo = FlowTaskInvokingInfo.Default;
            return _FlowTasks.TryGetValue(globalIdentifier, out var task)
                ? task.Invoke<TReturn, TArgument>(argument, invokingInfo)
                : throw new KeyNotFoundException($"There is no task with identifier '{globalIdentifier}'.");
        }
    }
}

public interface IFlowTask
{
    public Task<TReturn> Invoke<TReturn, TArgument>(TArgument argument, FlowTaskInvokingInfo invokingInfo);
}

/// <summary>
/// Flow 任务调用信息，用于追踪详细调用过程
/// </summary>
/// <param name="DirectCaller">直接调用者，可能为 <see langword="null"/></param>
/// <param name="Callers">相关调用者，不存在时为空列表，不包含 <paramref name="DirectCaller"/></param>
public readonly record struct FlowTaskInvokingInfo(
    string? DirectCaller,
    IReadOnlyList<string> Callers)
{
    public static readonly FlowTaskInvokingInfo Default = new(null, []);
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
