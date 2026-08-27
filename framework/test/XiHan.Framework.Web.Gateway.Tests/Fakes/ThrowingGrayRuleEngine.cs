// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 总是抛异常的灰度规则引擎替身
/// </summary>
/// <remarks>
/// 用于验证灰度中间件不吞掉引擎异常，而是交给外层异常中间件统一转换成网关错误响应。
/// </remarks>
public sealed class ThrowingGrayRuleEngine : IGrayRuleEngine
{
    /// <summary>
    /// 执行灰度决策，总是抛出异常
    /// </summary>
    public Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("灰度规则仓储不可用");
    }
}
