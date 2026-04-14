using System;
using System.Collections.Generic;

namespace Flow.Core;

partial class Flow
{
    public sealed class Context
    {
        private Context() {}

        private static readonly HashSet<string> _Identifiers = [];

        public static Context Create(string identifier)
        {
            return !_Identifiers.Add(identifier)
                ? throw new InvalidOperationException($"Identifier '{identifier}' already exists")
                : new Context { Identifier = identifier };
        }

        public required string Identifier { get; init; }
    }
}
