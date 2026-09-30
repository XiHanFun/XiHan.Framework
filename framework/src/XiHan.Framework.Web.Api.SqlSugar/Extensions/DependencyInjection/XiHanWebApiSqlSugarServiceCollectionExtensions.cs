// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Web.Api.Idempotency;
using XiHan.Framework.Web.Api.SqlSugar.Idempotency;

namespace XiHan.Framework.Web.Api.SqlSugar.Extensions.DependencyInjection;

/// <summary>
/// Web API SqlSugar 持久化服务注册扩展
/// </summary>
public static class XiHanWebApiSqlSugarServiceCollectionExtensions
{
    private const int MaxKeyColumnLength = 128;

    /// <summary>
    /// 注册 SqlSugar 幂等存储，替换默认的进程内存储，并在启动时校验幂等键最大长度不超过记录列长度
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebApiSqlSugar(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<XiHanIdempotencyOptions>()
            .Validate(options => options.MaxKeyLength <= MaxKeyColumnLength,
                $"幂等配置无效：使用 SqlSugar 存储时 MaxKeyLength 不能超过 {MaxKeyColumnLength}。")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<SqlSugarIdempotencyStore>();
        services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore, SqlSugarIdempotencyStore>());

        return services;
    }
}
