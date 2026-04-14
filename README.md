# Flow.NET

面向现代 .NET 应用程序的轻量级生命周期与流程控制框架。

一步到位构建应用程序：

```csharp
using FlowNet.Core;

[Flow.Scope("app")]
public sealed partial class Program
{
    public static Task Main() => Flow.Run("app:start");

    [Flow.Task]
    public static async Task Start() 
    {
        // do what you want
    }
}
```

*WORKING IN PROGRESS*
