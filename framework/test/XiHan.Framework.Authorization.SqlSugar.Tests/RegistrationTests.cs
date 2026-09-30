// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Authorization.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.SqlSugar.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Policies;
using XiHan.Framework.Authorization.SqlSugar.Roles;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace XiHan.Framework.Authorization.SqlSugar.Tests;

/// <summary>
/// 注册测试
/// </summary>
public class RegistrationTests
{
    /// <summary>
    /// 契约被顶替为 SqlSugar 实现
    /// </summary>
    /// <param name="serviceType">契约类型</param>
    /// <param name="implementationType">期望的实现类型</param>
    [Theory]
    [InlineData(typeof(IPermissionStore), typeof(SqlSugarPermissionStore))]
    [InlineData(typeof(IRoleStore), typeof(SqlSugarRoleStore))]
    [InlineData(typeof(IPermissionChecker), typeof(SqlSugarPermissionChecker))]
    [InlineData(typeof(IPolicyStore), typeof(SqlSugarPolicyStore))]
    public void 契约被顶替为SqlSugar实现(Type serviceType, Type implementationType)
    {
        var services = BuildServices();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 权限存储的具体类型以作用域注册
    /// </summary>
    [Fact]
    public void 权限存储的具体类型以作用域注册()
    {
        var services = BuildServices();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(SqlSugarPermissionStore));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 模块依赖授权模块与数据模块
    /// </summary>
    [Fact]
    public void 模块依赖授权模块与数据模块()
    {
        var attribute = typeof(XiHanAuthorizationSqlSugarModule).GetCustomAttribute<DependsOnAttribute>();

        Assert.NotNull(attribute);
        Assert.Contains(typeof(XiHanAuthorizationModule), attribute.DependedTypes);
        Assert.Contains(typeof(XiHanDataModule), attribute.DependedTypes);
    }

    /// <summary>
    /// 先注册主包默认实现，再注册本包
    /// </summary>
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();

        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());
        services.AddXiHanAuthorizationSqlSugar();

        return services;
    }
}
