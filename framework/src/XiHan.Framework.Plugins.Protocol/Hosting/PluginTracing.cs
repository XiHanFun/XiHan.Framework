// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 宿主调用插件时的 Span <c>xihan.plugin.invoke</c>
/// </summary>
/// <remarks>
/// <para>进程外与 Wasm 宿主共用（<c>spec/plugin/invocation.md</c> §6，ADR-0269）：代理操作的调用与事件投递各记一条，经内核的 ActivitySource（<see cref="FrameworkDiagnostics.SourceName"/>）创建，种类是 Client。</para>
/// <para>父级按 RFC-0003 规则 7 取（<see cref="FrameworkDiagnostics.StartActivity"/>）：管道里有 Observe 阶段的 <c>xihan.invoke</c> 时是它的子 Span。载体在这条 Span 之内组装调用的元数据，写给插件的 <c>trace_parent</c> 于是取它的 span-id 与采样位，插件一侧的 Span 挂在它下面。</para>
/// <para>覆盖宿主一侧的整个调用：等插件可用（懒激活、Wasm 的排队）与调用本身。插件不可用、调用失败时 Span 仍在，状态是 Error。没有监听器时直接转调，不进异步状态机、不分配。</para>
/// </remarks>
internal static class PluginTracing
{
    /// <summary>
    /// Span 名
    /// </summary>
    public const string ActivityName = "xihan.plugin.invoke";

    /// <summary>
    /// 标签：插件的清单 ID
    /// </summary>
    public const string PluginIdTag = "xihan.plugin.id";

    /// <summary>
    /// 调用插件的一个操作
    /// </summary>
    /// <remarks>
    /// 标签 <c>xihan.plugin.id</c> 与 <c>xihan.operation</c>（代理操作的 ID，来自登记表）
    /// </remarks>
    /// <param name="invoker">载体的运行时</param>
    /// <param name="pluginId">插件的清单 ID</param>
    /// <param name="operation">操作 ID</param>
    /// <param name="payload">请求载荷</param>
    /// <param name="context">调用上下文</param>
    public static ValueTask<Result<object>> InvokeAsync(IPluginInvoker invoker, string pluginId, string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(context);

        // 没人听就不开 Span：直接转调，省掉一层异步状态机
        return FrameworkDiagnostics.ActivitySource.HasListeners()
            ? TracedInvokeAsync(invoker, pluginId, operation, payload, context)
            : invoker.InvokeAsync(operation, payload, context);
    }

    /// <summary>
    /// 把一条消息投递给插件
    /// </summary>
    /// <remarks>
    /// 标签只有 <c>xihan.plugin.id</c>：投递不是操作调用，没有操作 ID；主题不进标签
    /// </remarks>
    /// <param name="runtime">载体的运行时</param>
    /// <param name="delivery">投递</param>
    /// <param name="context">调用上下文</param>
    public static ValueTask<Result> DeliverAsync(IPluginRuntime runtime, MessageDelivery delivery, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(context);
        return FrameworkDiagnostics.ActivitySource.HasListeners()
            ? TracedDeliverAsync(runtime, delivery, context)
            : runtime.DeliverAsync(delivery, context);
    }

    /// <summary>
    /// 在 Span 里调用操作
    /// </summary>
    private static async ValueTask<Result<object>> TracedInvokeAsync(IPluginInvoker invoker, string pluginId, string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
    {
        using var activity = FrameworkDiagnostics.StartActivity(context, ActivityName, ActivityKind.Client, [new(PluginIdTag, pluginId), new(DiagnosticTags.Operation, operation)]);
        try
        {
            var result = await invoker.InvokeAsync(operation, payload, context).ConfigureAwait(false);
            Complete(activity, result.Error);
            return result;
        }
        catch (Exception exception)
        {
            Fault(activity, exception);
            throw;
        }
    }

    /// <summary>
    /// 在 Span 里投递
    /// </summary>
    private static async ValueTask<Result> TracedDeliverAsync(IPluginRuntime runtime, MessageDelivery delivery, InvocationContext context)
    {
        using var activity = FrameworkDiagnostics.StartActivity(context, ActivityName, ActivityKind.Client, [new(PluginIdTag, runtime.Manifest.Id)]);
        try
        {
            var result = await runtime.DeliverAsync(delivery, context).ConfigureAwait(false);
            Complete(activity, result.Error);
            return result;
        }
        catch (Exception exception)
        {
            Fault(activity, exception);
            throw;
        }
    }

    /// <summary>
    /// 按结果给 Span 定状态
    /// </summary>
    /// <remarks>
    /// 失败时状态为 Error、说明是错误码，与 <c>xihan.invoke</c> 一致；成功时状态留空
    /// </remarks>
    private static void Complete(Activity? activity, Error? error)
    {
        if (error is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, error.Code);
        }
    }

    /// <summary>
    /// 调用抛出时给 Span 定状态
    /// </summary>
    /// <remarks>
    /// 取消或载体的故障：状态为 Error，说明只写异常类型。异常消息不进 Span：它可能带着路径与地址。取消最后算超时还是调用方取消，由 Resilience 阶段归因后记在外层的 <c>xihan.invoke</c> 上
    /// </remarks>
    private static void Fault(Activity? activity, Exception exception)
        => activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().FullName);
}
