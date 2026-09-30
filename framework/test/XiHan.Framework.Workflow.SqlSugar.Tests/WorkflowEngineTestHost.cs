// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.Events;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 以 SqlSugar 存储组装真实工作流引擎的测试主机
/// </summary>
internal sealed class WorkflowEngineTestHost : IDisposable
{
    private readonly WorkflowTestDatabase _database;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="database">测试库，为空时新建临时 SQLite 库；主机负责释放</param>
    /// <param name="configureServices">在本包注册之后追加的服务注册</param>
    /// <param name="workerId">雪花标识的工作节点号，多个主机共用一个库时各取不同值</param>
    public WorkflowEngineTestHost(
        WorkflowTestDatabase? database = null,
        Action<IServiceCollection>? configureServices = null,
        ushort workerId = 1)
    {
        _database = database ?? WorkflowTestDatabase.CreateSqlite();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions();
        services.AddLogging();

        Clock = new TestClock();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentTenant>(new TestCurrentTenant());
        services.AddSingleton<IDistributedLock>(new InProcessTestLock());
        services.AddSingleton(IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(workerId));

        services.AddXiHanWorkflow(configuration);
        services.Replace(ServiceDescriptor.Singleton<IWorkflowEventPublisher, NullWorkflowEventPublisher>());

        services.AddSingleton<ISqlSugarClientResolver>(_database.Resolver);
        services.AddSingleton(_database.UnitOfWorkManager);
        services.AddXiHanWorkflowSqlSugar(configuration);
        configureServices?.Invoke(services);

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    /// <summary>
    /// 可拨动测试时钟
    /// </summary>
    public TestClock Clock { get; }

    /// <summary>
    /// 工作流引擎
    /// </summary>
    public IWorkflowEngine Engine => _scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

    /// <summary>
    /// 定义管理器
    /// </summary>
    public IWorkflowDefinitionManager DefinitionManager => _scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionManager>();

    /// <summary>
    /// 人工任务服务
    /// </summary>
    public IWorkflowUserTaskService UserTaskService => _scope.ServiceProvider.GetRequiredService<IWorkflowUserTaskService>();

    /// <summary>
    /// 实例存储
    /// </summary>
    public IWorkflowInstanceStore InstanceStore => _scope.ServiceProvider.GetRequiredService<IWorkflowInstanceStore>();

    /// <summary>
    /// 书签存储
    /// </summary>
    public IWorkflowBookmarkStore BookmarkStore => _scope.ServiceProvider.GetRequiredService<IWorkflowBookmarkStore>();

    /// <summary>
    /// 定义存储
    /// </summary>
    public IWorkflowDefinitionStore DefinitionStore => _scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>();

    /// <summary>
    /// 创建并发布定义
    /// </summary>
    /// <param name="definition">定义内容</param>
    /// <returns>已发布的定义</returns>
    public async Task<WorkflowDefinition> PublishAsync(WorkflowDefinition definition)
    {
        var created = await DefinitionManager.CreateAsync(definition);
        return await DefinitionManager.PublishAsync(created.Id);
    }

    /// <summary>
    /// 从存储重新加载实例
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <returns>实例</returns>
    public async Task<WorkflowInstance> ReloadAsync(string instanceId)
    {
        return await InstanceStore.FindAsync(instanceId)
            ?? throw new InvalidOperationException($"实例 {instanceId} 不存在");
    }

    /// <summary>
    /// 释放作用域、容器与测试库
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _database.Dispose();
    }
}
