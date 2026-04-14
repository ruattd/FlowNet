using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FlowNet.SourceGenerators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlowNet.SourceGenerators.Core;

[Generator(LanguageNames.CSharp)]
public class FlowTaskGenerator : IIncrementalGenerator
{
    private readonly record struct TaskAutoRunModel(
        string? Before,
        string? After
    );

    private readonly record struct TaskModel(
        string Identifier,
        IMethodSymbol Method,
        IReadOnlyList<string> Scopes,
        IReadOnlyList<TaskAutoRunModel> AutoRuns
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tasks = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: Constants.FlowTaskAttribute,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) =>
            {
                var method = (IMethodSymbol)ctx.TargetSymbol;
                var containingType = method.ContainingType;
                if (containingType?.IsPartial() != true) return default;
                // 查找 scope
                var containingScopes = new Stack<string>();
                while (containingType != null)
                {
                    var scopeAttr = containingType.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.GetFullyQualifiedName() == Constants.FlowScopeAttribute);
                    if (scopeAttr?.ConstructorArguments[0].Value is not string scopeIdentifier) break;
                    containingScopes.Push(scopeIdentifier);
                    containingType = containingType.ContainingType;
                }
                var scopes = new List<string>();
                while (containingScopes.Count > 0) scopes.Add(containingScopes.Pop());
                // 提取 identifier
                var attr = ctx.Attributes.First(a =>
                    a.AttributeClass!.GetFullyQualifiedName() == Constants.FlowTaskAttribute);
                string? identifier = null;
                if (attr.ConstructorArguments.Length > 0) identifier = attr.ConstructorArguments[0].Value as string;
                if (identifier == null)
                {
                    var methodName = method.Name.Trim('_');
                    identifier = (string.IsNullOrEmpty(methodName) ? method.ContainingType.Name : methodName).PascalToSnakeId();
                }
                // 提取自动执行配置
                var runAttrs = ctx.Attributes.Where(a =>
                    a.AttributeClass!.GetFullyQualifiedName() == Constants.FlowRunAttribute);
                var autoRuns = (
                    from a in runAttrs
                    let before = a.NamedArguments.FirstOrDefault(x => x.Key == "Before").Value.Value as string
                    let after = a.NamedArguments.FirstOrDefault(x => x.Key == "After").Value.Value as string
                    select new TaskAutoRunModel(before, after)
                ).ToList();
                return (method.ContainingType, new TaskModel(identifier, method, scopes, autoRuns));
            })
            .Where(x => x != default)
            .Collect();

        context.RegisterSourceOutput(tasks, _GenerateTaskInvokePoints);
    }

    private static void _GenerateTaskInvokePoints(SourceProductionContext spc,
        ImmutableArray<(INamedTypeSymbol ContainingType, TaskModel Task)> items)
    {
        var groupedTasks = items
            .GroupBy(x => x.ContainingType, SymbolEqualityComparer.Default)
            .Where(g => g.Key != null)
            .Select(g => ((INamedTypeSymbol)g.Key!, g.Select(x => x.Task)));

        foreach (var (type, tasks) in groupedTasks)
        {
            var sb = new StringBuilder();

            // file header
            sb.AppendCommonHeader();
            sb.AppendLine();

            // containing type
            sb.AppendTypeHeader(out var indent, type);
            var indentStr = new string(' ', indent * 4);

            // invoking methods implementation
            sb.Append(indentStr).AppendLine("public static class FlowTasks");
            sb.Append(indentStr).AppendLine("{");

            foreach (var task in tasks)
            {
            }

            sb.Append(indentStr).AppendLine("}");

            while (--indent >= 0) sb.Append(new string(' ', indent * 4)).AppendLine("}");

            // task implementation
            // ...

            // register source
            spc.AddSource(type.GetFullyQualifiedName(), sb.ToString());
        }
    }
}
