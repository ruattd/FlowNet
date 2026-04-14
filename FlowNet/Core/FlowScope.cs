using System;
using System.Collections.Generic;

namespace FlowNet.Core;

partial class Flow
{
    public sealed class ScopeContext
    {
        private ScopeContext() {}

        private static readonly HashSet<string> _CreatedIdentifiers = [];

        public static ScopeContext Create(string identifier)
        {
            return !_CreatedIdentifiers.Add(identifier)
                ? throw new InvalidOperationException($"Identifier '{identifier}' already exists")
                : new ScopeContext { Identifier = identifier };
        }

        public required string Identifier { get; init; }
    }
}
