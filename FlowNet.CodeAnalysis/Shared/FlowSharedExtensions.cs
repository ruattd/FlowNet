using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FlowNet.CodeAnalysis.Shared;

internal static class FlowSharedExtensions
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
