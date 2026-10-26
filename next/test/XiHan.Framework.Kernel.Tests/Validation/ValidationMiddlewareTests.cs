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
/// Validate 阶段的内置中间件：按请求 Schema 拒绝载荷、按 <c>IValidatableObject</c> 拒绝请求对象、合法请求进 Handle、类型化调用同样校验、关闭内置中间件时不校验、非操作调用透传。
/// </summary>
public sealed class ValidationMiddlewareTests
{
    /// <summary>
    /// 回声请求的 Schema：文本非空、重复次数 1 到 5。
    /// </summary>
    private static readonly OperationSchema _schema = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"EchoRequest","type":"object","properties":{"text":{"type":["string","null"],"minLength":1},"repeat":{"type":"integer","minimum":1,"maximum":5}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"EchoResponse","type":"object","properties":{"echo":{"type":"string"},"traceId":{"type":"string"}},"required":["echo","traceId"]}""");

    /// <summary>
    /// 构建并启动装配了带 Schema 的回声特性的应用。
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
    /// UTF-8 字节。
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
        Assert.False(context.GetOperationResponse().HasValue);
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
        Assert.Equal("hi", ((EchoRequest)context.GetOperationRequest().ValueOrThrow()).Text);
        Assert.True(context.GetOperationPayload().HasValue);
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
