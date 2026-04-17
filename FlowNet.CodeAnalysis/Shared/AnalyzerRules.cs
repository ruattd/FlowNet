using Microsoft.CodeAnalysis;

namespace FlowNet.CodeAnalysis.Shared;

internal static class AnalyzerRules
{
    public static readonly DiagnosticDescriptor AvoidCallingFlowInternalMembers = new(
        id: "FLOW001",
        title: "Avoid calling Flow.Internal members",
        messageFormat: "Flow.Internal is intended for internal Flow.NET infrastructure and should not be called directly, use source generation and/or other public features instead",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateIdentifier = new(
        id: "FLOW002",
        title: "Duplicate identifier",
        messageFormat: "Identifier '{0}' is duplicated",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
