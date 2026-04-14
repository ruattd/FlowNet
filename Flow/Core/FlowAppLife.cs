namespace Flow.Core;

partial class Flow
{
}

[Flow.Scope("app-life")]
public sealed partial class AppLife
{
    [Flow.Task(Before = "app:*")]
    public static void Start()
    {
    }

    [Flow.Task(After = "app:exit")]
    public static void Stop()
    {
    }
}
