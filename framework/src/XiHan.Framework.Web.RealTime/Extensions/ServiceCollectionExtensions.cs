#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionExtensions
// Guid:af4a5b6c-7d8e-4f9a-9b5c-ad2e3f4a5b6c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 04:50:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.RealTime.Options;
using XiHan.Framework.Web.RealTime.Services;

namespace XiHan.Framework.Web.RealTime.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒 SignalR 服务（带 JSON 配置，从配置文件绑定选项）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>SignalR 服务构建器</returns>
    public static ISignalRServerBuilder AddXiHanSignalRWithJson(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<XiHanSignalROptions>(configuration.GetSection(XiHanSignalROptions.SectionName));
        return services.AddXiHanSignalRWithJson(configureOptions: null);
    }

    /// <summary>
    /// 添加曦寒 SignalR 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanSignalR(
        this IServiceCollection services,
        Action<XiHanSignalROptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // 添加 SignalR 服务
        services.AddSignalR();
        ApplySignalROptions(services);

        // 注册连接管理器
        services.AddSingleton<IConnectionManager, ConnectionManager>();

        // 注册用户 ID 提供器
        services.AddSingleton<IUserIdProvider, XiHanUserIdProvider>();
        services.AddSingleton<IXiHanUserIdProvider, XiHanUserIdProvider>();

        // 注册实时通知服务
        services.AddScoped(typeof(IRealtimeNotificationService<>), typeof(RealtimeNotificationService<>));

        return services;
    }

    /// <summary>
    /// 添加曦寒 SignalR 服务（带 JSON 配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns></returns>
    public static ISignalRServerBuilder AddXiHanSignalRWithJson(
        this IServiceCollection services,
        Action<XiHanSignalROptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // 添加 SignalR 服务并配置 JSON
        var signalRBuilder = services.AddSignalR()
            .AddJsonProtocol(jsonOptions =>
            {
                // 配置 JSON 序列化选项
                jsonOptions.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                jsonOptions.PayloadSerializerOptions.WriteIndented = false;
            });

        ApplySignalROptions(services);

        // 注册连接管理器
        services.AddSingleton<IConnectionManager, ConnectionManager>();

        // 注册用户 ID 提供器
        services.AddSingleton<IUserIdProvider, XiHanUserIdProvider>();
        services.AddSingleton<IXiHanUserIdProvider, XiHanUserIdProvider>();

        // 注册实时通知服务
        services.AddScoped(typeof(IRealtimeNotificationService<>), typeof(RealtimeNotificationService<>));

        return signalRBuilder;
    }

    /// <summary>
    /// 延迟桥接 XiHanSignalROptions → HubOptions，运行时从 DI 读取配置值
    /// </summary>
    private static void ApplySignalROptions(IServiceCollection services)
    {
        services.AddOptions<HubOptions>()
            .Configure<IOptions<XiHanSignalROptions>>((hubOptions, xihanOptions) =>
            {
                var opts = xihanOptions.Value;
                hubOptions.EnableDetailedErrors = opts.EnableDetailedErrors;
                hubOptions.KeepAliveInterval = opts.KeepAliveInterval;
                hubOptions.ClientTimeoutInterval = opts.ClientTimeoutInterval;
                hubOptions.HandshakeTimeout = opts.HandshakeTimeout;
                hubOptions.MaximumReceiveMessageSize = opts.MaximumReceiveMessageSize;
                hubOptions.StreamBufferCapacity = opts.StreamBufferCapacity;
                hubOptions.MaximumParallelInvocationsPerClient = opts.MaximumParallelInvocationsPerClient;
            });
    }
}
