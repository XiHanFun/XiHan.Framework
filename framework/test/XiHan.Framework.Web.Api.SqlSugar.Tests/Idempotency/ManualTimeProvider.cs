// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.SqlSugar.Tests.Idempotency;

/// <summary>
/// 可手动推进的时钟
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 当前时间
    /// </summary>
    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    /// <summary>
    /// 推进时间
    /// </summary>
    public void Advance(TimeSpan delta)
    {
        _now = _now.Add(delta);
    }
}
