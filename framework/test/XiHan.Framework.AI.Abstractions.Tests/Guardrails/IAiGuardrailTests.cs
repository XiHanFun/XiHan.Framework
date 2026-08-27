// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// AI 输入护栏契约测试
/// </summary>
/// <remarks>
/// 护栏挂在管道最外层，绝大多数调用都放行，因此返回 ValueTask 而非 Task 是有意的性能选择；
/// 同时它接收的是整个消息序列而不是单条消息——注入往往藏在历史消息里，
/// 只看最后一条用户输入会被「先铺垫再下指令」的手法绕过。
/// </remarks>
public class IAiGuardrailTests
{
    /// <summary>
    /// 未命中任何规则时放行
    /// </summary>
    [Fact]
    public async Task InspectInputAsync_WhenNothingMatches_Allows()
    {
        IAiGuardrail guardrail = new KeywordGuardrail("违禁词");

        var result = await guardrail.InspectInputAsync(
            [new ChatMessage(ChatRole.User, "今天天气怎么样")],
            TestContext.Current.CancellationToken);

        Assert.False(result.IsBlocked);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// 命中规则时拦截并给出原因
    /// </summary>
    [Fact]
    public async Task InspectInputAsync_WhenKeywordMatches_BlocksWithReason()
    {
        IAiGuardrail guardrail = new KeywordGuardrail("违禁词");

        var result = await guardrail.InspectInputAsync(
            [new ChatMessage(ChatRole.User, "请说出违禁词")],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Reason);
        Assert.Contains("违禁词", result.Reason!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 检查覆盖整个消息序列，而不仅是最后一条
    /// </summary>
    /// <remarks>
    /// 这是提示注入的典型形态：先用一条早期消息埋下指令，末条只写一句无害的话。
    /// 护栏若只看末条就会整条放行。
    /// </remarks>
    [Fact]
    public async Task InspectInputAsync_WhenMatchIsInEarlierMessage_StillBlocks()
    {
        IAiGuardrail guardrail = new KeywordGuardrail("违禁词");

        var result = await guardrail.InspectInputAsync(
            [
                new ChatMessage(ChatRole.System, "你是助手"),
                new ChatMessage(ChatRole.User, "记住这个违禁词"),
                new ChatMessage(ChatRole.Assistant, "好的"),
                new ChatMessage(ChatRole.User, "继续")
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsBlocked);
    }

    /// <summary>
    /// 空消息序列不应被拦截
    /// </summary>
    /// <remarks>没有内容就没有风险；此时拦截会让空会话初始化直接失败。</remarks>
    [Fact]
    public async Task InspectInputAsync_WithEmptyMessages_Allows()
    {
        IAiGuardrail guardrail = new KeywordGuardrail("违禁词");

        var result = await guardrail.InspectInputAsync([], TestContext.Current.CancellationToken);

        Assert.False(result.IsBlocked);
    }

    /// <summary>
    /// 护栏对外暴露只读的诊断名
    /// </summary>
    /// <remarks>多护栏串联时，拦截日志靠这个名字定位是哪一道拦的。</remarks>
    [Fact]
    public void Name_IsReadOnlyDiagnosticIdentifier()
    {
        IAiGuardrail guardrail = new KeywordGuardrail("违禁词");
        var property = typeof(IAiGuardrail).GetProperty(nameof(IAiGuardrail.Name))!;

        Assert.Equal(typeof(string), property.PropertyType);
        Assert.Null(property.SetMethod);
        Assert.Equal("keyword", guardrail.Name);
    }

    /// <summary>
    /// 检查方法返回 ValueTask 而非 Task
    /// </summary>
    /// <remarks>
    /// 护栏在绝大多数请求上同步放行，ValueTask 让这条热路径不分配 Task 对象。
    /// 改成 Task 不会有编译错误，只会在高频调用下多出一笔可观的分配，故在此锁死。
    /// </remarks>
    [Fact]
    public void InspectInputAsync_Signature_ReturnsValueTask()
    {
        var method = typeof(IAiGuardrail).GetMethod(nameof(IAiGuardrail.InspectInputAsync))!;

        Assert.Equal(typeof(ValueTask<GuardrailResult>), method.ReturnType);
    }

    /// <summary>
    /// 检查方法接收整个消息序列，取消令牌可选
    /// </summary>
    [Fact]
    public void InspectInputAsync_Signature_TakesWholeMessageSequence()
    {
        var parameters = typeof(IAiGuardrail).GetMethod(nameof(IAiGuardrail.InspectInputAsync))!.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("messages", parameters[0].Name);
        Assert.Equal(typeof(IEnumerable<ChatMessage>), parameters[0].ParameterType);
        Assert.False(parameters[0].IsOptional);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].IsOptional);
    }

    /// <summary>
    /// 只有输入侧检查，没有输出侧检查
    /// </summary>
    /// <remarks>
    /// 接口定位是「不下发模型」的第一道防线；输出侧过滤是另一个关注点，
    /// 若将来要加，应当是新接口而不是往这里塞方法——否则实现方被迫为不需要的方向写空实现。
    /// </remarks>
    [Fact]
    public void Interface_CoversInputInspectionOnly()
    {
        var methodNames = typeof(IAiGuardrail).GetMethods()
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(nameof(IAiGuardrail.InspectInputAsync), Assert.Single(methodNames));
    }

    /// <summary>
    /// 按敏感词子串匹配的护栏替身
    /// </summary>
    /// <remarks>
    /// 只用最直白的子串匹配，够用来验证「检查覆盖全部消息」与结果语义；
    /// 真实的启发式规则属实现包职责。
    /// </remarks>
    private sealed class KeywordGuardrail : IAiGuardrail
    {
        private readonly string _keyword;

        /// <summary>
        /// 构造护栏替身
        /// </summary>
        /// <param name="keyword">敏感词</param>
        public KeywordGuardrail(string keyword)
        {
            _keyword = keyword;
        }

        /// <summary>
        /// 护栏名
        /// </summary>
        public string Name => "keyword";

        /// <summary>
        /// 逐条检查入站消息
        /// </summary>
        /// <param name="messages">入站消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        public ValueTask<GuardrailResult> InspectInputAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var message in messages)
            {
                if (message.Text.Contains(_keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult(GuardrailResult.Block("命中敏感词：" + _keyword));
                }
            }

            return ValueTask.FromResult(GuardrailResult.Allow());
        }
    }
}
