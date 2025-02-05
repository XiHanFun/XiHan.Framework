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

using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http.Polly;

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

        // 若超时则抛出此异常
        var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
            [
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            ]);
        // 为每个重试定义超时策略
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10);

        // 远程请求
        _ = services.AddHttpClient(HttpGroupEnum.Remote.ToString(), options =>
            {
                options.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            // 忽略 SSL 不安全检查，或 HTTPS 不安全或 HTTPS 证书有误
            .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            })
            // 设置客户端生存期为 5 分钟
            .SetHandlerLifetime(TimeSpan.FromSeconds(5))
            // 将超时策略放在重试策略之内，每次重试会应用此超时策略
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        // 本地请求
        _ = services.AddHttpClient(HttpGroupEnum.Local.ToString(), options =>
            {
                options.BaseAddress = new Uri("http://127.0.0.1");
                options.DefaultRequestHeaders.Add("Accept", "application/json");
                // 需要额外的配置
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        // 注入 Http 相关实例
        _ = services.AddSingleton<IHttpPollyService, HttpPollyService>();
    }
}
