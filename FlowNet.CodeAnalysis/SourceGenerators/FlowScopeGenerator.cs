using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlowNet.CodeAnalysis.SourceGenerators;

[Generator]
public class FlowScopeGenerator : IIncrementalGenerator
{
    private readonly record struct ScopeModel(
        string Identifier,
        IReadOnlyList<string> ContainingScopes)
    {
        public string GlobalIdentifier { get; } = string.Join(":", ContainingScopes.Append(Identifier));
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scopes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: Constants.FlowScopeAttributeMetadataName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) =>
            {
                if (ctx.TargetSymbol is not INamedTypeSymbol type) return default;
                var attr = ctx.Attributes[0];
                var identifier = attr.ConstructorArguments[0].Value!.ToString();
                var containingScopes = type.GetContainingScopes();
                return new ScopeModel(identifier, containingScopes);
            })
            .Where(x => x != default)
            .Collect();

        context.RegisterSourceOutput(scopes, _GenerateScopeImplementations);
    }

    private static void _GenerateScopeImplementations(SourceProductionContext spc, ImmutableArray<ScopeModel> scopes)
    {
        // TODO
    }
}
