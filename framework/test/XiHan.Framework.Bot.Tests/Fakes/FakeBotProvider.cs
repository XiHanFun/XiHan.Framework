// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// 手写的 <see cref="IBotProvider"/> 替身
/// </summary>
/// <remarks>
/// 全部行为在进程内完成，绝不发起任何网络请求；通过 <see cref="CallCount"/> 观察被调用次数。
/// </remarks>
public sealed class FakeBotProvider : IBotProvider
{
    private readonly Func<int, BotResult> _resultFactory;

    /// <summary>
    /// 创建替身
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="resultFactory">按第几次调用（从 1 开始）产出结果；返回 null 表示抛出异常</param>
    public FakeBotProvider(string name, Func<int, BotResult> resultFactory)
    {
        Name = name;
        _resultFactory = resultFactory;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 最后一次收到的消息
    /// </summary>
    public BotMessage? LastMessage { get; private set; }

    /// <summary>
    /// 最后一次收到的上下文
    /// </summary>
    public BotContext? LastContext { get; private set; }

    /// <summary>
    /// 总是成功的提供者
    /// </summary>
    /// <param name="name">提供者名称</param>
    public static FakeBotProvider AlwaysSuccess(string name)
    {
        return new FakeBotProvider(name, _ => BotResult.Success(provider: name));
    }

    /// <summary>
    /// 总是失败的提供者
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="errorMessage">失败说明</param>
    public static FakeBotProvider AlwaysFailed(string name, string errorMessage = "failed")
    {
        return new FakeBotProvider(name, _ => BotResult.Failed(errorMessage, name));
    }

    /// <summary>
    /// 总是抛异常的提供者
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="errorMessage">异常消息</param>
    public static FakeBotProvider AlwaysThrows(string name, string errorMessage = "boom")
    {
        return new FakeBotProvider(name, _ => throw new InvalidOperationException(errorMessage));
    }

    /// <summary>
    /// 前若干次失败、之后成功的提供者
    /// </summary>
    /// <param name="name">提供者名称</param>
    /// <param name="failTimes">失败次数</param>
    public static FakeBotProvider FailsThenSucceeds(string name, int failTimes)
    {
        return new FakeBotProvider(
            name,
            attempt => attempt <= failTimes
                ? BotResult.Failed($"attempt-{attempt}", name)
                : BotResult.Success(provider: name));
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="context">调度上下文</param>
    public Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        CallCount++;
        LastMessage = message;
        LastContext = context;
        return Task.FromResult(_resultFactory(CallCount));
    }
}
