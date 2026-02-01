#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IGrayRuleEngine
// Guid:9c0d1e2f-3a4b-5c6d-7e8f-9a0b1c2d3e4f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
