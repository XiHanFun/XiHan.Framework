// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;

/// <summary>
/// 可手动推进的时间提供程序
/// </summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="utcNow">初始 UTC 时间</param>
    public MutableTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>
    /// 获取当前 UTC 时间
    /// </summary>
    /// <returns>当前 UTC 时间</returns>
    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    /// <summary>
    /// 推进时间
    /// </summary>
    /// <param name="delta">推进量</param>
    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }
}
