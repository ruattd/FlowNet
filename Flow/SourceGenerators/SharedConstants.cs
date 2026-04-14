namespace Flow.SourceGenerators;

internal static class SharedConstants
{
    public const string FlowNamespace = "Flow.Core";

    public const string ExcludeFromCodeCoverage = "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]";

    public const string FlowScopeAttribute = $"{FlowNamespace}.ScopeAttribute";
    public const string FlowTaskAttribute = $"{FlowNamespace}.TaskAttribute";
}
