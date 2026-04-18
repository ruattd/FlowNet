namespace FlowNet.Core;

partial class Flow
{
    public class ExtensionContext(string globalIdentifier) : ScopeContext(globalIdentifier)
    {
    }

    partial class Internal
    {
        public static ExtensionContext CreateExtension(string globalIdentifier) => new(globalIdentifier);
    }
}
