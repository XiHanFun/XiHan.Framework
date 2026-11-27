// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Kernel.Validation;

namespace XiHan.Framework.Kernel.Tests.Validation;

/// <summary>
/// Validate 阶段的内置中间件
/// </summary>
/// <remarks>
/// 按请求 Schema 拒绝载荷、按 <c>IValidatableObject</c> 拒绝请求对象、合法请求进 Handle、类型化调用同样校验、关闭内置中间件时不校验、非操作调用透传
/// </remarks>
public sealed class ValidationMiddlewareTests
{
    /// <summary>
    /// 回声请求的 Schema：文本非空、重复次数 1 到 5
    /// </summary>
    private static readonly OperationSchema _schema = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"EchoRequest","type":"object","properties":{"text":{"type":["string","null"],"minLength":1},"repeat":{"type":"integer","minimum":1,"maximum":5}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"EchoResponse","type":"object","properties":{"echo":{"type":"string"},"traceId":{"type":"string"}},"required":["echo","traceId"]}""");

    /// <summary>
    /// 构建并启动装配了带 Schema 的回声特性的应用
    /// </summary>
    private static async Task<(ServiceProvider Provider, App App)> StartAsync(bool builtIn = true)
    {
        var (provider, app) = Hosts.BuildApp(
            b => b.UseFeature(new EchoFeature(schema: _schema)),
            Hosts.Configuration(("XiHan:Kernel:UseBuiltInMiddleware", builtIn ? "true" : "false")));
        await app.StartAsync(CancellationToken.None);
        return (provider, app);
    }

    /// <summary>
    /// UTF-8 字节
    /// </summary>
    private static ReadOnlyMemory<byte> Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void BuiltInMiddleware_IncludesValidateStage()
    {
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new EchoFeature()));
        using var _ = provider;

        var dump = app.DescribePipeline();
        Assert.Contains("validate     XiHan.Framework.Kernel.Validation.ValidationMiddleware", dump, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_Json_WithSchemaViolations_IsRejectedBeforeHandle()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var context = Hosts.Context(scope.ServiceProvider);
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var response = await invoker.InvokeAsync(context, new OperationRequest(EchoOperation.Id, Utf8("""{"text":"","repeat":9}""")));

        Assert.Equal(KernelErrors.SchemaViolation, response.Error?.Code);
        Assert.Equal(EchoOperation.Id, response.Error!.Details!["id"]);
        Assert.Equal("$.text: length must be >= 1", response.Error.Details["reason"]);
        Assert.Equal(["$.text: length must be >= 1", "$.repeat: value must be <= 5"], (IEnumerable<string>)response.Error.Details["violations"]!);
        // errors 保留违规对象：宿主据此按字段键分组并按调用方区域翻译
        var errors = (IReadOnlyList<SchemaViolation>)response.Error.Details["errors"]!;
        Assert.Equal(["text", "repeat"], errors.Select(static e => e.Field));
        Assert.Equal([KernelMessages.ValidationMinLength, KernelMessages.ValidationMaximum], errors.Select(static e => e.MessageKey));
        // 没进 Handle：没有响应对象，捕获中间件（Authorize 阶段）仍看到了调用
        Assert.False(context.FindOperationResponse().HasValue);
        Assert.True(response.Payload.IsEmpty);
        Assert.Equal([EchoOperation.Id], provider.GetRequiredService<OperationCapture>().Seen);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Json_ValidPayload_IsDecodedAtValidate_AndHandled()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var context = Hosts.Context(scope.ServiceProvider);
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var response = await invoker.InvokeAsync(context, new OperationRequest(EchoOperation.Id, Utf8("""{"text":"hi","repeat":2}""")));

        Assert.True(response.IsSuccess, response.Error?.ToString());
        using var document = JsonDocument.Parse(response.Payload);
        Assert.Equal("hihi", document.RootElement.GetProperty("echo").GetString());
        Assert.Equal("hi", ((EchoRequest)context.FindOperationRequest().ValueOrThrow()).Text);
        Assert.True(context.FindOperationPayload().HasValue);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Json_NotJson_IsInvalidPayload_AtValidate()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var response = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("not json")));

        Assert.Equal(KernelErrors.InvalidOperationPayload, response.Error?.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_WithSelfValidatingRequest_IsRejected_ForJsonAndTypedCalls()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var raw = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("""{"text":"invalid"}""")));
        var typed = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("invalid"));

        Assert.Equal(KernelErrors.RequestInvalid, raw.Error?.Code);
        // 成员名按 camelCase 转成序列化名；应用给的文案没有消息键，原样进原因
        Assert.Equal(["$.text: The text is not allowed."], (IEnumerable<string>)raw.Error!.Details!["violations"]!);
        var violation = Assert.Single((IReadOnlyList<SchemaViolation>)raw.Error.Details["errors"]!);
        Assert.Equal("text", violation.Field);
        Assert.Null(violation.MessageKey);
        Assert.Equal(KernelErrors.RequestInvalid, typed.Error?.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Typed_IsValidatedAgainstSchema_ByEncodingOnce()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var rejected = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("hi", Repeat: 0));
        var accepted = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("hi", Repeat: 3));

        Assert.Equal(KernelErrors.SchemaViolation, rejected.Error?.Code);
        Assert.Equal("hihihi", accepted.ValueOrThrow().Echo);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Typed_WithALoneSurrogate_IsInvalidPayload_LikeTheSameRawPayload()
    {
        // 为 Schema 校验编出的请求严格编码（RFC-0011「校验」，ADR-0320）：修之前写成 U+FFFD，Schema 校验的是另一份请求，操作照常执行。
        // 结果与原始调用里同样的请求相同：载荷不合法，reason 是 $
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var typed = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("hi\uD800"));
        var raw = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("""{"text":"hi\ud800"}""")));
        var paired = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("\uD83D\uDE00\uFFFD"));

        Assert.Equal((KernelErrors.InvalidOperationPayload, "$"), (typed.Error?.Code, typed.Error?.Details?["reason"]));
        Assert.Equal((raw.Error?.Code, raw.Error?.Details?["reason"]), (typed.Error?.Code, typed.Error?.Details?["reason"]));
        Assert.Equal("\uD83D\uDE00\uFFFD", paired.ValueOrThrow().Echo);
        await app.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public async Task Invoke_Typed_WithANonFiniteFloat_IsInvalidPayload_LikeTheSameRawPayload(double ratio, string literal)
    {
        // 浮点成员编不成（RFC-0011「校验」，ADR-0329）：修之前 System.Text.Json 抛出的异常让 Validate 阶段按未处理的异常结束（500）。
        // 结果与原始调用里同样的内容相同：JSON 没有这样的数，载荷不合法，reason 是 $
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new MeasureFeature()));
        await app.StartAsync(CancellationToken.None);
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var typed = await invoker.InvokeAsync<MeasureRequest, MeasureRequest>(Hosts.Context(scope.ServiceProvider), MeasureOperation.Id, new MeasureRequest(ratio));
        var raw = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(MeasureOperation.Id, Utf8($$"""{"ratio":{{literal}}}""")));

        Assert.Equal((KernelErrors.InvalidOperationPayload, "$"), (raw.Error?.Code, raw.Error?.Details?["reason"]));
        Assert.Equal((raw.Error?.Code, raw.Error?.Details?["reason"]), (typed.Error?.Code, typed.Error?.Details?["reason"]));
        await app.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task Invoke_Typed_AtTheDepthLimit_IsEncoded_LikeTheSameRawPayloadIsRead(int levels, bool readable)
    {
        // 64 层、最里层有值（RFC-0011「校验」，ADR-0329）：原始调用读得进，修之前类型化调用编码时 System.Text.Json 在第 64 层写不出、按未处理的异常结束。
        // 65 层两种调用都是载荷不合法，reason 是 $
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new MeasureFeature()));
        await app.StartAsync(CancellationToken.None);
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        // 根是第 1 层，nest 起的对象链占其余各层
        var typed = await invoker.InvokeAsync<MeasureRequest, MeasureRequest>(Hosts.Context(scope.ServiceProvider), MeasureOperation.Id, new MeasureRequest(2, DepthNode.Make(levels - 1)));
        var text = """{"nest":""" + string.Concat(Enumerable.Repeat("""{"next":""", levels - 2)) + """{"value":1}""" + new string('}', levels - 1);
        var raw = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(MeasureOperation.Id, Utf8(text)));

        if (readable)
        {
            Assert.True(raw.IsSuccess, raw.Error?.ToString());
            Assert.Equal(2, typed.ValueOrThrow().Ratio);
        }
        else
        {
            Assert.Equal((KernelErrors.InvalidOperationPayload, "$"), (raw.Error?.Code, raw.Error?.Details?["reason"]));
            Assert.Equal((raw.Error?.Code, raw.Error?.Details?["reason"]), (typed.Error?.Code, typed.Error?.Details?["reason"]));
        }

        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Typed_WithASerializationOnlyContext_IsEncodedForTheSchema()
    {
        // 只生成快速路径的上下文先照快速路径编码请求（ADR-0342）：修之前为 Schema 校验编码时向解析器为选项副本重新取元数据，给不出即按未处理的异常结束（500）
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new MeasureFeature(SerializationOnlyJsonContext.Default.MeasureRequest)));
        await app.StartAsync(CancellationToken.None);
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var typed = await invoker.InvokeAsync<MeasureRequest, MeasureRequest>(Hosts.Context(scope.ServiceProvider), MeasureOperation.Id, new MeasureRequest(2, DepthNode.Make(3), "x"));
        var nan = await invoker.InvokeAsync<MeasureRequest, MeasureRequest>(Hosts.Context(scope.ServiceProvider), MeasureOperation.Id, new MeasureRequest(double.NaN));

        Assert.Equal(2, typed.ValueOrThrow().Ratio);
        Assert.Equal((KernelErrors.InvalidOperationPayload, "$"), (nan.Error?.Code, nan.Error?.Details?["reason"]));
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Typed_WithoutSchema_IsNotEncoded()
    {
        // 编码只为 Schema 校验：没有 Schema 的类型化调用不经 JSON，带孤立代理项的请求照常交给操作
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new EchoFeature()));
        await app.StartAsync(CancellationToken.None);
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var typed = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("hi\uD800"));

        Assert.Equal("hi\uD800", typed.ValueOrThrow().Echo);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WithoutBuiltInMiddleware_NoValidation_AndHandleDecodes()
    {
        var (provider, app) = await StartAsync(builtIn: false);
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        // Schema 会拒绝 repeat: 9，但没有内置中间件就直接进 Handle，由操作自己处理
        var response = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("""{"text":"a","repeat":9}""")));

        Assert.True(response.IsSuccess, response.Error?.ToString());
        Assert.DoesNotContain("ValidationMiddleware", app.DescribePipeline(), StringComparison.Ordinal);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task NonOperationInvocation_PassesThrough()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var reached = false;

        var outcome = await app.InvokeAsync(Hosts.Context(scope.ServiceProvider), _ =>
        {
            reached = true;
            return ValueTask.CompletedTask;
        });

        Assert.True(outcome.IsSuccess);
        Assert.True(reached);
        await app.StopAsync(CancellationToken.None);
    }
}
