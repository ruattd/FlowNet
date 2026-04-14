using FlowNet.Core;

namespace FlowNet.Test.Core;

[Flow.Scope("app-life")]
public static partial class AppLife
{
    [Flow.Task]
    [Flow.Run(Before = "app:*")]
    public static void Start()
    {
    }

    [Flow.Task]
    [Flow.Run(After = "app:exit")]
    public static void Stop()
    {
    }
}
