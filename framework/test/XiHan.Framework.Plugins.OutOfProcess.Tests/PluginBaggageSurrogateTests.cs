// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主写给进程外插件的元数据里的 Baggage 值不经宽松编码
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §2（ADR-0334）：上下文里只有幂等键可能带着孤立的代理项，没有 Idempotency 阶段的宿主不核对它；gRPC 载体的 protobuf 编码会把它换成 U+FFFD，两个不同的幂等键到了插件是同一个。宿主只装配了进程外插件特性，没有 Idempotency 阶段
/// </remarks>
public sealed class PluginBaggageSurrogateTests
{
    [Fact]
    public async Task AnIdempotencyKeyWithALoneSurrogate_FailsTheCall_AndThePluginKeepsServing()
    {
        await using var host = await EchoPluginHost.StartAsync();

        foreach (var key in new[] { "k\uD800", "\uDC00k", "k\uDE00\uD83D" })
        {
            // 修之前 protobuf 把它写成 U+FFFD 交给插件，插件照常应答
            var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"ping"}""", new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = key });

            var error = response.Outcome.Error;
            Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
            Assert.Equal(
                (EchoPluginHost.PluginId, "echoes.create", "baggage 'xihan.idempotency-key' has a lone surrogate and cannot be encoded as UTF-8"),
                (error!.Details!["id"], error.Details["phase"], error.Details["reason"]));
            Assert.True(response.Payload.IsEmpty);
        }

        // 成对的代理项是平面以外的一个字符，照常交给插件；宿主一侧的失败不碰进程，插件一直在服务
        var paired = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"ping"}""", new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = "k😀" });
        Assert.True(paired.Outcome.IsSuccess, paired.Outcome.ToString());
        var status = EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal((LifecycleState.Ready, 0), (status.Status.State, status.Restarts));
    }
}
