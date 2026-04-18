using System;
using System.Collections.Generic;

namespace FlowNet.Core;

partial class Flow
{
    public class ScopeContext
    {
        private static readonly HashSet<string> _CreatedIdentifiers = [];

        public string Identifier { get; }

        internal ScopeContext(string globalIdentifier)
        {
            if (!_CreatedIdentifiers.Add(globalIdentifier)) 
                throw new InvalidOperationException($"Global identifier '{globalIdentifier}' already exists");
            Identifier = globalIdentifier;
        }
    }

    partial class Internal
    {
        public static ScopeContext CreateScope(string identifier) => new(identifier);
    }
}
