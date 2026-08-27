// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Tests.Fakes;

/// <summary>
/// 租户解析贡献者基类的最小具体子类
/// </summary>
/// <remarks>
/// <see cref="TenantResolveContributorBase"/> 是抽象类，无法直接实例化。
/// 这个子类只做两件事：把名称固定下来、按构造时给定的结果决定是否「命中」，
/// 并记录被调用次数，用来验证解析链的首命中短路语义。
/// </remarks>
internal sealed class RecordingTenantResolveContributor : TenantResolveContributorBase
{
    private readonly string _name;
    private readonly string? _resolvedValue;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">贡献者名称</param>
    /// <param name="resolvedValue">命中时写入上下文的租户唯一标识或名称；为 null 表示该贡献者不命中</param>
    public RecordingTenantResolveContributor(string name, string? resolvedValue = null)
    {
        _name = name;
        _resolvedValue = resolvedValue;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => _name;

    /// <summary>
    /// 解析被调用的次数
    /// </summary>
    public int ResolveCallCount { get; private set; }

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <returns></returns>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        ResolveCallCount++;

        if (_resolvedValue is not null)
        {
            context.TenantIdOrName = _resolvedValue;
            context.Handled = true;
        }

        return Task.CompletedTask;
    }
}
