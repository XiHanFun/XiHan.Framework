#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IGrayRuleRepository
// Guid:8b9c0d1e-2f3a-4b5c-6d7e-8f9a0b1c2d3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.GrayRouting.Abstractions;

/// <summary>
/// 灰度规则仓储接口
/// </summary>
/// <remarks>
/// 负责灰度规则的读取,不负责写入和管理
/// Gateway 只读取,应用层管理
/// </remarks>
public interface IGrayRuleRepository
{
    /// <summary>
    /// 获取所有启用的灰度规则
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则列表</returns>
    Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则ID获取规则
    /// </summary>
    /// <param name="ruleId">规则ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则</returns>
    Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新规则缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
