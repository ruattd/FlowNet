# Flow.NET

面向现代 .NET 应用程序的轻量级生命周期与流程控制框架。

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

*WORKING IN PROGRESS*
