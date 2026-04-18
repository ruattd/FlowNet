using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlowNet.CodeAnalysis.SourceGenerators;

[Generator]
public class FlowExtensionInitGenerator : IIncrementalGenerator
{
    private readonly record struct FlowExtensionInfo(
        string EntryPoint,
        ImmutableArray<TypedConstant> Parameters
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attrs = context.CompilationProvider
            .Select((compilation, _) => compilation.Assembly.GetAttributes()
                .Select(extAttr =>
                {
                    var usageAttr = extAttr.AttributeClass?.GetAttributes().FirstOrDefault(a1 =>
                        a1.AttributeClass?.GetSimplifiedTypeName() == Constants.FlowExtensionUsageAttribute);
                    var entryPoint = usageAttr?.ConstructorArguments[0].Value?.ToString();
                    return entryPoint == null ? default : new FlowExtensionInfo(entryPoint, extAttr.ConstructorArguments);
                })
                .Where(x => x != default)
            );

        context.RegisterSourceOutput(attrs, _GenerateExtensionInitMethod);
    }

    private static void _GenerateExtensionInitMethod(SourceProductionContext context,
        IEnumerable<FlowExtensionInfo> extensionInfos)
    {
        var sb = new StringBuilder();

        // header
        sb.AppendCommonHeader();
        sb.AppendLine();
        sb.AppendLine("namespace FlowNet.Core;");
        sb.AppendLine();
        sb.AppendLine("partial class FlowInterops");
        sb.AppendLine("{");
        sb.Append("    ").AppendGeneratedCodeAttribute();
        sb.Append("    ").AppendExcludeFromCodeCoverageAttribute();
        sb.AppendLine("    private static async Task InitializeExtensions()");
        sb.AppendLine("    {");

        foreach (var info in extensionInfos)
        {
            sb.Append("        await ").Append(info.EntryPoint).Append('(');
            var it = info.Parameters.GetEnumerator();
            if (it.MoveNext())
            {
                sb.Append(it.Current.ToCSharpString());
                while (it.MoveNext()) sb.Append(", ").Append(it.Current.ToCSharpString());
            }
            sb.AppendLine(").ConfigureAwait(false);");
        }

        // tail
        sb.AppendLine("    }");
        sb.AppendLine("}");

        // add source
        context.AddSource("FlowInterops.InitializeExtensions.g.cs", sb.ToString());
    }
}
