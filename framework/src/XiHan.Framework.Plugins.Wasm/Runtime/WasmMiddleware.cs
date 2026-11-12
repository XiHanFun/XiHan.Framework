// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Wasm.Runtime;

/// <summary>
/// 插件贡献的一个中间件（<c>spec/plugin/wasm.md</c> §6）
/// </summary>
/// <remarks>
/// 进入下游前后各调一次 <c>xihan_middleware</c>；before 可短路或改写已登记 Baggage，after 只观察；任何失败都关闭调用。实例绑定一个运行时：插件卸载时按归属撤出管道，再加载登记的是绑定新运行时的新实例；撤出之前已经取到管道的调用仍经过它，插件停止后以 <c>PLUGINS_UNAVAILABLE_001</c> 结束（ADR-0088）
/// </remarks>
internal sealed class WasmMiddleware(WasmPluginRuntime runtime, string name, PipelineStage stage) : IPipelineMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        var before = await runtime.MiddlewareAsync(name, stage, "before", context, null, context.CancellationToken).ConfigureAwait(false);
        if (!before.TryGetValue(out var decision))
        {
            // 失败即关闭：插件不可用或钩子出错时不放行
            context.SetOutcome(Result.Failure(before.Error!));
            return;
        }

        if (decision.Action == MiddlewareAction.ShortCircuit)
        {
            context.SetOutcome(decision.Outcome is { Ok: false, Error: { } error }
                ? Result.Failure(PluginErrorMapper.FromProto(error, runtime.Manifest.Id))
                : Result.Failure(Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", name), ("reason", "a short-circuiting middleware must return a failure outcome")))));
            return;
        }

        var downstream = context;
        foreach (var (key, value) in decision.Baggage)
        {
            // 只合并已登记的键；主体与租户不可改写：插件不能替调用选租户，Tenant 阶段之前的中间件改写了它就能把调用放进任意启用的租户（ADR-0195）
            if (BaggageKeys.IsRegistered(key) && key != BaggageKeys.Principal && key != BaggageKeys.Tenant)
            {
                downstream = downstream.WithBaggage(key, value);
            }
        }

        await nextStep(downstream).ConfigureAwait(false);

        // after 只观察：请求已取消或超时也要让插件看到结果，因此不带调用的取消令牌（仍受中间件时限约束）
        var outcome = downstream.FindOutcome().TryGetValue(out var result) ? result : Result.Success();
        var after = await runtime.MiddlewareAsync(name, stage, "after", downstream, outcome, CancellationToken.None).ConfigureAwait(false);
        if (after.IsFailure && outcome.IsSuccess)
        {
            // 失败即关闭；下游已经失败时保留原因，不用钩子的失败覆盖
            context.SetOutcome(Result.Failure(after.Error!));
        }
    }
}
