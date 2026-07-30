// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Abstractions;

/// <summary>
/// 灰度规则引擎接口
/// </summary>
/// <remarks>
/// 负责执行灰度规则的匹配逻辑
/// </remarks>
public interface IGrayRuleEngine
{
    /// <summary>
    /// 执行灰度决策
    /// </summary>
    /// <param name="context">灰度上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>灰度决策结果</returns>
    Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken = default);
}
