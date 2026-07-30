// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务提供者访问器接口
/// </summary>
public interface IServiceProviderAccessor
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
