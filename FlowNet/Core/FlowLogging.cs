using System;
using System.Collections.Generic;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
    private static readonly FlowEvent<FlowLogItem> _LogEvent = new();

    /// <summary>
    /// 记录日志时触发的预览事件
    /// </summary>
    public static event FlowPreviewEventHandler<FlowLogItem> PreviewOnLog
    {
        add => _LogEvent.AddPreviewHandler(value);
        remove => throw new NotSupportedException();
    }

    /// <summary>
    /// 记录日志时触发的事件
    /// </summary>
    public static event FlowEventHandler<FlowLogItem> OnLog
    {
        add => _LogEvent.AddHandler(value);
        remove => throw new NotSupportedException();
    }

    partial class Internal
    {
        public static void SendLog(FlowLogItem logItem) => _LogEvent.Invoke(logItem);
    }
}

public static class FlowLoggingExtensions
{
    public static void Send(this FlowLogItem item) => Flow.Internal.SendLog(item);

    public static void Log(this Flow.ScopeContext context, string message,
        Exception? cause = null, Action<FlowLogItem>? configScope = null)
    {
        var logItem = FlowLogItem.Create(message, context.Identifier, cause);
        configScope?.Invoke(logItem);
        logItem.Send();
    }
}

public readonly struct FlowLogItem : IEquatable<FlowLogItem>
{
    private static ulong _nextLogId = 0;

    private readonly ulong _id = _nextLogId++;

    public bool Equals(FlowLogItem other)
    {
        return _id == other._id;
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

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
    /// 获取或设置日志项的元数据
    /// </summary>
    /// <param name="key">元数据键</param>
    /// <exception cref="KeyNotFoundException">指定的键不存在</exception>
    /// <exception cref="NotSupportedException">元数据字典不支持修改</exception>
    /// <returns>指定的键对应的元数据值</returns>
    public AnyValue this[string key]
    {
        get => key switch
        {
            nameof(Message) => AnyValue.Of(Message),
            nameof(ScopeIdentifier) => AnyValue.Of(ScopeIdentifier),
            nameof(Cause) => AnyValue.Of(Cause),
            _ => _metadata?[key] ?? throw new KeyNotFoundException($"Key '{key}' not found in metadata")
        };
        set
        {
            IDictionary<string, AnyValue> metadata;
            if (_metadata == null) metadata = new Dictionary<string, AnyValue>();
            else if (_metadata is IDictionary<string, AnyValue> data) metadata = data;
            else throw new NotSupportedException("Immutable metadata dictionary");
            metadata[key] = value;
        }
    }

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
