// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using XiHan.Framework.AI.Abstractions.Chat;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// AI 会话服务门面契约测试
/// </summary>
/// <remarks>
/// 抽象包里没有实现，这里用手写 fake 承接接口，验证两件真实契约：
/// 一是可选参数的默认值（编译进每个调用点，改动属静默破坏性变更），
/// 二是流式方法返回 IAsyncEnumerable 而非 Task&lt;IEnumerable&gt;——后者会让「流式」退化成一次性返回。
/// </remarks>
public class IXiHanAiServiceTests
{
    /// <summary>
    /// 省略选项时实现侧收到的是 null，即「用默认 provider」
    /// </summary>
    [Fact]
    public async Task ChatAsync_WhenOptionsOmitted_PassesNullToImplementation()
    {
        var service = new RecordingAiService();

        var response = await service.ChatAsync(
            [new ChatMessage(ChatRole.User, "你好")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(service.LastOptions);
        Assert.Equal("你好", response.Text);
    }

    /// <summary>
    /// 传入的会话选项原样抵达实现侧
    /// </summary>
    [Fact]
    public async Task ChatAsync_WhenOptionsProvided_PassesSameInstance()
    {
        var service = new RecordingAiService();
        var options = new XiHanChatOptions
        {
            Provider = "openai",
            ChatOptions = new ChatOptions { ModelId = "gpt-4o-mini" }
        };

        await service.ChatAsync(
            [new ChatMessage(ChatRole.User, "你好")],
            options,
            TestContext.Current.CancellationToken);

        Assert.Same(options, service.LastOptions);
        Assert.Equal("openai", service.LastOptions!.Provider);
    }

    /// <summary>
    /// 入站消息按原顺序抵达实现侧
    /// </summary>
    /// <remarks>系统提示必须排在用户提问之前，顺序错乱会直接改变模型行为。</remarks>
    [Fact]
    public async Task ChatAsync_WithMultipleMessages_PreservesOrder()
    {
        var service = new RecordingAiService();

        await service.ChatAsync(
            [
                new ChatMessage(ChatRole.System, "你是助手"),
                new ChatMessage(ChatRole.User, "第一问"),
                new ChatMessage(ChatRole.Assistant, "第一答"),
                new ChatMessage(ChatRole.User, "第二问")
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(4, service.LastMessages.Count);
        Assert.Equal(ChatRole.System, service.LastMessages[0].Role);
        Assert.Equal("第一问", service.LastMessages[1].Text);
        Assert.Equal(ChatRole.Assistant, service.LastMessages[2].Role);
        Assert.Equal("第二问", service.LastMessages[3].Text);
    }

    /// <summary>
    /// 流式对话逐块产出，拼接后等于完整回答
    /// </summary>
    [Fact]
    public async Task ChatStreamAsync_WhenEnumerated_YieldsIncrementalUpdates()
    {
        var service = new RecordingAiService();
        var chunks = new List<string>();

        await foreach (var update in service.ChatStreamAsync(
            [new ChatMessage(ChatRole.User, "讲个故事")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(update.Text);
        }

        Assert.Equal(RecordingAiService.StreamPieces.Length, chunks.Count);
        Assert.Equal(string.Concat(RecordingAiService.StreamPieces), string.Concat(chunks));
    }

    /// <summary>
    /// 流式对话在令牌已取消时抛出取消异常
    /// </summary>
    /// <remarks>
    /// 长回答的中途取消是常态（用户点停止/连接断开），令牌必须一路传到产出循环里。
    /// 断言用 ThrowsAny，因为取消可能表现为 OperationCanceledException 或其派生的 TaskCanceledException。
    /// </remarks>
    [Fact]
    public async Task ChatStreamAsync_WhenTokenAlreadyCanceled_Throws()
    {
        var service = new RecordingAiService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in service.ChatStreamAsync(
                [new ChatMessage(ChatRole.User, "讲个故事")],
                cancellationToken: cts.Token))
            {
                _ = update;
            }
        });
    }

    /// <summary>
    /// 一次性对话在令牌已取消时抛出取消异常
    /// </summary>
    [Fact]
    public async Task ChatAsync_WhenTokenAlreadyCanceled_Throws()
    {
        var service = new RecordingAiService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.ChatAsync([new ChatMessage(ChatRole.User, "你好")], cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 一次性对话的可选参数默认值锁定
    /// </summary>
    /// <remarks>默认值编译进调用点，改动不会让既有代码报错，只会让运行期行为悄悄变化。</remarks>
    [Fact]
    public void ChatAsync_Signature_HasStableOptionalDefaults()
    {
        var parameters = typeof(IXiHanAiService).GetMethod(nameof(IXiHanAiService.ChatAsync))!.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(IEnumerable<ChatMessage>), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal(typeof(XiHanChatOptions), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.Null(parameters[1].DefaultValue);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].IsOptional);
    }

    /// <summary>
    /// 流式对话返回 IAsyncEnumerable，逐块产出而非一次性返回
    /// </summary>
    [Fact]
    public void ChatStreamAsync_Signature_ReturnsAsyncStream()
    {
        var method = typeof(IXiHanAiService).GetMethod(nameof(IXiHanAiService.ChatStreamAsync))!;

        Assert.Equal(typeof(IAsyncEnumerable<ChatResponseUpdate>), method.ReturnType);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(IEnumerable<ChatMessage>), parameters[0].ParameterType);
        Assert.True(parameters[1].IsOptional);
        Assert.True(parameters[2].IsOptional);
    }

    /// <summary>
    /// 一次性对话返回 Task&lt;ChatResponse&gt;，直接复用原生响应类型
    /// </summary>
    /// <remarks>不包一层 XiHan 自有响应类型，是「薄封装」这条设计声明的可验证形态。</remarks>
    [Fact]
    public void ChatAsync_Signature_ReturnsNativeChatResponse()
    {
        var method = typeof(IXiHanAiService).GetMethod(nameof(IXiHanAiService.ChatAsync))!;

        Assert.Equal(typeof(Task<ChatResponse>), method.ReturnType);
    }

    /// <summary>
    /// 记录入参并回放固定回答的会话服务替身
    /// </summary>
    private sealed class RecordingAiService : IXiHanAiService
    {
        /// <summary>
        /// 流式回放使用的分片
        /// </summary>
        public static readonly string[] StreamPieces = ["从前", "有座", "山。"];

        /// <summary>
        /// 最近一次收到的入站消息
        /// </summary>
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        /// <summary>
        /// 最近一次收到的会话选项
        /// </summary>
        public XiHanChatOptions? LastOptions { get; private set; }

        /// <summary>
        /// 回放最后一条入站消息的文本
        /// </summary>
        /// <param name="messages">入站消息</param>
        /// <param name="options">会话选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<ChatResponse> ChatAsync(
            IEnumerable<ChatMessage> messages,
            XiHanChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToList();
            LastOptions = options;

            var echo = LastMessages.Count == 0 ? string.Empty : LastMessages[^1].Text;

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, echo)));
        }

        /// <summary>
        /// 按固定分片逐块回放
        /// </summary>
        /// <param name="messages">入站消息</param>
        /// <param name="options">会话选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async IAsyncEnumerable<ChatResponseUpdate> ChatStreamAsync(
            IEnumerable<ChatMessage> messages,
            XiHanChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            LastOptions = options;

            foreach (var piece in StreamPieces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                yield return new ChatResponseUpdate(ChatRole.Assistant, piece);
            }
        }
    }
}
