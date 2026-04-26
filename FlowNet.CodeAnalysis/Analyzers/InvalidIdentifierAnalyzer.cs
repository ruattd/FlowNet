using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FlowNet.CodeAnalysis.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InvalidIdentifierAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [
        AnalyzerRules.DuplicateIdentifier,
        AnalyzerRules.EmptyIdentifier
    ];

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

    private readonly record struct AttributeReportContext(
        SymbolAnalysisContext Context,
        AttributeData Data
    );

    private static void _AnalyzeDuplicate(CompilationStartAnalysisContext context,
        string targetAttr, SymbolKind symbolKind, bool enableAutoInferredIdentifier = false)
    {
        var distinct = new ConcurrentDictionary<string, AttributeReportContext?>();

        context.RegisterSymbolAction(ctx =>
        {
            var symbol = ctx.Symbol;
            // match attributes
            var attrs = symbol.GetAttributes().Where(a =>
                a.AttributeClass?.GetSimplifiedTypeName() == targetAttr);
            foreach (var attr in attrs)
            {
                // generate global identifier
                var identifier = attr.ConstructorArguments[0].Value?.ToString();
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    if (!enableAutoInferredIdentifier) continue;
                    var name = symbol.Name.Trim('_');
                    identifier = string.IsNullOrWhiteSpace(name) ? null : name.PascalToSnakeId();
                }
                var containingScopes = symbol.GetContainingScopes();
                if (identifier == null && containingScopes.Count == 0)
                {
                    // report empty identifier
                    var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                    if (loc != null) ctx.ReportDiagnostic(Diagnostic.Create(AnalyzerRules.EmptyIdentifier, loc));
                    continue;
                }
                var id = string.Join(":", identifier == null ? containingScopes : containingScopes.Append(identifier));
                var dc = new AttributeReportContext(ctx, attr);
                distinct.AddOrUpdate(id, dc, (_, d) =>
                {
                    // report duplicate
                    Report(id, dc);
                    if (d is { } c) Report(id, c, ctx);
                    return null;
                });
            }
        }, symbolKind);

        return;

        void Report(string id, AttributeReportContext c, SymbolAnalysisContext? fallbackCtx = null)
        {
            var attrSyntax = c.Data.ApplicationSyntaxReference?.GetSyntax(c.Context.CancellationToken) as AttributeSyntax;
            var loc = attrSyntax?.ArgumentList?.Arguments
                .FirstOrDefault()?.Expression.GetLocation() ?? attrSyntax?.GetLocation();
            if (loc is null) return;
            var diag = Diagnostic.Create(AnalyzerRules.DuplicateIdentifier, loc, id);
            try { c.Context.ReportDiagnostic(diag); }
            // fallback for microsoft's shit code in dotnet 10.0.200+
            catch (NullReferenceException) { fallbackCtx?.ReportDiagnostic(diag); }
            catch (ArgumentException) { /* ignoring */ }
        }
    }
}
