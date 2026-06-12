using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#if NET5_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
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
        // === Usage ===
        // 根据 _AutoRunConfigs 中的内容对其中 identifier 排序并生成局部变量 runMap，赋值给 _AutoRunMap
        // 排序规则：
        // 1. after 锚点表示该 identifier 排在锚点命中的 identifier 之后，且该 identifier 和命中的 identifier 都应添加进 runMap
        // 2. before 锚点表示该 identifier 排在锚点命中的 identifier 之前，但在该 identifier 的 after 锚点为 null 且不存在其他 identifier 的 after 锚点命中该 identifier 时，该 identifier 不应被添加进 runMap
        // 3. 互相之间没有明确的 before/after 关系且恰好在同一个位置的两个 identifier 应属于同级
        // 4. 遵循“最小距离”原则，即所有 identifier 与其锚点命中的 identifier 的距离应尽可能近。
        // 生成的 runMap：
        // 1. 应为一个由上述排序规则规定的 List，该 List 的每一个元素均为由同级 identifier 元素构成的列表，列表按照 identifier 的 priority 排序
        // 2. 每个 identifier 元素均为 identifier 本身和一个 HashSet 构成的 ValueTuple，该 HashSet 由“该 identifier 的 before/after 命中的 identifier”和“after 命中该 identifier 的 identifier”构成
        // 3. 若 _AutoRunConfigs 中重复出现同一个 identifier，则应分别按各自的锚点将该 identifier 添加进 runMap 多次，但此时其他 identifier 不应命中该 identifier，若命中则抛出异常
        // 4. 不应存在循环引用，若存在则抛出异常

        static bool IsRegexAnchor(string anchor)
        {
            var escaped = false;
            foreach (var c in anchor)
            {
                if (escaped) return true;
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c is '.' or '+' or '?' or '|' or '(' or ')' or '[' or ']' or '{' or '}' or '^' or '$')
                    return true;
            }
            return false;
        }

        static string BuildWildcardRegex(string anchor)
        {
            var patterns = new List<string>(anchor.Length);
            for (var i = 0; i < anchor.Length; i++)
            {
                if (anchor[i] == '*')
                {
                    if (i + 1 < anchor.Length && anchor[i + 1] == '*')
                    {
                        patterns.Add(".*");
                        i++;
                    }
                    else patterns.Add("[^:]*");
                    continue;
                }
                patterns.Add(Regex.Escape(anchor[i].ToString()));
            }
            return string.Concat(patterns);
        }

        static bool AnchorMatches(string? anchor, string identifier)
        {
            if (anchor == null) return false;
            var pattern = IsRegexAnchor(anchor) ? anchor : BuildWildcardRegex(anchor);
            return Regex.IsMatch(identifier, $@"\A(?:{pattern})\z");
        }

#if NET5_0_OR_GREATER
        // 在 .NET 5+ 上将 List<int> 暴露为 Span<int>：免去 List<T>.Enumerator 的 _version 版本校验，
        // 并允许 JIT 在迭代时消除部分边界检查。CollectionsMarshal 与 Span<T> 在 netstandard2.0 不可用
        static Span<int> AsView(List<int> list)
            => CollectionsMarshal.AsSpan(list);
#else
        // netstandard2.0 回退为 List<T> 自身，foreach 仍走结构体枚举器，保持语义一致
        static List<int> AsView(List<int> list) => list;
