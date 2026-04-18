using System.Text;
using FlowNet.CodeAnalysis.Shared;
using Microsoft.CodeAnalysis;

namespace FlowNet.CodeAnalysis.SourceGenerators;

[Generator]
public class FlowInteropGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(_GenerateFlowInteropsClass);
    }

    private static void _GenerateFlowInteropsClass(IncrementalGeneratorPostInitializationContext context)
    {
        var sb = new StringBuilder();

        // header
        sb.AppendCommonHeader();
        sb.AppendLine();
        sb.Append("namespace ").Append(Constants.FlowCoreNamespace).AppendLine(";");
        sb.AppendLine();
        sb.AppendLine("internal static partial class FlowInterops");
        sb.AppendLine("{");

        // initialize method
        sb.Append("    ").AppendGeneratedCodeAttribute();
        sb.Append("    ").AppendExcludeFromCodeCoverageAttribute();
        sb.AppendLine("    public static async Task Initialize(string? startPoint = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        await RegisterFlowTasks().ConfigureAwait(false);");
        sb.AppendLine("        await InitializeExtensions().ConfigureAwait(false);");
        sb.AppendLine("        if (startPoint != null) await Flow.InvokeTask(startPoint);");
        sb.AppendLine("    }");

        // tail
        sb.AppendLine("}");

        // register source
        context.AddSource("FlowInterops.g.cs", sb.ToString());
    }
}
