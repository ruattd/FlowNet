using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Flow.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class FlowTaskGenerator : IIncrementalGenerator
{
    private readonly record struct TaskModel(
        IReadOnlyList<string> Scopes,
        string Identifier,
        string QualifiedMethodName
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tasks = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: SharedConstants.FlowTaskAttribute,
            predicate: (node, _) => node is MethodDeclarationSyntax,
            transform: (ctx, _) =>
            {
                var method = (IMethodSymbol)ctx.TargetSymbol;
                var containingType = method.ContainingType;
                if (containingType?.IsPartial() != true) return default;
                // 查找 scope
                var containingScopes = new Stack<string>();
                while (containingType != null)
                {
                    var scopeAttr = containingType.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.GetFullyQualifiedName() == SharedConstants.FlowScopeAttribute);
                    if (scopeAttr?.ConstructorArguments[0].Value is not string scopeIdentifier) break;
                    containingScopes.Push(scopeIdentifier);
                    containingType = containingType.ContainingType;
                }
                var scopes = new List<string>();
                while (containingScopes.Count > 0) scopes.Add(containingScopes.Pop());
                // 提取 identifier
                var attr = ctx.Attributes.First(a =>
                    a.AttributeClass!.GetFullyQualifiedName() == SharedConstants.FlowTaskAttribute);
                string? identifier = null;
                if (attr.ConstructorArguments.Length > 0) identifier = attr.ConstructorArguments[0].Value as string;
                if (identifier == null)
                {
                    var methodName = method.Name.Trim('_');
                    identifier = (string.IsNullOrEmpty(methodName) ? method.ContainingType.Name : methodName).PascalToSnakeId();
                }
                return (method.ContainingType, new TaskModel(scopes, identifier, method.GetQualifiedSymbolName()));
            })
            .Where(x => x != default)
            .Collect();

        context.RegisterSourceOutput(tasks, _GenerateTaskInvokePoints);
    }

    private static void _GenerateTaskInvokePoints(SourceProductionContext spc,
        ImmutableArray<(INamedTypeSymbol ContainingType, TaskModel Task)> tasks)
    {
    }
}
