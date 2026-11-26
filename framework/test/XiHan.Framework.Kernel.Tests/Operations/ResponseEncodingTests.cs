// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 类型化的响应写成载荷：写不成合法的 JSON 文本即写不出
/// </summary>
/// <remarks>
/// RFC-0011 规则 10 的「编码」与 RFC-0001「写出与转交」（ADR-0318）：响应里的字符串有孤立的代理项时，XGI 入口以 <c>KERNEL_UNEXPECTED_001</c> + <c>kernel.invocation.unhandled_exception</c> 结束，不带 <c>inner</c>，没有载荷；
/// 修之前 System.Text.Json 把它写成转义的 U+FFFD，调用照常成功，调用方拿到的是另一个值。合法的响应写出的字节与以前相同
/// </remarks>
public sealed class ResponseEncodingTests
{
    /// <summary>
    /// 写不出的形状：孤立的代理项在值、字典的键与数组元素里
    /// </summary>
    public static TheoryData<string> Unwritable => new() { "value-high", "value-low", "reversed", "key", "item" };

    /// <summary>
    /// 启动只装配 <see cref="TextsFeature"/> 的应用
    /// </summary>
    private static async Task<ServiceProvider> StartAsync()
    {
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new TextsFeature()));
        await app.StartAsync(CancellationToken.None);
        return provider;
    }

    /// <summary>
    /// 经 XGI 入口按形状调用
    /// </summary>
    private static async Task<OperationResponse> InvokeAsync(ServiceProvider provider, string shape)
    {
        await using var scope = provider.CreateAsyncScope();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new EchoRequest(shape), EchoJsonContext.Default.EchoRequest);
        return await provider.GetRequiredService<IOperationInvoker>().InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(TextsOperation.Id, payload));
    }

    [Theory]
    [MemberData(nameof(Unwritable))]
    public async Task InvokeAsync_AResponseStringWithALoneSurrogate_EndsAsUnhandledException_WithoutInner(string shape)
    {
        await using var provider = await StartAsync();

        var response = await InvokeAsync(provider, shape);

        Assert.True(response.Outcome.IsFailure, shape);
        var error = response.Outcome.Error!;
        Assert.Equal((KernelErrors.Unexpected, KernelMessages.UnhandledException), (error.Code, error.MessageKey));
        Assert.Null(error.Inner);
        Assert.True(error.Details is null || error.Details.Count == 0);
        Assert.True(response.Payload.IsEmpty);
    }

    [Fact]
    public async Task InvokeAsync_AValidResponse_IsWrittenAsBefore()
    {
        // 代理对、本来就是 U+FFFD 的字符、HTML 敏感字符、引号、反斜杠与控制字符照常写出，字节与 System.Text.Json 缺省写出的相同
        await using var provider = await StartAsync();

        var response = await InvokeAsync(provider, "valid");

        Assert.True(response.IsSuccess, response.Outcome.ToString());
        var expected = JsonSerializer.SerializeToUtf8Bytes(TextsOperation.Render("valid"), TextsJsonContext.Default.TextsResponse);
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(response.Payload.Span));
        Assert.Equal(expected, response.Payload.ToArray());
        Assert.Equal(TextsOperation.Render("valid").Text, JsonSerializer.Deserialize(response.Payload.Span, TextsJsonContext.Default.TextsResponse)!.Text);
    }

    [Theory]
    [MemberData(nameof(Unwritable))]
    public void EncodeResponse_RefusesALoneSurrogate_AndFollowsTheSerializerOptions(string shape)
    {
        // 序列化上下文的选项（这里是缩进）照旧生效：严格的写出只多一道核对
        var indented = new TextsJsonContext(new JsonSerializerOptions { WriteIndented = true });
        var descriptor = new OperationDescriptor<TextsOperation, EchoRequest, TextsResponse>(new(TextsOperation.Id), "xihan.test.texts", EchoJsonContext.Default.EchoRequest, indented.TextsResponse);
        var valid = TextsOperation.Render("valid");

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(valid, indented.TextsResponse), descriptor.EncodeResponse(valid).ToArray());
        Assert.Throws<ArgumentException>(() => descriptor.EncodeResponse(TextsOperation.Render(shape)));
    }

    [Fact]
    public async Task TypedInvokeAsync_DoesNotWriteJson_AndHandsTheResponseOverAsIs()
    {
        // 类型化入口不经 JSON，交出的是处理器返回的对象：这一条只管写成载荷的那一处
        await using var provider = await StartAsync();
        await using var scope = provider.CreateAsyncScope();

        var result = await provider.GetRequiredService<IOperationInvoker>().InvokeAsync<EchoRequest, TextsResponse>(Hosts.Context(scope.ServiceProvider), TextsOperation.Id, new EchoRequest("value-high"));

        Assert.Equal("a" + TextsOperation.HighSurrogate, result.ValueOrThrow().Text);
    }
}
