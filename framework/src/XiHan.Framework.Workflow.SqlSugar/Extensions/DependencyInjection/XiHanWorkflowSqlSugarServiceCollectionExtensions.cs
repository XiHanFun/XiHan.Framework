// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.SqlSugar.Options;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 工作流 SqlSugar 存储服务集合扩展
/// </summary>
public static class XiHanWorkflowSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 存储替换工作流的进程内默认存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWorkflowSqlSugar(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<XiHanWorkflowSqlSugarOptions>(
            configuration.GetSection(XiHanWorkflowSqlSugarOptions.SectionName));

        services.TryAddScoped<WorkflowSqlSugarExecutor>();
        services.Replace(ServiceDescriptor.Scoped<IWorkflowDefinitionStore, SqlSugarWorkflowDefinitionStore>());
        services.Replace(ServiceDescriptor.Scoped<IWorkflowInstanceStore, SqlSugarWorkflowInstanceStore>());
        services.Replace(ServiceDescriptor.Scoped<IWorkflowBookmarkStore, SqlSugarWorkflowBookmarkStore>());

        return services;
    }
}
