// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// 插件贡献的一个中间件（<c>spec/plugin/wasm.md</c> §6）：进入下游前后各调一次 <c>xihan_middleware</c>；before 可短路或改写已登记 Baggage，after 只观察；任何失败都关闭调用。
/// </summary>
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
            // 只合并已登记的键；主体不可改写
            if (BaggageKeys.IsRegistered(key) && key != BaggageKeys.Principal)
            {
                downstream = downstream.WithBaggage(key, value);
            }
        }

        await nextStep(downstream).ConfigureAwait(false);

        // after 只观察：请求已取消或超时也要让插件看到结果，因此不带调用的取消令牌（仍受中间件时限约束）
        var outcome = downstream.GetOutcome().TryGetValue(out var result) ? result : Result.Success();
        var after = await runtime.MiddlewareAsync(name, stage, "after", downstream, outcome, CancellationToken.None).ConfigureAwait(false);
        if (after.IsFailure && outcome.IsSuccess)
        {
            // 失败即关闭；下游已经失败时保留原因，不用钩子的失败覆盖
            context.SetOutcome(Result.Failure(after.Error!));
        }
    }
}
