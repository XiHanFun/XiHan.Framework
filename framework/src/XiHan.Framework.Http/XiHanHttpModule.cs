#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpModule
// Guid:bd5cd31c-c791-42d9-a48c-56ead4293941
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:35:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Middleware;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http;

/// <summary>
/// 曦寒框架网络请求模块
/// </summary>
public class XiHanHttpModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // 配置选项
        services.Configure<HttpClientOptions>(configuration.GetSection(HttpClientOptions.SectionName));

        // 注册服务
        services.AddSingleton<IHttpPollyService, HttpPollyService>();
        services.AddScoped<IAdvancedHttpService, AdvancedHttpService>();

        // 配置HTTP客户端
        ConfigureHttpClients(services, configuration);
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 初始化字符串扩展的HTTP服务
        var httpService = context.ServiceProvider.GetRequiredService<IAdvancedHttpService>();
        StringHttpExtensions.SetHttpService(httpService);
    }

    /// <summary>
    /// 配置HTTP客户端
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    private static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        var httpOptions = new HttpClientOptions();
        configuration.GetSection(HttpClientOptions.SectionName).Bind(httpOptions);

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
    private static void ConfigureRemoteHttpClient(IServiceCollection services, HttpClientOptions options)
    {
        var clientBuilder = services.AddHttpClient(HttpGroupEnum.Remote.ToString(), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.DefaultTimeoutSeconds);

            // 添加默认请求头
            foreach (var header in options.DefaultHeaders)
            {
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
    private static void ConfigureLocalHttpClient(IServiceCollection services, HttpClientOptions options)
    {
        var clientBuilder = services.AddHttpClient(HttpGroupEnum.Local.ToString(), client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1");
            client.Timeout = TimeSpan.FromSeconds(options.DefaultTimeoutSeconds);
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            // 添加默认请求头
            foreach (var header in options.DefaultHeaders)
            {
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
    private static void ConfigureCustomHttpClients(IServiceCollection services, HttpClientOptions options)
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
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }

                // 添加自定义请求头
                foreach (var header in clientConfig.Value.Headers)
                {
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
    private static void ConfigurePollyPolicies(IHttpClientBuilder clientBuilder, HttpClientOptions options,
        HttpClientConfiguration? clientConfig = null)
    {
        var enableRetry = clientConfig?.EnableRetry ?? true;
        var enableCircuitBreaker = clientConfig?.EnableCircuitBreaker ?? true;

        // 重试策略
        if (enableRetry)
        {
            var retryDelays = options.RetryDelaySeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray();
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(retryDelays, onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // 注意：这里移除了日志记录，因为在静态方法中无法直接访问日志记录器
                    // 如果需要日志记录，可以在HTTP中间件中处理
                });

            clientBuilder.AddPolicyHandler(retryPolicy);
        }

        // 超时策略
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(options.DefaultTimeoutSeconds);
        clientBuilder.AddPolicyHandler(timeoutPolicy);

        // 熔断器策略
        if (enableCircuitBreaker)
        {
            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: options.CircuitBreakerFailureThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakerDurationOfBreakSeconds),
                    onBreak: (exception, duration) =>
                    {
                        // 可以在这里记录熔断器打开的日志
                    },
                    onReset: () =>
                    {
                        // 可以在这里记录熔断器重置的日志
                    });

            clientBuilder.AddPolicyHandler(circuitBreakerPolicy);
        }
    }
}
