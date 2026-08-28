// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 租户解析贡献者的手写替身
/// </summary>
/// <remarks>
/// 实现解析链的标准行为：上下文已被前序贡献者处理（<see cref="ITenantResolveContext.Handled"/> 为 true）时直接放行，
/// 否则写入自己的解析结果并置位 Handled。<see cref="InvokeCount"/> 用来观察链上每一环是否真的被调用过。
/// </remarks>
internal sealed class FakeTenantResolveContributor : ITenantResolveContributor
{
    private readonly string? _resolvedTenantIdOrName;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">贡献者名称</param>
    /// <param name="resolvedTenantIdOrName">该贡献者能解析出的租户唯一标识或名称，null 表示解析不出</param>
    public FakeTenantResolveContributor(string name, string? resolvedTenantIdOrName = null)
    {
        Name = name;
        _resolvedTenantIdOrName = resolvedTenantIdOrName;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int InvokeCount { get; private set; }

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <returns>解析任务</returns>
    public Task ResolveAsync(ITenantResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        InvokeCount++;

        if (context.Handled || _resolvedTenantIdOrName is null)
        {
            return Task.CompletedTask;
        }

        context.TenantIdOrName = _resolvedTenantIdOrName;
        context.Handled = true;
        return Task.CompletedTask;
    }
}
