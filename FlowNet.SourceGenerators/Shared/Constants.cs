namespace FlowNet.SourceGenerators.Shared;

internal static class Constants
{
    public const string ExcludeFromCodeCoverage = "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]";

    public const string FlowCoreNamespace = "FlowNet.Core";
    public const string FlowClass = $"{FlowCoreNamespace}.Flow";

    public const string FlowScopeAttribute = $"{FlowClass}.ScopeAttribute";
    public const string FlowTaskAttribute = $"{FlowClass}.TaskAttribute";
    public const string FlowRunAttribute = $"{FlowClass}.RunAttribute";
}
