// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主核对插件交回的应答载荷
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §1 与 RFC-0001「写出与转交」（ADR-0315）：<c>status.ok</c> 为真而载荷不为空时先过「JSON 文本」的共同检查，没过即 <c>PLUGINS_UNEXPECTED_001</c>，载荷不交给协议适配器与调用方。
/// 前半用假运行时核对两种宿主共用的代理那一处；后半用真实的 EchoPlugin 子进程端到端核对：插件经 <c>echoes.relay</c> 把宿主操作的应答原样当作自己的应答，宿主操作写出的是一份有重复成员的 JSON
/// </remarks>
[Collection(nameof(TelemetryGroup))]
public sealed class PluginResponsePayloadTests
{
    /// <summary>
    /// 假插件的清单 ID
    /// </summary>
    private const string PluginId = "xihan.test.alpha";

    /// <summary>
    /// 假插件的操作
    /// </summary>
    private const string Operation = "alphas.run";

    /// <summary>
    /// 没过检查的应答与 <c>reason</c> 里的路径
    /// </summary>
    public static TheoryData<string, byte[], string> InvalidPayloads => new()
    {
        { "syntax", "{\"a\":"u8.ToArray(), "$" },
        { "comment", "{\"a\":1 /* x */}"u8.ToArray(), "$" },
        { "two roots", "{} {}"u8.ToArray(), "$" },
        { "nesting", Encoding.ASCII.GetBytes(new string('[', 65) + new string(']', 65)), "$" },
        { "invalid utf-8", [.. "{\"a\":\""u8, 0xC3, .. "\"}"u8], "$" },
        { "utf-8 encoded surrogate", [.. "{\"a\":\""u8, 0xED, 0xA0, 0x80, .. "\"}"u8], "$" },
        { "escaped lone surrogate", "{\"a\":\"\\ud800\"}"u8.ToArray(), "$" },
        { "duplicate member", "{\"a\":1,\"a\":2}"u8.ToArray(), "$.a" },
        { "nested duplicate member", "{\"items\":[{\"id\":1,\"id\":2}]}"u8.ToArray(), "$.items[0].id" },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task AResponse_ThatIsNotValidJsonText_IsAPluginFailure(string name, byte[] payload, string path)
    {
        var (proxy, _) = Proxy(payload);

        var result = await proxy.ExecuteAsync(TestInvocationContext.Create(), new PluginPayload("{}"u8.ToArray()));

        Assert.True(result.IsFailure, name);
        Assert.Equal(PluginErrors.Unexpected, result.Error!.Code);
        Assert.Equal(PluginMessages.ProcessFailed, result.Error.MessageKey);
        Assert.Equal(PluginId, result.Error.Details!["id"]);
        Assert.Equal(Operation, result.Error.Details["phase"]);
        Assert.Equal($"the response payload is not valid JSON text at '{path}'", result.Error.Details["reason"]);
    }

    [Fact]
    public async Task AResponse_ThatIsValidJsonText_IsPassedOnUnchanged()
    {
        // 多字节文本、代理对与转义写出的代理对照常交回，字节不变
        byte[] valid = [.. "{\"text\":\"订单 😀\",\"pair\":\"\\ud83d\\ude00\",\"list\":[1,2.5,null,true]}"u8];
        var (proxy, _) = Proxy(valid);

        var result = await proxy.ExecuteAsync(TestInvocationContext.Create(), new PluginPayload("{}"u8.ToArray()));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(valid, Assert.IsType<PluginPayload>(result.ValueOrThrow()).Bytes.ToArray());
    }

    [Fact]
    public async Task AnEmptyResponse_IsPassedOnAsBefore()
    {
        // 空的载荷不做这道检查：协议适配器按没有响应体处理
        var (proxy, _) = Proxy([]);

        var result = await proxy.ExecuteAsync(TestInvocationContext.Create(), new PluginPayload("{}"u8.ToArray()));

        Assert.True(result.IsSuccess, result.ToString());
        Assert.True(Assert.IsType<PluginPayload>(result.ValueOrThrow()).Bytes.IsEmpty);
    }

    [Fact]
    public async Task APluginFailure_IsReturnedAsTheRuntimeGaveIt()
    {
        // 插件报的失败不经这道检查：它没有载荷
        var failure = Error.Validation("ALPHAS_VALIDATION_001", "alphas.name.empty");
        var runtime = new StubPluginRuntime(PluginId, Operation) { OnInvoke = (_, _) => ValueTask.FromResult(Result.Failure<object>(failure)) };
        var proxy = new ProxyOperationDescriptor(new OperationContract(Operation), PluginId, runtime, null);

        var result = await proxy.ExecuteAsync(TestInvocationContext.Create(), new PluginPayload("{}"u8.ToArray()));

        Assert.Same(failure, result.Error);
    }

    [Fact]
    public async Task TheCheck_IsInsideThePluginInvokeSpan()
    {
        // 宿主一侧的结果是失败：xihan.plugin.invoke 的状态是 Error，说明是错误码（invocation.md §6）
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = "00f067aa0ba902b7", TraceFlags = ActivityTraceFlags.Recorded }, CancellationToken.None);
        var (proxy, _) = Proxy("{\"a\":"u8.ToArray());

