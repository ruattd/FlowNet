using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
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
    /// 查询是否存在指定标识的 Flow 任务。
    /// </summary>
    /// <param name="globalIdentifier">全局标识，即包含作用域前缀的完整标识符</param>
    /// <returns>若存在则返回 <see langword="true"/>，否则返回 <see langword="false"/></returns>
    public static bool ExistsTask(string globalIdentifier)
        => _FlowTasks.ContainsKey(globalIdentifier);

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
    /// 使用该方法及其变体调用任务不会产生任何调用信息，即 <see cref="EnableTaskInvokingInfo"/> 为
    /// <see langword="true"/> 时，任务仍然只会收到 <see cref="FlowTaskInvokingInfo.Default"/> 值。<br/>
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
/// Flow 任务调用信息，用于追踪详细调用过程。<br/>
/// <b>NOTE</b>: 设置 <see cref="Flow.EnableTaskInvokingInfo"/> 为 <see langword="true"/>，并使用
/// <see cref="Flow.InvokingInfoAttribute"/> 标记任务方法的<b>第一个参数</b>以接收调用信息。
/// </summary>
/// <param name="DirectCaller">直接调用者，可能为 <see langword="null"/></param>
/// <param name="Callers">相关调用者，不存在时为空列表，不包含 <paramref name="DirectCaller"/></param>
public readonly record struct FlowTaskInvokingInfo(
    string Target,
    string? DirectCaller,
    IReadOnlyCollection<string> Callers)
{
    public static readonly FlowTaskInvokingInfo Default = new(string.Empty, null, []);

    public override string ToString()
    {
        var s = $"TaskInvokingInfo {{ " +
            $"Target: '{Target}', " +
            $"DirectCaller: '{DirectCaller ?? "null"}', " +
            $"Callers: ['{string.Join("', '", Callers)}'] }}";
        return s;
    }
}
