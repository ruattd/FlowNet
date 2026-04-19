namespace FlowNet.CodeAnalysis.Shared;

internal static class Constants
{
    public const string ExcludeFromCodeCoverage = "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]";

    public const string FlowCoreNamespace = "FlowNet.Core";
    public const string FlowClass = $"{FlowCoreNamespace}.Flow";

    private const string FlowScopeClassName = "ScopeAttribute";
    public const string FlowScopeAttribute = $"{FlowClass}.{FlowScopeClassName}";
    public const string FlowScopeAttributeMetadataName = $"{FlowClass}+{FlowScopeClassName}";

    private const string FlowTaskClassName = "TaskAttribute";
    public const string FlowTaskAttribute = $"{FlowClass}.{FlowTaskClassName}";
    public const string FlowTaskAttributeMetadataName = $"{FlowClass}+{FlowTaskClassName}";

    private const string FlowRunClassName = "RunAttribute";
    public const string FlowRunAttribute = $"{FlowClass}.{FlowRunClassName}";
    public const string FlowRunAttributeMetadataName = $"{FlowClass}+{FlowRunClassName}";

    private const string FlowInvokingInfoClassName = "InvokingInfoAttribute";
    public const string FlowInvokingInfoAttribute = $"{FlowClass}.{FlowInvokingInfoClassName}";
    public const string FlowInvokingInfoAttributeMetadataName = $"{FlowClass}+{FlowInvokingInfoClassName}";

    private const string FlowExtensionUsageClassName = "FlowExtensionUsageAttribute";
    public const string FlowExtensionUsageAttribute = $"{FlowCoreNamespace}.{FlowExtensionUsageClassName}";
}
