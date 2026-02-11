#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpServiceCollectionServiceExtensions
// Guid:81827492-88f1-4dba-abbd-746c7c062cac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/06 00:16:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Middleware;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Proxy;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架网络请求服务扩展方法
/// </summary>
public static class XiHanHttpServiceCollectionServiceExtensions
{
    /// <summary>
    /// 添加曦寒框架网络请求模块
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanHttpModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置选项
        services.Configure<XiHanHttpClientOptions>(configuration.GetSection(XiHanHttpClientOptions.SectionName));
        services.Configure<XiHanProxyPoolOptions>(configuration.GetSection(XiHanProxyPoolOptions.SectionName));

        // 注册代理相关服务
        services.AddSingleton<IProxyValidator, ProxyValidator>();
        services.AddSingleton<IProxyPoolManager, ProxyPoolManager>();

        // 注册HTTP服务
        services.AddScoped<IAdvancedHttpService, AdvancedHttpService>();

        // 配置HTTP客户端
        ConfigureHttpClients(services, configuration);

        // 启动代理池健康检查
        ConfigureProxyPool(services, configuration);

        return services;
    }

    /// <summary>
    /// 配置代理池
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    private static void ConfigureProxyPool(IServiceCollection services, IConfiguration configuration)
    {
        var proxyPoolOptions = new XiHanProxyPoolOptions();
        configuration.GetSection(XiHanProxyPoolOptions.SectionName).Bind(proxyPoolOptions);

        if (proxyPoolOptions.Enabled && proxyPoolOptions.EnableHealthCheck)
        {
            // 添加后台服务来管理代理池健康检查
            services.AddHostedService<ProxyPoolHealthCheckService>();
        }
    }

    /// <summary>
    /// 配置HTTP客户端
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    private static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        var httpOptions = new XiHanHttpClientOptions();
        configuration.GetSection(XiHanHttpClientOptions.SectionName).Bind(httpOptions);

        // 配置远程请求客户端
        ConfigureRemoteHttpClient(services, httpOptions);

        // 配置本地请求客户端
        ConfigureLocalHttpClient(services, httpOptions);

        // 配置自定义客户端
        ConfigureCustomHttpClients(services, httpOptions);
    }

    /// <summary>
    /// 配置远程HTTP客户端
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">HTTP选项</param>
    private static void ConfigureRemoteHttpClient(IServiceCollection services, XiHanHttpClientOptions options)
    {
        var clientBuilder = services.AddHttpClient(HttpGroupEnum.Remote.ToString(), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.DefaultTimeoutSeconds);

            // 添加默认请求头
            foreach (var header in options.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Remove(header.Key);
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        });

        // 配置主要消息处理器
        clientBuilder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var handler = new HttpClientHandler();

            if (options.IgnoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            return handler;
        });

        // 添加日志中间件
        clientBuilder.AddHttpMessageHandler(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<HttpLoggingMiddleware>>();
            return new HttpLoggingMiddleware(logger, options);
        });

        // 配置Polly策略
        ConfigurePollyPolicies(clientBuilder, options);

        // 设置客户端生存期
        clientBuilder.SetHandlerLifetime(TimeSpan.FromMinutes(options.ClientLifetimeMinutes));
    }

    /// <summary>
    /// 配置本地HTTP客户端
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">HTTP选项</param>
    private static void ConfigureLocalHttpClient(IServiceCollection services, XiHanHttpClientOptions options)
    {
        var clientBuilder = services.AddHttpClient(HttpGroupEnum.Local.ToString(), client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1");
            client.Timeout = TimeSpan.FromSeconds(options.DefaultTimeoutSeconds);
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            // 添加默认请求头
            foreach (var header in options.DefaultHeaders)
            {
                client.DefaultRequestHeaders.Remove(header.Key);
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        });

        // 添加日志中间件
        clientBuilder.AddHttpMessageHandler(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<HttpLoggingMiddleware>>();
            return new HttpLoggingMiddleware(logger, options);
        });

        // 配置Polly策略
        ConfigurePollyPolicies(clientBuilder, options);

        // 设置客户端生存期
        clientBuilder.SetHandlerLifetime(TimeSpan.FromMinutes(options.ClientLifetimeMinutes));
    }

    /// <summary>
    /// 配置自定义HTTP客户端
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">HTTP选项</param>
    private static void ConfigureCustomHttpClients(IServiceCollection services, XiHanHttpClientOptions options)
    {
        foreach (var clientConfig in options.Clients)
        {
            var clientBuilder = services.AddHttpClient(clientConfig.Key, client =>
            {
                if (!string.IsNullOrEmpty(clientConfig.Value.BaseAddress))
                {
                    client.BaseAddress = new Uri(clientConfig.Value.BaseAddress);
                }

                var timeout = clientConfig.Value.TimeoutSeconds ?? options.DefaultTimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeout);

                // 添加默认请求头
                foreach (var header in options.DefaultHeaders)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }

                // 添加自定义请求头
                foreach (var header in clientConfig.Value.Headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            });

            // 配置主要消息处理器
            clientBuilder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var handler = new HttpClientHandler();

                var ignoreSslErrors = clientConfig.Value.IgnoreSslErrors ?? options.IgnoreSslErrors;
                if (ignoreSslErrors)
                {
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }

                return handler;
            });

            // 添加日志中间件
            clientBuilder.AddHttpMessageHandler(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<HttpLoggingMiddleware>>();
                return new HttpLoggingMiddleware(logger, options);
            });

            // 配置Polly策略
            if (clientConfig.Value.EnableRetry || clientConfig.Value.EnableCircuitBreaker)
            {
                ConfigurePollyPolicies(clientBuilder, options, clientConfig.Value);
            }

            // 设置客户端生存期
            clientBuilder.SetHandlerLifetime(TimeSpan.FromMinutes(options.ClientLifetimeMinutes));
        }
    }

    /// <summary>
    /// 配置Polly策略
    /// </summary>
    /// <param name="clientBuilder">HTTP客户端构建器</param>
    /// <param name="options">HTTP选项</param>
    /// <param name="clientConfig">客户端配置</param>
    private static void ConfigurePollyPolicies(IHttpClientBuilder clientBuilder, XiHanHttpClientOptions options,
        HttpClientConfiguration? clientConfig = null)
    {
        // 重试策略
        var enableRetry = clientConfig?.EnableRetry ?? true;
        if (enableRetry)
        {
            // 使用动态策略选择器，允许在运行时根据请求上下文决定是否应用重试
            clientBuilder.AddPolicyHandler((request) =>
            {
                // 检查请求上下文中是否禁用了重试
                if (request.Options.TryGetValue(new HttpRequestOptionsKey<bool>("EnableRetry"), out var enableRetryOption)
                    && !enableRetryOption)
                {
                    // 如果禁用重试，返回一个空策略（NoOp）
                    return Policy.NoOpAsync<HttpResponseMessage>();
                }

                // 否则应用重试策略
                var retryDelays = options.RetryDelaySeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray();
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TimeoutRejectedException>()
                    .WaitAndRetryAsync(retryDelays, onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        // 如果需要日志记录，可以在HTTP中间件中处理
                    });
            });
        }

        // 超时策略
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(2 * options.DefaultTimeoutSeconds);
        clientBuilder.AddPolicyHandler(timeoutPolicy);

        // 熔断器策略
        var enableCircuitBreaker = clientConfig?.EnableCircuitBreaker ?? true;
        if (enableCircuitBreaker)
        {
            // 使用动态策略选择器，允许在运行时根据请求上下文决定是否应用断路器
            clientBuilder.AddPolicyHandler((request) =>
            {
                // 检查请求上下文中是否禁用了断路器
                if (request.Options.TryGetValue(new HttpRequestOptionsKey<bool>("EnableCircuitBreaker"), out var enableCircuitBreakerOption)
                    && !enableCircuitBreakerOption)
                {
                    // 如果禁用断路器，返回一个空策略（NoOp）
                    return Policy.NoOpAsync<HttpResponseMessage>();
                }

                // 否则应用断路器策略
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: options.CircuitBreakerFailureThreshold,
                        durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakerDurationOfBreakSeconds),
                        onBreak: (exception, duration) =>
                        {
                            // 记录熔断器打开的日志
                        },
                        onReset: () =>
                        {
                            // 记录熔断器重置的日志
                        },
                        onHalfOpen: () =>
                        {
                            // 记录熔断器半开状态的日志
                        });
            });
        }
    }
}
