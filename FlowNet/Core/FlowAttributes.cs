using System;
using System.Threading.Tasks;

namespace FlowNet.Core;

partial class Flow
{
    /// <summary>
    /// 标记一个 <see langword="partial"/> 类/接口为 Flow 作用域，将自动生成作用域上下文及相关代码。
    /// </summary>
    /// <param name="identifier">标识符</param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class ScopeAttribute(string identifier) : Attribute;

    /// <summary>
    /// 标记一个 <see langword="partial"/> 类中的 <see langword="static"/> 方法为 Flow
    /// 任务，将自动生成相关调用代码。<br/>
    /// 若该方法位于 Flow 作用域内，该任务将自动加入该作用域。<br/>
    /// <b>NOTE</b>: 由于语言特性的限制，最多支持 7 个参数的方法，若有更多则会导致生成的代码无法编译或运行时行为不正确。
    /// </summary>
    /// <param name="identifier">标识符，若为空则自动由方法名生成，生成时将忽略单下划线前缀</param>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TaskAttribute(string? identifier = null) : Attribute;

    /// <summary>
    /// 为已标记的 Flow 任务配置自动执行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RunAttribute : Attribute
    {
        /// <summary>
        /// 标记该任务在指定锚点<b>之前</b>执行，支持通配符和正则表达式
        /// </summary>
        public string? Before { get; init; }

        /// <summary>
        /// 标记该任务在指定锚点<b>之后</b>执行，支持通配符和正则表达式
        /// </summary>
        public string? After { get; init; }
    }
}

/// <summary>
/// 标记一个标记类，指定该类为 Flow 扩展标记，并指定扩展生成代码的入口点。
/// <p>入口点方法需为静态异步方法，接受与扩展标记完全相同的参数，并返回 <see cref="Task"/>
/// 值。生成的初始化代码将在 <c>FlowNet.Core.FlowInterops</c> 类的 <c>InitializeExtensions</c>
/// 方法中调用该入口点，该类为 <see langword="partial"/> 类。</p>
/// <p>建议的做法是将 <see langword="private"/> 的入口点方法生成在 <see langword="partial"/>
/// 标记的同名类中，同时由于所有扩展的入口点可能都在此类，该方法应尽可能避免重名。</p>
/// </summary>
/// <param name="entryPoint">入口点，可调用的方法全名</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FlowExtensionUsageAttribute(string entryPoint) : Attribute;
