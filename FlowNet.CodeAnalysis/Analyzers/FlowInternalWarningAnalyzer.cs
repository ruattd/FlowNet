using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis.Operations;

namespace FlowNet.CodeAnalysis.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FlowInternalWarningAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FLOW001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid calling Flow.Internal members",
        messageFormat: "Flow.Internal is intended for internal Flow.NET infrastructure and should not be called directly, use source generation and/or other public features instead",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var flowType = startContext.Compilation.GetTypeByMetadataName(Constants.FlowClass + "+Internal");
            if (flowType is null) return;
            var internalMethods = flowType.GetMembers()
                .OfType<IMethodSymbol>()
                .ToImmutableHashSet(SymbolEqualityComparer.Default);
            if (internalMethods.IsEmpty) return;
            startContext.RegisterOperationAction(
                action: operationContext => AnalyzeInvocation(operationContext, internalMethods), 
                operationKinds: OperationKind.Invocation
            );
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableHashSet<ISymbol?> internalMethods)
    {
        if (context.ContainingSymbol is IMethodSymbol methodSymbol && methodSymbol.HasGeneratedCodeAttribute()) return;
        if (context.Operation is not IInvocationOperation invocation) return;
        var targetMethod = invocation.TargetMethod.OriginalDefinition;
        if (!internalMethods.Contains(targetMethod)) return;
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
    }
}
