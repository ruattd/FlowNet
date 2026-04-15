using System.Threading.Tasks;
using FlowNet.Core;

namespace FlowNet.Test.Core;

[TestClass]
public class FlowTaskTest
{
    [TestMethod]
    public async Task TaskInvoke()
    {
        await FlowInterops.Initialize("app:test");
    }
}
