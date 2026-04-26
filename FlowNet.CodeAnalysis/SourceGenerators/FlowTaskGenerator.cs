using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlowNet.CodeAnalysis.SourceGenerators;

[Generator]
public class FlowTaskGenerator : IIncrementalGenerator
{
    private readonly record struct TaskAutoRunModel(
        string? Before,
        string? After,
        int Priority
    );

    private readonly record struct TaskModel(
        IReadOnlyList<string?> Identifiers,
        IMethodSymbol Method,
        IReadOnlyList<string> Scopes,
        IReadOnlyList<TaskAutoRunModel> AutoRuns)
    {
        public IEnumerable<string> GlobalIdentifiers { get; } =
            Identifiers.Select(id => string.Join(":", id == null ? Scopes : Scopes.Append(id)));
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tasks = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: Constants.FlowTaskAttributeMetadataName,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) =>
            {
                var method = (IMethodSymbol)ctx.TargetSymbol;
                var scopes = method.GetContainingScopes();
                // 提取 identifier
                var identifiers = ctx.Attributes.Select(attr => {
                    string? identifier = null;
                    if (attr.ConstructorArguments.Length > 0) identifier = attr.ConstructorArguments[0].Value as string;
                    if (string.IsNullOrWhiteSpace(identifier))
                    {
                        var methodName = method.Name.Trim('_');
                        identifier = string.IsNullOrWhiteSpace(methodName) ? null : methodName.PascalToSnakeId();
                    }
                    return identifier;
                }).Distinct(StringComparer.InvariantCulture).ToList();
                // 提取自动执行配置
                var runAttrs = method.GetAttributes().Where(a =>
                    a.AttributeClass?.GetFullyQualifiedName() == Constants.FlowRunAttribute);
                var autoRuns = (
                    from a in runAttrs
                    let before = a.NamedArguments.FirstOrDefault(x => x.Key == "Before").Value.Value as string
                    let after = a.NamedArguments.FirstOrDefault(x => x.Key == "After").Value.Value as string
                    let priority = a.NamedArguments.FirstOrDefault(x => x.Key == "Priority").Value.Value is int p ? p : 0
                    select new TaskAutoRunModel(before, after, priority)
                ).ToList();
                return new TaskModel(identifiers, method, scopes, autoRuns);
            })
            .Where(x => x != default)
            .Collect();

        context.RegisterSourceOutput(tasks, _GenerateTaskInvokePoints);
    }

    private static void _GenerateTaskInvokePoints(SourceProductionContext spc,
        ImmutableArray<TaskModel> items)
    {
        var groupedTasks = items
            .GroupBy(x => x.Method.ContainingType, SymbolEqualityComparer.Default)
            .Where(g => g.Key != null)
            .Select(g => ((INamedTypeSymbol)g.Key!, g.Select(x => x)));

        var collectedTasks = new List<TaskModel>();

        // task invoking implementations
        foreach (var (type, tasks) in groupedTasks)
        {
            var sb = new StringBuilder();

            // file header
            sb.AppendCommonHeader();
            sb.AppendLine();

            // containing type
            sb.AppendTypeHeader(out var indent, type);
            var indentStr = new string(' ', indent * 4);

            // invoking methods implementation
            sb.Append(indentStr).AppendLine("public static class FlowTasks");
            sb.Append(indentStr).AppendLine("{");

            foreach (var task in tasks)
            {
                var methodName = task.Method.Name;
                sb.Append(indentStr).Append("    public static IFlowTask ").Append(methodName)
                    .Append(" { get; } = new ").Append(methodName).AppendLine("_FlowTask();");
                collectedTasks.Add(task);
                var method = task.Method;
                sb.Append(indentStr).Append("    private sealed class ").Append(methodName).AppendLine("_FlowTask : IFlowTask");
                sb.Append(indentStr).AppendLine("    {");
                sb.Append(indentStr).Append("        ").AppendGeneratedCodeAttribute();
                sb.Append(indentStr).Append("        ").AppendExcludeFromCodeCoverageAttribute();
                var isAwaitable = method.IsAwaitable();
                var hasReturn = isAwaitable
                    ? method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 }
                    : !method.ReturnsVoid;
                var param = method.Parameters;
                var hasInvokingInfo = false;
                if (param.Length > 0 && param[0].GetAttributes().Any(a =>
                        a.AttributeClass?.GetSimplifiedTypeName() == Constants.FlowInvokingInfoAttribute))
                {
                    hasInvokingInfo = true;
                    param = param.Length > 1 ? param.Slice(1, param.Length - 1) : [];
                }
                sb.Append(indentStr).Append("        public async Task<TReturn> Invoke<TReturn, TArgument>" +
                    "(TArgument argument, FlowTaskInvokingInfo ").Append(hasInvokingInfo ? "invokingInfo)" : "_)");
                sb.Append(indentStr).AppendLine("        {");
                sb.Append(indentStr).Append("            if (argument is not ");
                if (param.Length == 0) sb.AppendLine("None)");
                else
                {
                    if (param.Length == 1) sb.Append(param[0].Type.GetFullyQualifiedName());
                    else
                    {
                        sb.Append("ValueTuple<");
                        sb.Append(param[0].Type.GetFullyQualifiedName());
                        foreach (var p in param.Skip(1))
                            sb.Append(", ").Append(p.Type.GetFullyQualifiedName());
                        sb.Append(">");
                    }
                    sb.AppendLine(" arg)");
                }
                sb.Append(indentStr).AppendLine("                throw new InvalidCastException(\"Argument type mismatch.\");");
                sb.Append(indentStr).Append("            ");
                if (hasReturn) sb.Append("var result = ");
                sb.Append("await ");
                if (!isAwaitable) sb.Append("Task.Run(() => ");
                sb.Append(method.GetQualifiedSymbolName()).Append("(");
                if (hasInvokingInfo) sb.Append("invokingInfo");
                if (param.Length > 0)
                {
                    if (hasInvokingInfo) sb.Append(", ");
                    if (param.Length == 1) sb.Append("arg");
                    else
                    {
                        sb.Append("arg.Item1");
                        for (var i = 2; i <= param.Length; i++) sb.Append(", arg.Item").Append(i);
                    }
                }
                sb.Append(")");
                if (!isAwaitable) sb.Append(").ConfigureAwait(false)");
                sb.AppendLine(";");
                if (hasReturn)
                {
                    sb.Append(indentStr).AppendLine("            if (result is not TReturn typedResult)");
                    sb.Append(indentStr).AppendLine("                throw new InvalidCastException(\"Return type mismatch.\");");
                }
                sb.Append(indentStr).Append("            return ").Append(hasReturn ? "typedResult" : "default!").AppendLine(";");
                sb.Append(indentStr).AppendLine("        }");
                sb.Append(indentStr).AppendLine("    }");
                sb.AppendLine();
            }

            sb.Remove(sb.Length - 1, 1);
            sb.Append(indentStr).AppendLine("}");

            // containing type end
            while (--indent >= 0) sb.Append(new string(' ', indent * 4)).AppendLine("}");

            // register source
            spc.AddSource($"{type.GetFullyQualifiedName()}+FlowTasks.g.cs", sb.ToString());
        }

        // global identifier map
        {
            var sb = new StringBuilder();

            // header
            sb.AppendCommonHeader();
            sb.AppendLine();
            sb.Append("namespace ").Append(Constants.FlowCoreNamespace).AppendLine(";");
            sb.AppendLine();
            sb.AppendLine("partial class FlowInterops");
            sb.AppendLine("{");
            sb.Append("    ").AppendGeneratedCodeAttribute();
            sb.Append("    ").AppendExcludeFromCodeCoverageAttribute();
            sb.AppendLine("    private static async Task RegisterFlowTasks()");
            sb.AppendLine("    {");

            foreach (var task in collectedTasks) foreach (var identifier in task.GlobalIdentifiers)
            {
                sb.Append("        Flow.Internal.RegisterTask(\"")
                  .Append(identifier)
                  .Append("\", ")
                  .Append(task.Method.ContainingType.GetFullyQualifiedName())
                  .Append(".FlowTasks.")
                  .Append(task.Method.Name);
                foreach (var run in task.AutoRuns)
                {
                    sb.AppendLine(",");
                    sb.Append("            (")
                      .Append(run.Before.ToPrimitive()).Append(", ")
                      .Append(run.After.ToPrimitive()).Append(", ")
                      .Append(run.Priority).Append(')');
                }
                sb.AppendLine(");");
            }

            // tail
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // register source
            spc.AddSource("FlowInterops.RegisterFlowTasks.g.cs", sb.ToString());
        }
    }
}
