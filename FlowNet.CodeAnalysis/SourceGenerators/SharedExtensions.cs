using System.Collections.Generic;
using System.Linq;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;

namespace FlowNet.CodeAnalysis.SourceGenerators;

internal static class SharedExtensions
{
    extension(ISymbol symbol)
    {
        public IReadOnlyList<string> GetContainingScopes()
        {
            var containingType = symbol.ContainingType;
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
            return scopes;
        }
    }
}
