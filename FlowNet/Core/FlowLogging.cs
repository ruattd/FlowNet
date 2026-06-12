using System;
using System.Collections.Generic;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
    public static event Action<FlowLogItem>? OnLog;

    partial class Internal
    {
        public static void CreateLog(FlowLogItem logItem) => OnLog?.Invoke(logItem);
    }
}

public readonly record struct FlowLogItem
{
    /// <summary>
    /// 日志项的内容
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 产生日志项的作用域
    /// </summary>
    public string? ScopeIdentifier { get; }

    /// <summary>
    /// 造成日志项的异常
    /// </summary>
    public Exception? Cause { get; }

    private readonly IReadOnlyDictionary<string, AnyValue>? _metadata;

    private FlowLogItem(string message, string? scopeIdentifier,
        Exception? cause, IReadOnlyDictionary<string, AnyValue>? metadata)
    {
        Message = message;
        ScopeIdentifier = scopeIdentifier;
        Cause = cause;
        _metadata = metadata;
    }

    /// <summary>
    /// 获取日志项的元数据
    /// </summary>
    /// <param name="key">元数据键</param>
    /// <exception cref="KeyNotFoundException">指定的键不存在</exception>
    /// <returns>指定的键对应的元数据值</returns>
    public AnyValue this[string key] => key switch
    {
        nameof(Message) => AnyValue.Of(Message),
        nameof(ScopeIdentifier) => AnyValue.Of(ScopeIdentifier),
        nameof(Cause) => AnyValue.Of(Cause),
        _ => _metadata?[key] ?? throw new KeyNotFoundException($"Key '{key}' not found in metadata")
    };

    /// <summary>
    /// 创建日志项
    /// </summary>
    /// <param name="message">日志消息内容</param>
    /// <param name="scopeIdentifier">来源作用域标识符</param>
    /// <param name="cause">造成日志项的相关异常</param>
    /// <param name="metadata">元数据字典</param>
    /// <returns>日志项</returns>
    public static FlowLogItem Create(string message, string? scopeIdentifier = null,
        Exception? cause = null, IReadOnlyDictionary<string, AnyValue>? metadata = null)
    {
        return new FlowLogItem(message, scopeIdentifier, cause, metadata);
    }

    /// <summary>
    /// 创建日志项
    /// </summary>
    /// <param name="message">日志消息内容</param>
    /// <param name="scopeIdentifier">来源作用域标识符</param>
    /// <param name="cause">造成日志项的相关异常</param>
    /// <param name="metadata">元数据, 每个元组均为一组键值对</param>
    /// <returns>日志项</returns>
    public static FlowLogItem Create(string message, string? scopeIdentifier = null,
        Exception? cause = null, params IEnumerable<(string key, AnyValue value)> metadata)
    {
        IReadOnlyDictionary<string, AnyValue>? dataDict = null;
        using var it = metadata.GetEnumerator();
        if (it.MoveNext())
        {
            var data = new Dictionary<string, AnyValue> { [it.Current.key] = it.Current.value };
            while (it.MoveNext()) data[it.Current.key] = it.Current.value;
            dataDict = data;
        }
        return Create(message, scopeIdentifier, cause, dataDict);
    }
}
