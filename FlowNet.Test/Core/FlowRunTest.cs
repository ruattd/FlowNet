using System;
using System.Threading.Tasks;
using FlowNet.Core;

namespace FlowNet.Test.Core;

[TestClass]
public sealed partial class FlowRunTest
{
    [Flow.Task]
    [Flow.Run(After = "app:run", Before = "app:loading")]
    [Flow.Run(After = "app:run", Before = "app:loading")]
    private static void _TestTask1()
    {
        Console.WriteLine("Task1 invoked");
    }

    [Flow.Task]
    [Flow.Run(After = "app:loading")]
    private static void _TestTask2()
    {
        Console.WriteLine("Task2 invoked");
    }

    [Flow.Task]
    [Flow.Run(After = "app:exiting")]
    private static void _TestTask3()
    {
        Console.WriteLine("Task3 invoked");
    }

    [Flow.Task]
    [Flow.Run(Before = "app:exiting")]
    private static void _TestTask4()
    {
        Console.WriteLine("Task4 invoked");
    }

    [TestMethod]
    public async Task Test()
    {
        Console.WriteLine("Running flow:run test...");
        Flow.EnableTaskInvokingInfo = true;
        await FlowInterops.Run();
    }
}
