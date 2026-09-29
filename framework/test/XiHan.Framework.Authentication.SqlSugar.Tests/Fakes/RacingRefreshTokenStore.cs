// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.Jwt;

namespace XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;

/// <summary>
/// 在下一次保存前插入一段操作的刷新令牌存储装饰器，用于确定性地复现并发刷新
/// </summary>
internal sealed class RacingRefreshTokenStore : IRefreshTokenStore
{
    private readonly IRefreshTokenStore _inner;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner">被装饰的存储</param>
    public RacingRefreshTokenStore(IRefreshTokenStore inner)
    {
        _inner = inner;
    }

    /// <summary>
    /// 下一次保存前执行的操作，执行一次后清空
    /// </summary>
    public Action? BeforeNextSave { get; set; }

    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">主体标识</param>
    /// <param name="expiresAt">过期时间</param>
    public void Save(string refreshToken, string? subject, DateTime expiresAt)
    {
        var hook = BeforeNextSave;
        BeforeNextSave = null;
        hook?.Invoke();

        _inner.Save(refreshToken, subject, expiresAt);
    }

    /// <summary>
    /// 校验刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">主体标识</param>
    /// <returns>是否有效</returns>
    public bool Validate(string refreshToken, string? subject = null)
    {
        return _inner.Validate(refreshToken, subject);
    }

    /// <summary>
    /// 移除刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    public void Remove(string refreshToken)
    {
        _inner.Remove(refreshToken);
    }
}