#endif

        var configs = _AutoRunConfigs
            .Select((config, index) => (config, index))
            .ToArray();

        // 使用 Dictionary 直接统计标识符出现次数，避免 LINQ GroupBy/Where/Select/Distinct 的多次中间分配与延迟枚举开销
        var identifierCounts = new Dictionary<string, int>(configs.Length);
        foreach (var (config, _) in configs)
        {
#if NET6_0_OR_GREATER
            // .NET 6+ 提供 GetValueRefOrAddDefault：一次哈希查找即可拿到值槽位的 ref 并自增，
            // 相比 TryGetValue + 索引器赋值的双查询路径，每次累加减少一次哈希与相等比较
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(identifierCounts, config.Identifier, out _);
            count++;
#else
            identifierCounts.TryGetValue(config.Identifier, out var count);
            identifierCounts[config.Identifier] = count + 1;
#endif
        }

        foreach (var pair in identifierCounts)
        {
            if (pair.Value <= 1) continue;
            var duplicateIdentifier = pair.Key;
            foreach (var (config, _) in configs)
            {
                if (config.Identifier == duplicateIdentifier) continue;
                if (AnchorMatches(config.Before, duplicateIdentifier) || AnchorMatches(config.After, duplicateIdentifier))
                    throw new InvalidOperationException($"Multi-run task '{duplicateIdentifier}' cannot be targeted by '{config.Identifier}'.");
            }
        }

        var matchedBefore = new List<int>[configs.Length];
        var matchedAfter = new List<int>[configs.Length];
        var incomingAfter = new List<int>[configs.Length];
        // 给邻接表预设初始容量，避免从 0 反复扩容（List<T>(int) 在 netstandard2.0 与 net5.0 均可用）
        for (var i = 0; i < configs.Length; i++)
        {
            matchedBefore[i] = new List<int>(4);
            matchedAfter[i] = new List<int>(4);
            incomingAfter[i] = new List<int>(4);
        }

        for (var i = 0; i < configs.Length; i++)
        {
            var current = configs[i].config;
            for (var j = 0; j < configs.Length; j++)
            {
                if (i == j) continue;
                var target = configs[j].config.Identifier;
                if (AnchorMatches(current.Before, target)) matchedBefore[i].Add(j);
                if (AnchorMatches(current.After, target))
                {
                    matchedAfter[i].Add(j);
                    incomingAfter[j].Add(i);
                }
            }
        }

        var included = new bool[configs.Length];
        // 预设 Queue 容量上限，避免多次扩容（最坏情况入队所有节点）
        var includeQueue = new Queue<int>(configs.Length);

        void Include(int index)
        {
            if (included[index]) return;
            included[index] = true;
            includeQueue.Enqueue(index);
        }

        for (var i = 0; i < configs.Length; i++)
        {
            if (configs[i].config.After != null || incomingAfter[i].Count > 0)
                Include(i);
        }

        while (includeQueue.Count > 0)
        {
            var node = includeQueue.Dequeue();
            // 热点遍历：通过 AsView 在 .NET 5+ 上获得 Span 路径
            foreach (var target in AsView(matchedAfter[node])) Include(target);
            foreach (var target in AsView(matchedBefore[node])) Include(target);
        }

        var edges = new HashSet<int>[configs.Length];
        for (var i = 0; i < configs.Length; i++)
        {
#if NET5_0_OR_GREATER
            // HashSet<T>(int capacity) 构造函数在 netstandard2.0 中不存在，仅 .NET 5+ 提供，用它避免后续 Add 多次 rehash
            edges[i] = new HashSet<int>(4);
#else
            edges[i] = [];
#endif
        }

        for (var i = 0; i < configs.Length; i++)
        {
            if (!included[i]) continue;

            // 热点遍历：通过 AsView 在 .NET 5+ 上获得 Span 路径
            foreach (var target in AsView(matchedAfter[i]))
            {
                if (included[target])
                    edges[target].Add(i);
            }

            foreach (var target in AsView(matchedBefore[i]))
            {
                if (included[target])
                    edges[i].Add(target);
            }
        }

        var indegree = new int[configs.Length];
        for (var i = 0; i < configs.Length; i++)
        {
            if (!included[i]) continue;
            foreach (var target in edges[i])
            {
                if (included[target])
                    indegree[target]++;
            }
        }

        var levels = new int[configs.Length];
        var queue = new Queue<int>(configs.Length);
        for (var i = 0; i < configs.Length; i++)
        {
            if (included[i] && indegree[i] == 0)
                queue.Enqueue(i);
        }

        var visitedCount = 0;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            visitedCount++;

            foreach (var target in edges[node])
            {
                levels[target] = Math.Max(levels[target], levels[node] + 1);
                indegree[target]--;
                if (indegree[target] == 0)
                    queue.Enqueue(target);
            }
        }

        // 直接循环统计 included 数量，避免 LINQ Count(x => x) 的委托与枚举器分配
        var includedCount = 0;
        foreach (var t in included) if (t) includedCount++;
        if (visitedCount != includedCount)
            throw new InvalidOperationException("Circular auto-run dependency detected.");

        // 使用按 level 分桶的命令式实现替换 LINQ Select/Where/GroupBy/OrderBy/Select/ToArray 链，
        // 一次遍历建桶，桶内手动排序，减少中间集合与匿名委托分配
        var maxLevel = -1;
        for (var i = 0; i < configs.Length; i++)
        {
            if (included[i] && levels[i] > maxLevel)
                maxLevel = levels[i];
        }

        var levelBuckets = new List<int>?[maxLevel + 1];
        for (var i = 0; i < configs.Length; i++)
        {
            if (!included[i]) continue;
            var bucket = levelBuckets[levels[i]] ??= new List<int>(4);
            bucket.Add(i);
        }

        var runMap = new List<IReadOnlyList<(string, IReadOnlyCollection<string>)>>(maxLevel + 1);
        for (var level = 0; level <= maxLevel; level++)
        {
            var bucket = levelBuckets[level];
            if (bucket == null) continue;

            // 同级按 priority 升序排序，相同 priority 按原 index 维持稳定顺序
            bucket.Sort((a, b) =>
            {
                var cmp = configs[a].config.Priority.CompareTo(configs[b].config.Priority);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            var section = new (string, IReadOnlyCollection<string>)[bucket.Count];
            for (var k = 0; k < bucket.Count; k++)
            {
                var idx = bucket[k];
#if NET5_0_OR_GREATER
                // HashSet<T>(int capacity) 仅在 .NET 5+ 可用，按已知元素数量上限预分配，避免多次 rehash
                var callers = new HashSet<string>(matchedBefore[idx].Count + matchedAfter[idx].Count + incomingAfter[idx].Count);
#else
                var callers = new HashSet<string>();
#endif
                foreach (var target in AsView(matchedBefore[idx])) callers.Add(configs[target].config.Identifier);
                foreach (var target in AsView(matchedAfter[idx])) callers.Add(configs[target].config.Identifier);
                foreach (var source in AsView(incomingAfter[idx])) callers.Add(configs[source].config.Identifier);
                section[k] = (configs[idx].config.Identifier, callers);
            }
            runMap.Add(section);
        }

        _AutoRunMap = runMap;
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
                    invokingInfo = new FlowTaskInvokingInfo(id, "flow:run", callers);
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
