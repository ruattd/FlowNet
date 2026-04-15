using FlowNet.Core;

namespace FlowNet.Test.Core;

[Flow.Scope("app-life")]
public static partial class AppLife
{
    [Flow.Scope("events")]
    public sealed partial class Events
    {
        [Flow.Task]
        public static void Tap()
        {
        }
    }

    [Flow.Task]
    [Flow.Run(Before = "app:*")]
    public static int Start(Events e, Events e1)
    {
        return 0;
    }

    [Flow.Task]
    private static void _Test()
    {
        Flow.InvokeTask<int>("app:start");
    }
}
