# Flow.NET

面向现代 .NET 应用程序的轻量级生命周期与流程控制框架。

*[WORKING IN PROGRESS]*

[![NuGet Version](https://img.shields.io/nuget/v/FlowNet.Core?style=flat-square)](https://www.nuget.org/packages/FlowNet.Core)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ruattd/FlowNet/ci.yml?style=flat-square&label=ci)](https://github.com/ruattd/FlowNet/actions/workflows/ci.yml)
[![GitHub top language](https://img.shields.io/github/languages/top/ruattd/FlowNet?style=flat-square)](https://github.com/ruattd/FlowNet)

## 快速开始

添加依赖：

```shell
dotnet add package FlowNet.Core
```

添加代码：

```csharp
using FlowNet.Core;
using System.Threading.Tasks;

namespace YourApp.Namespace;

[Flow.Scope("app")]
public sealed partial class Program
{
    // Start application by Flow.NET
    public static Task Main() => FlowInterops.Initialize("app:start");

    [Flow.Task]
    public static async Task Start() 
    {
        // 这个 Task 的全局标识是 app:start, 其中 start 是从方法名推断出来的
        // 若要自定义标识, 可以使用 [Flow.Task("custom")]
        // do something...
    }
}
```

然后就可以开始写业务逻辑了——就这么简单，简单到连 template 都不用。

## 基于流程的开发

将 Program 类改成这样：

```csharp
using FlowNet.Core;
using System.Threading.Tasks;

namespace YourApp.Namespace;

[Flow.Scope("app")]
internal static partial class Program
{
    // Run application based on flow control
    public static Task Main() => FlowInterops.Run();

    // Define base flows
    [Flow.Task]
    [Flow.Task("loading")]
    [Flow.Task("exit")]
    private static Task Wildcard() => Task.CompletedTask;
}
```

然后使用 `Flow.Run` 标记来定义各流程间的依赖关系，例如：

```csharp
using FlowNet.Core;
using System.Threading.Tasks;

namespace YourApp.Namespace;

[Flow.Scope("config")]
public sealed partial class ConfigService
{
    [Flow.Task]
    [Flow.Run(After = "app:loading", Before = "*:start")]
    private static async Task _()
    {
        // 这是一个特殊的 Flow 任务，按照一般的标识符推断规则，它的标识符是空的
        // 因此这个任务的全局标识就是外部 scope 的标识，即 config
        // Initialize config service...
    }
}
```
