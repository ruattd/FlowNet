namespace FlowNet.Core;

/// <summary>
/// Flow 核心操作类。
/// </summary>
public static partial class Flow
{
    /// <summary>
    /// Flow 内部操作类，如无特殊需求请勿调用。
    /// </summary>
    public static partial class Internal;

    private static readonly ScopeContext Context = ScopeContext.Create("flow");
}
