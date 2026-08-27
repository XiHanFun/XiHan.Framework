// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 带公共无参构造函数的仓储替身，专供依赖注入装配测试使用
/// </summary>
/// <remarks>
/// 用于验证 TryAddSingleton 的「不覆盖既有注册」语义与 ReplaceGrayRuleRepository 的替换语义，
/// 因此必须能被容器直接激活，不能带构造参数。
/// </remarks>
public sealed class StubGrayRuleRepository : IGrayRuleRepository
{
    /// <summary>
    /// 获取所有启用的灰度规则
    /// </summary>
    public Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<IGrayRule>());
    }

    /// <summary>
    /// 根据规则ID获取规则
    /// </summary>
    public Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IGrayRule?>(null);
    }

    /// <summary>
    /// 刷新规则缓存
    /// </summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
