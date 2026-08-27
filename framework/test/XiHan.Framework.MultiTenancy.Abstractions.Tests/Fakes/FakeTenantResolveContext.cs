// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 租户解析上下文的手写替身
/// </summary>
/// <remarks>
/// <see cref="ITenantResolveContext"/> 继承自 Core 的服务提供者访问器契约，
/// 因此替身必须同时给出 <see cref="ServiceProvider"/>，解析贡献者才能在解析期取到容器内的依赖。
/// </remarks>
internal sealed class FakeTenantResolveContext : ITenantResolveContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public FakeTenantResolveContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 租户唯一标识或名称
    /// </summary>
    public string? TenantIdOrName { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool Handled { get; set; }
}