        Assert.True((await proxy.ExecuteAsync(context, new PluginPayload("{}"u8.ToArray()))).IsFailure);

        var span = Assert.Single(recorder.Spans(context.TraceId, "xihan.plugin.invoke"));
        Assert.Equal((ActivityStatusCode.Error, PluginErrors.Unexpected), (span.Status, span.StatusDescription));
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void ARecordedResponse_ThatIsNotValidJsonText_CannotBeReplayed(string name, byte[] payload, string path)
    {
        // 幂等回放还原记录的应答时做同一道检查：解码失败，由 Idempotency 阶段报 CACHING_CONFLICT_003
        var (proxy, _) = Proxy([]);

        var decoded = proxy.DecodeResponse(payload);

        Assert.True(decoded.IsFailure, name);
        Assert.Equal(KernelErrors.InvalidOperationPayload, decoded.Error!.Code);
        Assert.Equal(KernelMessages.InvalidOperationPayload, decoded.Error.MessageKey);
        Assert.Equal((Operation, path), (decoded.Error.Details!["id"], decoded.Error.Details["reason"]));
    }

    [Fact]
    public void ARecordedResponse_ThatIsValidJsonText_IsReplayed()
    {
        var (proxy, _) = Proxy([]);

        Assert.Equal("{\"a\":1}"u8.ToArray(), Assert.IsType<PluginPayload>(proxy.DecodeResponse("{\"a\":1}"u8.ToArray()).ValueOrThrow()).Bytes.ToArray());
        Assert.True(Assert.IsType<PluginPayload>(proxy.DecodeResponse(ReadOnlyMemory<byte>.Empty).ValueOrThrow()).Bytes.IsEmpty);
    }

    [Fact]
    public async Task EndToEnd_ARelayedResponseWithADuplicateMember_FailsTheCall_AndThePluginKeepsServing()
    {
        // 真实的子进程：插件把宿主操作写出的 {"a":1,"a":2} 原样当作自己的应答。修之前宿主原样交回，调用方拿到的是重复成员的 JSON
        await using var host = await EchoPluginHost.StartAsync();
        var registered = host.Services.GetRequiredService<OperationRegistry>().AddRange("xihan.test.raw", [new RawResponseOperation("raws.create", "{\"a\":1,\"a\":2}"u8.ToArray())], []);
        Assert.True(registered.IsSuccess, registered.ToString());

        var relayed = await EchoPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"raws.create","payload":{}}""");

        Assert.Equal(PluginErrors.Unexpected, relayed.Outcome.Error?.Code);
        Assert.Equal(PluginMessages.ProcessFailed, relayed.Outcome.Error!.MessageKey);
        Assert.Equal((EchoPluginHost.PluginId, "echoes.relay", "the response payload is not valid JSON text at '$.a'"), (relayed.Outcome.Error.Details!["id"], relayed.Outcome.Error.Details["phase"], relayed.Outcome.Error.Details["reason"]));
        Assert.True(relayed.Payload.IsEmpty);

        // 不按崩溃处理：进程不重启，同一个插件照常应答
        var status = EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal((LifecycleState.Ready, 0), (status.Status.State, status.Restarts));
        var echoed = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"again"}""");
        Assert.True(echoed.Outcome.IsSuccess, echoed.Outcome.ToString());
    }

    /// <summary>
    /// 只带一个操作的假插件，调用交回给定的载荷
    /// </summary>
    private static (ProxyOperationDescriptor Proxy, StubPluginRuntime Runtime) Proxy(byte[] response)
    {
        var runtime = new StubPluginRuntime(PluginId, Operation)
        {
            OnInvoke = (_, _) => ValueTask.FromResult(Result.Success<object>(new PluginPayload(response))),
        };
        return (new ProxyOperationDescriptor(new OperationContract(Operation), PluginId, runtime, null), runtime);
    }
}
