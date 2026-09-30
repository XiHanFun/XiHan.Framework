// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Auditing.SqlSugar.Writers;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// 审计日志 SqlSugar 服务集合扩展
/// </summary>
public static class XiHanAuditingSqlSugarServiceCollectionExtensions
{
    /// <summary>
    /// 以 SqlSugar 写入器替换审计日志的空写入器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAuditingSqlSugar(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IAccessLogWriter, SqlSugarAccessLogWriter>());
        services.Replace(ServiceDescriptor.Scoped<IApiLogWriter, SqlSugarApiLogWriter>());
        services.Replace(ServiceDescriptor.Scoped<IEntityDiffLogWriter, SqlSugarEntityDiffLogWriter>());
        services.Replace(ServiceDescriptor.Scoped<IExceptionLogWriter, SqlSugarExceptionLogWriter>());
        services.Replace(ServiceDescriptor.Scoped<ILoginLogWriter, SqlSugarLoginLogWriter>());
        services.Replace(ServiceDescriptor.Scoped<IOperationLogWriter, SqlSugarOperationLogWriter>());

        return services;
    }
}
