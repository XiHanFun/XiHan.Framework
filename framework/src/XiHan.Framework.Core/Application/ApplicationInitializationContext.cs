// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 应用初始化上下文
/// </summary>
public class ApplicationInitializationContext : IServiceProviderAccessor
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public ApplicationInitializationContext(IServiceProvider serviceProvider)
    {
        Guard.NotNull(serviceProvider, nameof(serviceProvider));

        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
