using System;
using System.Threading.Tasks;
using FlowNet.Core;

namespace FlowNet.Test.Core;

[Flow.Scope("app")]
public static partial class AppLife
{
    [Flow.Task]
    public static int Start()
    {
        return 0;
    }

    [Flow.Task]
    public static int Start1(int i)
    {
        return i;
    }

    [Flow.Task]
    private static async Task _Test()
    {
        var r = await Flow.InvokeTask<int, int>("app:start", 123);
        Console.WriteLine($"Output: {r}");
    }
}
