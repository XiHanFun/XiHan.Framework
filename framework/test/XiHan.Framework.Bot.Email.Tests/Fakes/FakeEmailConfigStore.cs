// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Tests.Fakes;

/// <summary>
/// 手写的邮件配置存储替身
/// </summary>
/// <remarks>
/// 本仓测试栈不引入 Mock 框架，提供者的编排逻辑全部通过该替身驱动：
/// 既能返回任意配置（含 null），又能回放调用次数与实际透传的取消令牌。
/// </remarks>
public sealed class FakeEmailConfigStore : IEmailConfigStore
{
    private readonly EmailOptions? _options;

    /// <summary>
    /// 构造替身
    /// </summary>
    /// <param name="options">GetAsync 要返回的配置；null 表示未配置</param>
    public FakeEmailConfigStore(EmailOptions? options)
    {
        _options = options;
    }

    /// <summary>
    /// GetAsync 被调用的次数
    /// </summary>
    public int GetCallCount { get; private set; }

    /// <summary>
    /// 最近一次 GetAsync 收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 返回构造时给定的配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>构造时给定的配置</returns>
    public Task<EmailOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult<EmailOptions?>(_options);
    }
}
