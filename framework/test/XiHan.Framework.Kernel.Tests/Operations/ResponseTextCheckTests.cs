// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 类型化的响应写成载荷：整段文本要过 JSON 文本的共同检查
/// </summary>
/// <remarks>
/// RFC-0011 规则 10 的「编码」与 RFC-0001「写出与转交」（ADR-0319）：嵌套超过 64 层（层数从响应的根算起，原样嵌进去的文本也算）或任何一层有重复成员即写不出，XGI 入口以 <c>KERNEL_UNEXPECTED_001</c> + <c>kernel.invocation.unhandled_exception</c> 结束，不带 <c>inner</c>，没有载荷。
/// 修之前 System.Text.Json 写不出 64 层、最里层有值的响应，却照写序列化上下文放宽深度后的更多层，也不核对重复成员与原样嵌进去的文本
/// </remarks>
public sealed class ResponseTextCheckTests
{
    /// <summary>
    /// 写不出的形状
    /// </summary>
    /// <remarks>
    /// 超过 64 层、重复成员的六种写法与原样嵌进去的孤立代理项转义，见 <see cref="CheckedOperation.Render"/>
    /// </remarks>
    public static TheoryData<string> Unwritable => new() { "depth-65", "raw-depth", "extension", "dictionary-policy", "element", "node", "converter", "raw-duplicate", "raw-escape" };

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
        return await provider.GetRequiredService<IOperationInvoker>().InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(CheckedOperation.Id, payload));
    }

    /// <summary>
    /// 用给定选项的序列化上下文建描述
    /// </summary>
    private static OperationDescriptor<CheckedOperation, EchoRequest, CheckedResponse> DescriptorOf(JsonSerializerOptions options)
        => new(new(CheckedOperation.Id), "xihan.test.texts", EchoJsonContext.Default.EchoRequest, new CheckedJsonContext(options).CheckedResponse);

    /// <summary>
    /// 写出时是否失败
    /// </summary>
    /// <remarks>
    /// 写不出只抛 <see cref="ArgumentException"/>：超过写出深度时 System.Text.Json 的 <see cref="JsonException"/> 也包在里面（ADR-0320）
    /// </remarks>
    private static bool Fails(OperationDescriptor descriptor, CheckedResponse response)
        => Record.Exception(() => descriptor.EncodeResponse(response)) is { } exception && exception.GetType() == typeof(ArgumentException);

    [Theory]
    [MemberData(nameof(Unwritable))]
    public async Task InvokeAsync_AResponseTextFailingTheCheck_EndsAsUnhandledException_WithoutInner(string shape)
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
    public async Task InvokeAsync_SixtyFourLevelsWithAValueInside_AreWritten()
    {
        // 第 64 层的值照常写出，与读取一侧读得进的层数相同；写出的文本过检查，按描述解码回来仍是 64 层
        await using var provider = await StartAsync();

        var response = await InvokeAsync(provider, "depth-64");

        Assert.True(response.IsSuccess, response.Outcome.ToString());
        Assert.False(JsonText.FindProblem(response.Payload.Span).HasValue);
        var descriptor = provider.GetRequiredService<OperationRegistry>().Find(CheckedOperation.Id).ValueOrThrow();
        var levels = 1;
        for (var node = ((CheckedResponse)descriptor.DecodeResponse(response.Payload).ValueOrThrow()).Nest; node is not null; node = node.Next)
        {
            levels++;
        }

        Assert.Equal(64, levels);
    }

    [Fact]
    public async Task InvokeAsync_AValidResponse_IsWrittenAsTheSerializerWrites()
    {
        // 元素、节点、按命名策略改写的字典键、原样嵌进去的文本与扩展数据都合法时，字节与 System.Text.Json 写出的相同
        await using var provider = await StartAsync();

        var response = await InvokeAsync(provider, "valid");

        Assert.True(response.IsSuccess, response.Outcome.ToString());
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(CheckedOperation.Render("valid"), CheckedJsonContext.Default.CheckedResponse), response.Payload.ToArray());
    }

    [Fact]
    public void EncodeResponse_CountsLevelsFromTheRoot_WhenTheContextAllowsDeeper()
    {
        // 序列化上下文的深度上限放宽到 128，写出仍到 64 层为止；原样嵌进去的文本同样从响应的根算起
        var descriptor = DescriptorOf(new JsonSerializerOptions { MaxDepth = 128 });

        Assert.False(Fails(descriptor, CheckedOperation.Render("depth-64")));
        Assert.True(Fails(descriptor, CheckedOperation.Render("depth-65")));
        Assert.True(Fails(descriptor, new CheckedResponse { Nest = DepthNode.Make(100) }));
        Assert.True(Fails(descriptor, CheckedOperation.Render("raw-depth")));
    }

    [Fact]
    public void EncodeResponse_TooDeep_ThrowsOnlyArgumentException_WithTheSerializerExceptionInside()
    {
        // 修之前超过写出深度时抛的是 System.Text.Json 的 JsonException，其余写不出是 ArgumentException：直接调这个 Stable 方法的代码要接两种（ADR-0320）
        var descriptor = DescriptorOf(new JsonSerializerOptions());

        var deeper = Assert.Throws<ArgumentException>(() => descriptor.EncodeResponse(CheckedOperation.Render("depth-65")));
        var deepest = Assert.Throws<ArgumentException>(() => descriptor.EncodeResponse(new CheckedResponse { Nest = DepthNode.Make(100) }));

        Assert.True(deeper.InnerException is null or JsonException, deeper.InnerException?.GetType().Name);
        Assert.IsType<JsonException>(deepest.InnerException);
    }

    [Fact]
    public void EncodeResponse_KeepsAShallowerContextMaxDepth()
    {
        // 序列化上下文把深度上限设得比 64 小时照它：与按同一个上下文读请求时的层数相同
        var descriptor = DescriptorOf(new JsonSerializerOptions { MaxDepth = 8 });

        Assert.False(Fails(descriptor, new CheckedResponse { Nest = DepthNode.Make(7) }));
        Assert.True(Fails(descriptor, new CheckedResponse { Nest = DepthNode.Make(8) }));
    }
}
