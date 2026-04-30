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

        static bool AnchorMatches(string? anchor, string identifier)
            => anchor != null && Regex.IsMatch(identifier, anchor);

        var configs = _AutoRunConfigs
            .Select((config, index) => (config, index))
            .ToArray();

        var duplicateIdentifiers = new HashSet<string>(configs
            .GroupBy(x => x.config.Identifier)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));

        foreach (var duplicateIdentifier in duplicateIdentifiers)
        {
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
        for (var i = 0; i < configs.Length; i++)
        {
            matchedBefore[i] = [];
            matchedAfter[i] = [];
            incomingAfter[i] = [];
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
        var includeQueue = new Queue<int>();

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
            foreach (var target in matchedAfter[node]) Include(target);
            foreach (var target in matchedBefore[node]) Include(target);
        }

        var edges = new HashSet<int>[configs.Length];
        for (var i = 0; i < configs.Length; i++) edges[i] = [];

        for (var i = 0; i < configs.Length; i++)
        {
            if (!included[i]) continue;

            foreach (var target in matchedAfter[i])
            {
                if (included[target])
                    edges[target].Add(i);
            }

            foreach (var target in matchedBefore[i])
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
        var queue = new Queue<int>();
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

        var includedCount = included.Count(x => x);
        if (visitedCount != includedCount)
            throw new InvalidOperationException("Circular auto-run dependency detected.");

        var runMap = levels
            .Select((level, index) => (level, index))
            .Where(x => included[x.index])
            .GroupBy(x => x.level)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<(string, IReadOnlyCollection<string>)>)g
                .OrderBy(x => configs[x.index].config.Priority)
                .ThenBy(x => x.index)
                .Select(x =>
                {
                    var callers = new HashSet<string>();
                    foreach (var target in matchedBefore[x.index]) callers.Add(configs[target].config.Identifier);
                    foreach (var target in matchedAfter[x.index]) callers.Add(configs[target].config.Identifier);
                    foreach (var source in incomingAfter[x.index]) callers.Add(configs[source].config.Identifier);
                    return (configs[x.index].config.Identifier, (IReadOnlyCollection<string>)callers);
                })
                .ToArray())
            .ToList();

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
