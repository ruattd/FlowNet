using System.Collections.Generic;

namespace FlowNet.ComponentModel;

/// <summary>
/// 事件参数
/// </summary>
/// <typeparam name="TData">事件数据类型</typeparam>
public ref struct FlowEventArgs<TData>
{
    /// <summary>
    /// 事件数据
    /// </summary>
    public TData Data { get; init; }
}

/// <summary>
/// 预览事件参数
/// </summary>
/// <typeparam name="TData">事件数据类型</typeparam>
public ref struct FlowPreviewEventArgs<TData>()
{
    /// <summary>
    /// 指定该事件是否被取消
    /// </summary>
    public bool IsCanceled { get; set; } = false;

    /// <summary>
    /// 事件数据
    /// </summary>
    public TData Data { get; init; }
}

public delegate void FlowEventHandler<TData>(ref FlowEventArgs<TData> e);

public delegate void FlowPreviewEventHandler<TData>(ref FlowPreviewEventArgs<TData> e);

public interface IFlowEvent
{
}

public interface IFlowEvent<in TData> : IFlowEvent
{
    public void Invoke(TData data);
}

public interface IFlowEventAddable<TData>
{
    public void AddHandler(FlowEventHandler<TData> handler);
    public void AddPreviewHandler(FlowPreviewEventHandler<TData> handler);
}

public interface IFlowEventRemovable<TData>
{
    public void RemoveHandler(FlowEventHandler<TData> handler);
    public void RemovePreviewHandler(FlowPreviewEventHandler<TData> handler);
}

public abstract class FlowCommonEvent<TData> : IFlowEvent<TData>
{
    public abstract IEnumerable<FlowEventHandler<TData>> Handlers { get; }
    public abstract IEnumerable<FlowPreviewEventHandler<TData>> PreviewHandlers { get; }

    public void Invoke(TData data)
    {
        var previewArgs = new FlowPreviewEventArgs<TData> { Data = data };
        foreach (var handler in PreviewHandlers)
        {
            handler.Invoke(ref previewArgs);
            if (previewArgs.IsCanceled) return;
        }
        var args = new FlowEventArgs<TData> { Data = data };
        foreach (var handler in Handlers) handler.Invoke(ref args);
    }
}

public class FlowEvent<TData> : FlowCommonEvent<TData>, IFlowEventAddable<TData>
{
    private readonly List<FlowEventHandler<TData>> _handlers = [];
    private readonly List<FlowPreviewEventHandler<TData>> _previewHandlers = [];
    public override IEnumerable<FlowEventHandler<TData>> Handlers => _handlers;
    public override IEnumerable<FlowPreviewEventHandler<TData>> PreviewHandlers => _previewHandlers;
    public void AddHandler(FlowEventHandler<TData> handler) => _handlers.Add(handler);
    public void AddPreviewHandler(FlowPreviewEventHandler<TData> handler) => _previewHandlers.Add(handler);
}

public class FlowHashEvent<TData> : FlowCommonEvent<TData>, IFlowEventAddable<TData>, IFlowEventRemovable<TData>
{
    private readonly HashSet<FlowEventHandler<TData>> _handlers = [];
    private readonly HashSet<FlowPreviewEventHandler<TData>> _previewHandlers = [];
    public override IEnumerable<FlowEventHandler<TData>> Handlers => _handlers;
    public override IEnumerable<FlowPreviewEventHandler<TData>> PreviewHandlers => _previewHandlers;
    public void AddHandler(FlowEventHandler<TData> handler) => _handlers.Add(handler);
    public void AddPreviewHandler(FlowPreviewEventHandler<TData> handler) => _previewHandlers.Add(handler);
    public void RemoveHandler(FlowEventHandler<TData> handler) => _handlers.Remove(handler);
    public void RemovePreviewHandler(FlowPreviewEventHandler<TData> handler) => _previewHandlers.Remove(handler);
}
