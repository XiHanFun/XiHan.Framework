// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.SqlSugar.Options;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 服务注册测试
/// </summary>
public class ServiceRegistrationTests
{
    /// <summary>
    /// 选项从配置节绑定
    /// </summary>
    [Fact]
    public void 选项从配置节绑定()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Workflow:SqlSugar:ConfigId"] = "Workflow"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddXiHanWorkflowSqlSugar(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanWorkflowSqlSugarOptions>>().Value;
        Assert.Equal("Workflow", options.ConfigId);
        Assert.Equal("XiHan:Workflow:SqlSugar", XiHanWorkflowSqlSugarOptions.SectionName);
    }

    /// <summary>
    /// 执行器注册为作用域服务
    /// </summary>
    [Fact]
    public void 执行器注册为作用域服务()
    {
        var services = CreateServices(registerSqlSugarFirst: false);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(WorkflowSqlSugarExecutor));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 定义存储被替换为作用域实现
    /// </summary>
    /// <param name="registerSqlSugarFirst">是否先注册本包</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 定义存储被替换为作用域实现(bool registerSqlSugarFirst)
    {
        var services = CreateServices(registerSqlSugarFirst);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IWorkflowDefinitionStore));
        Assert.Equal(typeof(SqlSugarWorkflowDefinitionStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 实例存储被替换为作用域实现
    /// </summary>
    /// <param name="registerSqlSugarFirst">是否先注册本包</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 实例存储被替换为作用域实现(bool registerSqlSugarFirst)
    {
        var services = CreateServices(registerSqlSugarFirst);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IWorkflowInstanceStore));
        Assert.Equal(typeof(SqlSugarWorkflowInstanceStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 书签存储被替换为作用域实现
    /// </summary>
    /// <param name="registerSqlSugarFirst">是否先注册本包</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 书签存储被替换为作用域实现(bool registerSqlSugarFirst)
    {
        var services = CreateServices(registerSqlSugarFirst);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IWorkflowBookmarkStore));
        Assert.Equal(typeof(SqlSugarWorkflowBookmarkStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 三个默认存储全部被替换
    /// </summary>
    [Fact]
    public void 三个默认存储全部被替换()
    {
        var services = CreateServices(registerSqlSugarFirst: false);

        var storeTypes = services
            .Where(item => item.ServiceType == typeof(IWorkflowDefinitionStore)
                || item.ServiceType == typeof(IWorkflowInstanceStore)
                || item.ServiceType == typeof(IWorkflowBookmarkStore))
            .Select(item => item.ImplementationType)
            .ToList();

        Assert.Equal(3, storeTypes.Count);
        Assert.All(storeTypes, type => Assert.Equal("XiHan.Framework.Workflow.SqlSugar.Stores", type?.Namespace));
    }

    private static IServiceCollection CreateServices(bool registerSqlSugarFirst)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        if (registerSqlSugarFirst)
        {
            services.AddXiHanWorkflowSqlSugar(configuration);
            services.AddXiHanWorkflow(configuration);
        }
        else
        {
            services.AddXiHanWorkflow(configuration);
            services.AddXiHanWorkflowSqlSugar(configuration);
        }

        return services;
    }
}
