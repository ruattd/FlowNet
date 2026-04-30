using System;
using System.Threading.Tasks;
using FlowNet.Core;

namespace FlowNet.Test.Core;

[Flow.Scope("app")]
public static partial class AppLife
{
    [Flow.Task("run")] [Flow.Run(Before = "app:loading")]
    [Flow.Task("loading")] [Flow.Run(Before = "app:running")]
    [Flow.Task("running")] [Flow.Run(Before = "app:exiting")]
    [Flow.Task("exiting")] [Flow.Run(Before = "app:exit")]
    [Flow.Task("exit")]
    public static Task Wildcard([Flow.InvokingInfo] FlowTaskInvokingInfo info)
    {
        Console.WriteLine($"Invoking wildcard: {info}");
        return Task.CompletedTask;
    }

    [Flow.Task]
    public static int Test([Flow.InvokingInfo] FlowTaskInvokingInfo info, int i)
    {
        Console.WriteLine(info.ToString());
        return i;
    }
}
