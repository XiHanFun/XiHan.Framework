// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Settings.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Settings.SqlSugar.Stores;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.SqlSugar.Tests;

/// <summary>
/// 注册扩展测试
/// </summary>
public class RegistrationTests
{
    /// <summary>
    /// 注册扩展以 SqlSugar 存储顶替空存储
    /// </summary>
    [Fact]
    public void 注册扩展顶替空存储()
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<ISettingStore, NullSettingStore>();

        services.AddXiHanSettingsSqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ISettingStore));
        Assert.Equal(typeof(SqlSugarSettingStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
