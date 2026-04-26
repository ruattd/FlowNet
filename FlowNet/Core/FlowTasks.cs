using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FlowNet.ComponentModel;

namespace FlowNet.Core;

partial class Flow
{
    private static readonly Dictionary<string, IFlowTask> _FlowTasks = new()
    {
        ["flow:run"] = new FlowRunTask(),
    };

    partial class Internal
    {
        public static void RegisterTask(string globalIdentifier, IFlowTask task,
            params IEnumerable<(string? before, string? after, int priority)> runConfigs)
        {
            if (_FlowTasks.ContainsKey(globalIdentifier))
                throw new InvalidOperationException($"Task with identifier '{globalIdentifier}' has already existed.");
            foreach (var (before, after, priority) in runConfigs)
                FlowRunTask.AddConfig(globalIdentifier, before, after, priority);
            _FlowTasks[globalIdentifier] = task;
        }
    }
}

file sealed class FlowRunTask : IFlowTask
{
    private readonly record struct FlowTaskAutoRunConfig(
        string Identifier,
        string? Before,
        string? After,
        int Priority
    );

    private static readonly List<FlowTaskAutoRunConfig> _AutoRunConfigs = [];

    public static void AddConfig(string identifier, string? before, string? after, int priority)
        => _AutoRunConfigs.Add(new FlowTaskAutoRunConfig(identifier, before, after, priority));

    private static bool _isInvoked = false;
    private static readonly object _InvokeLock = new();

    private static List<IReadOnlyList<(string, IReadOnlyCollection<string>)>>? _AutoRunMap = null;

    private static void BuildAutoRunMap()
    {
        var autoRunConfigMap = new Dictionary<string, List<FlowTaskAutoRunConfig>>(StringComparer.Ordinal);
        var priorityMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var config in _AutoRunConfigs)
        {
            if (!autoRunConfigMap.TryGetValue(config.Identifier, out var configs))
                autoRunConfigMap[config.Identifier] = configs = [];
            configs.Add(config);
            if (!priorityMap.TryGetValue(config.Identifier, out var priority) || config.Priority > priority)
                priorityMap[config.Identifier] = config.Priority;
        }

        var allIdentifiers = new List<string>();
        var allIdentifierSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identifier in Flow.TaskIdentifiers)
        {
            if (string.Equals(identifier, "flow:run", StringComparison.Ordinal))
                continue;
            if (allIdentifierSet.Add(identifier))
                allIdentifiers.Add(identifier);
        }
        allIdentifiers.Sort(StringComparer.Ordinal);

        var beforeTargetsMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var afterTargetsMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var beforeSourcesMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var afterSourcesMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var pair in autoRunConfigMap)
        {
            var sourceIdentifier = pair.Key;
            foreach (var config in pair.Value)
            {
                if (!string.IsNullOrWhiteSpace(config.Before))
                {
                    foreach (var targetIdentifier in ResolveMatches(config.Before!, sourceIdentifier))
                    {
                        AddRelation(beforeTargetsMap, sourceIdentifier, targetIdentifier);
                        AddRelation(beforeSourcesMap, targetIdentifier, sourceIdentifier);
                    }
                }

                if (!string.IsNullOrWhiteSpace(config.After))
                {
                    foreach (var targetIdentifier in ResolveMatches(config.After!, sourceIdentifier))
                    {
                        AddRelation(afterTargetsMap, sourceIdentifier, targetIdentifier);
                        AddRelation(afterSourcesMap, targetIdentifier, sourceIdentifier);
                    }
                }
            }
        }

        var includedIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        var pendingIdentifiers = new Queue<string>();
        foreach (var pair in afterTargetsMap)
        {
            IncludeIdentifier(pair.Key);
            foreach (var targetIdentifier in pair.Value)
                IncludeIdentifier(targetIdentifier);
        }

        while (pendingIdentifiers.Count > 0)
        {
            var identifier = pendingIdentifiers.Dequeue();
            if (!beforeSourcesMap.TryGetValue(identifier, out var sourceIdentifiers))
                continue;
            foreach (var sourceIdentifier in sourceIdentifiers)
                IncludeIdentifier(sourceIdentifier);
        }

        var sortedIdentifiers = new List<string>(includedIdentifiers);
        sortedIdentifiers.Sort(StringComparer.Ordinal);

        var adjacencyMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var indegreeMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var identifier in sortedIdentifiers)
        {
            adjacencyMap[identifier] = new HashSet<string>(StringComparer.Ordinal);
            indegreeMap[identifier] = 0;
        }

        foreach (var pair in afterTargetsMap)
        {
            foreach (var targetIdentifier in pair.Value)
                AddEdge(targetIdentifier, pair.Key);
        }

        foreach (var pair in beforeTargetsMap)
        {
            foreach (var targetIdentifier in pair.Value)
                AddEdge(pair.Key, targetIdentifier);
        }

        var currentLevel = new List<string>();
        foreach (var identifier in sortedIdentifiers)
        {
            if (indegreeMap[identifier] == 0)
                currentLevel.Add(identifier);
        }
        SortLevel(currentLevel);

        _AutoRunMap = [];
        var resolvedCount = 0;
        while (currentLevel.Count > 0)
        {
            var level = new (string, IReadOnlyCollection<string>)[currentLevel.Count];
            for (var i = 0; i < currentLevel.Count; i++) level[i] = BuildEntry(currentLevel[i]);
            _AutoRunMap.Add(level);
            resolvedCount += currentLevel.Count;

            var nextLevel = new List<string>();
            foreach (var identifier in currentLevel)
            {
                foreach (var nextIdentifier in adjacencyMap[identifier])
                {
                    indegreeMap[nextIdentifier]--;
                    if (indegreeMap[nextIdentifier] == 0)
                        nextLevel.Add(nextIdentifier);
                }
            }

            SortLevel(nextLevel);
            currentLevel = nextLevel;
        }

        if (resolvedCount != sortedIdentifiers.Count)
        {
            var cyclicIdentifiers = new List<string>();
            foreach (var identifier in sortedIdentifiers)
            {
                if (indegreeMap[identifier] > 0)
                    cyclicIdentifiers.Add(identifier);
            }
            cyclicIdentifiers.Sort(StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Auto-run order contains cyclic dependencies: {string.Join(", ", cyclicIdentifiers)}.");
        }

        return;

        void AddRelation(Dictionary<string, HashSet<string>> relationMap, string sourceIdentifier,
            string targetIdentifier)
        {
            if (!relationMap.TryGetValue(sourceIdentifier, out var identifiers))
                relationMap[sourceIdentifier] = identifiers = new HashSet<string>(StringComparer.Ordinal);
            identifiers.Add(targetIdentifier);
        }

        void IncludeIdentifier(string identifier)
        {
            if (includedIdentifiers.Add(identifier))
                pendingIdentifiers.Enqueue(identifier);
        }

        void AddEdge(string fromIdentifier, string toIdentifier)
        {
            if (string.Equals(fromIdentifier, toIdentifier, StringComparison.Ordinal)
                || !includedIdentifiers.Contains(fromIdentifier)
                || !includedIdentifiers.Contains(toIdentifier))
                return;
            if (adjacencyMap[fromIdentifier].Add(toIdentifier))
                indegreeMap[toIdentifier]++;
        }

        void SortLevel(List<string> identifiers)
        {
            identifiers.Sort((left, right) =>
            {
                var byPriority = GetPriority(right).CompareTo(GetPriority(left));
                return byPriority != 0
                    ? byPriority
                    : StringComparer.Ordinal.Compare(left, right);
            });
        }

        int GetPriority(string identifier)
            => priorityMap.TryGetValue(identifier, out var priority) ? priority : 0;

        (string, IReadOnlyCollection<string>) BuildEntry(string identifier)
        {
            var seenIdentifiers = new HashSet<string>(StringComparer.Ordinal) { identifier };

            if (beforeTargetsMap.TryGetValue(identifier, out var beforeTargets))
            {
                var actualTargets = beforeTargets.Where(targetIdentifier => includedIdentifiers.Contains(targetIdentifier)).ToList();
                foreach (var targetIdentifier in actualTargets) seenIdentifiers.Add(targetIdentifier);
            }
            if (afterSourcesMap.TryGetValue(identifier, out var afterSources))
            {
                foreach (var sourceIdentifier in afterSources) seenIdentifiers.Add(sourceIdentifier);
            }

            seenIdentifiers.Remove(identifier);
            return (identifier, seenIdentifiers);
        }

        IEnumerable<string> ResolveMatches(string pattern, string sourceIdentifier)
        {
            if (allIdentifierSet.Contains(pattern)) {
                if (!string.Equals(pattern, sourceIdentifier, StringComparison.Ordinal)) yield return pattern;
                yield break;
            }

            string regexPattern;
            if (pattern.Length > 1 && pattern[0] == '/' && pattern[pattern.Length - 1] == '/')
            {
                regexPattern = pattern.Substring(1, pattern.Length - 2);
            }
            else
            {
                var hasWildcard = false;
                var hasRegex = false;
                foreach (var c in pattern)
                {
                    switch (c)
                    {
                        case '*':
                        case '?':
                            hasWildcard = true;
                            break;
                        case '[': case ']': case '(': case ')':
                        case '{': case '}': case '+': case '|':
                        case '\\': case '^': case '$': case '.':
                            hasRegex = true;
                            break;
                    }
                }

                if (!hasWildcard && !hasRegex)
                    yield break;

                regexPattern = hasRegex ? pattern : Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
            }

            Regex regex;
            try
            {
                regex = new Regex($"^(?:{regexPattern})$", RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Invalid auto-run anchor pattern '{pattern}'.", ex);
            }

            foreach (var candidate in allIdentifiers)
            {
                if (string.Equals(candidate, sourceIdentifier, StringComparison.Ordinal)) continue;
                if (regex.IsMatch(candidate)) yield return candidate;
            }
        }
    }

    private static async Task Run()
    {
        if (_AutoRunMap == null) await Task.Run(BuildAutoRunMap).ConfigureAwait(false);
        foreach (var section in _AutoRunMap!)
        {
            var tasks = new Task[section.Count];
            for (var i = 0; i < section.Count; i++)
            {
                var (id, callers) = section[i];
                if (!Flow.ExistsTask(id)) continue; // bypass non-existent identifier
                var invokingInfo = FlowTaskInvokingInfo.Default;
                if (Flow.EnableTaskInvokingInfo)
                    invokingInfo = new FlowTaskInvokingInfo("flow:run", callers);
                tasks[i] = Task.Run(() => Flow.Internal.InvokeTask<None, None>(id, default, invokingInfo));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public Task<TReturn> Invoke<TReturn, TArgument>(TArgument argument, FlowTaskInvokingInfo _)
    {
        if (argument is not None) throw new InvalidCastException("Argument is not supported by 'flow:run'");
        if (default(None) is not TReturn) throw new InvalidCastException("Return value is not supported by 'flow:run'");
        lock (_InvokeLock)
        {
            if (_isInvoked)
            {
                if (!Flow.AllowMultipleAutoRunInvoking)
                    throw new InvalidOperationException("Multiple invoking of 'flow:run' is prohibited");
            }
            else _isInvoked = true;
        }
        return Run().ContinueWith(_ => default(TReturn)!);
    }
}
