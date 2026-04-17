# Flow.NET

面向现代 .NET 应用程序的轻量级生命周期与流程控制框架。

## 快速开始

添加依赖：

```shell
dotnet add package FlowNet
```

添加代码：

```csharp
using FlowNet.Core;

namespace YourApp.Namespace;

[Flow.Scope("app")]
public sealed partial class Program
{
    public static Task Main() => FlowInterops.Initialize("app:start");

    [Flow.Task]
    public static async Task Start() 
    {
        // do what you want
    }
}
```

然后就可以开始写业务逻辑了——是的，就这么简单，简单到连 template 都不用。

*WORKING IN PROGRESS*
