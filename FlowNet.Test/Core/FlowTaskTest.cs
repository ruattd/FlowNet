using System;
using System.Threading.Tasks;
using FlowNet.Core;

namespace FlowNet.Test.Core;

[TestClass]
public class FlowTaskTest
{
    [TestMethod]
    public async Task TaskInvoke()
    {
        await FlowInterops.Initialize("app:test:test2");
    }
}

[Flow.Scope("app:test")]
public static partial class AppTest
{
    [Flow.Task]
    public static int Test() => 114514;

    [Flow.Task]
    public static async Task Test2()
    {
        var r = await Context.InvokeTask<int>("test");
        Console.WriteLine(r);
    }
}
