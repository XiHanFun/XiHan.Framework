// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// 测试处理器共享记录器
/// </summary>
/// <remarks>
/// 平台把处理器注册成瞬态，路由器每次都从作用域重新解析实例，
/// 因此调用痕迹不能存在处理器自身字段里；这里用一个单例记录器收集全部调用，
/// 同时充当处理器行为开关（是否命中、是否抛异常）。
/// </remarks>
public sealed class HandlerRecorder
{
    /// <summary>
    /// 按时间顺序记录的处理器调用
    /// </summary>
    public List<HandlerInvocation> Invocations { get; } = [];

    /// <summary>
    /// 设置后处理器在记录调用之后立即抛出该异常
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// 普通消息处理器的 CanHandle 返回值
    /// </summary>
    public bool MessageCanHandle { get; set; } = true;

    /// <summary>
    /// 回复消息处理器的 CanHandle 返回值
    /// </summary>
    public bool ReplyCanHandle { get; set; } = true;

    /// <summary>
    /// 会话状态处理器的 CanHandle 返回值
    /// </summary>
    public bool StateCanHandle { get; set; } = true;

    /// <summary>
    /// 内联查询处理器的 CanHandle 返回值
    /// </summary>
    public bool InlineCanHandle { get; set; } = true;

    /// <summary>
    /// /start 深链处理器是否声明已消费本次深链
    /// </summary>
    public bool StartPayloadHandled { get; set; } = true;

    /// <summary>
    /// 已记录的处理器名称序列
    /// </summary>
    public IReadOnlyList<string> HandlerNames => [.. Invocations.Select(x => x.Handler)];

    /// <summary>
    /// 记录一次调用；若配置了异常则记录后抛出
    /// </summary>
    /// <param name="handler">处理器名称</param>
    /// <param name="data">回调数据 / 深链参数 / 状态步骤等</param>
    /// <param name="args">命令参数</param>
    public void Record(string handler, string? data = null, string[]? args = null)
    {
        Invocations.Add(new HandlerInvocation(handler, data, args ?? []));
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }

    /// <summary>
    /// 获取指定处理器的调用次数
    /// </summary>
    /// <param name="handler">处理器名称</param>
    /// <returns>调用次数</returns>
    public int CountOf(string handler)
    {
        return Invocations.Count(x => string.Equals(x.Handler, handler, StringComparison.Ordinal));
    }
}

/// <summary>
/// 一次处理器调用的快照
/// </summary>
/// <param name="Handler">处理器名称</param>
/// <param name="Data">回调数据 / 深链参数 / 状态步骤等</param>
/// <param name="Args">命令参数</param>
public sealed record HandlerInvocation(string Handler, string? Data, string[] Args);
