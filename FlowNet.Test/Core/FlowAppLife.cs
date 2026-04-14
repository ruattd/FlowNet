namespace FlowNet.Core;

[Flow.Scope("app-life")]
public sealed partial class AppLife
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
