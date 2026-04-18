using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FlowNet.CodeAnalysis.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DuplicateIdentifierAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AnalyzerRules.DuplicateIdentifier];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static ctx =>
        {
            _AnalyzeDuplicate(ctx, Constants.FlowTaskAttribute, SymbolKind.Method, true);
            _AnalyzeDuplicate(ctx, Constants.FlowScopeAttribute, SymbolKind.NamedType);
        });
    }

    private static void _AnalyzeDuplicate(CompilationStartAnalysisContext context,
        string targetAttr, SymbolKind symbolKind, bool enableAutoInferredIdentifier = false)
    {
        var distinct = new ConcurrentDictionary<string, AttributeData?>();
        var distinctCtx = new Dictionary<string, SymbolAnalysisContext>();

        context.RegisterSymbolAction(ctx =>
        {
            var symbol = ctx.Symbol;
            // match attribute
            var attr = symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.GetSimplifiedTypeName() == targetAttr);
            if (attr is null) return;
            // generate global identifier
            var identifier = attr.ConstructorArguments[0].Value?.ToString();
            if (identifier == null)
            {
                if (!enableAutoInferredIdentifier) return;
                identifier = (symbol.Name.StartsWith("_") ? symbol.Name.Substring(1) : symbol.Name).PascalToSnakeId();
            }
            var containingScopes = symbol.GetContainingScopes();
            var id = string.Join(":", containingScopes.Append(identifier));
            // report
            var result = distinct.AddOrUpdate(id, attr, (_, a) =>
            {
                Report(ctx, id, attr);
                if (a != null) Report(distinctCtx[id], id, a);
                return null;
            });
            if (result != null) distinctCtx[id] = ctx;
        }, symbolKind);

        return;

        void Report(SymbolAnalysisContext ctx, string id, AttributeData attr)
        {
            var attrSyntax = attr.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken) as AttributeSyntax;
            var loc = attrSyntax?.ArgumentList?.Arguments
                .FirstOrDefault()?.Expression.GetLocation() ?? attrSyntax?.GetLocation();
            if (loc is null) return;
            ctx.ReportDiagnostic(Diagnostic.Create(AnalyzerRules.DuplicateIdentifier, loc, id));
        }
    }
}
