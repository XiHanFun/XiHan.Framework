// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core 的曦寒集成。
/// 将框架 Feature 和 Pipeline 注册到 Web 应用中。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public static class XiHanWebApplicationExtensions
{
    /// <summary>
    /// 向 ASP.NET Core WebApplication 注册曦寒框架。
    /// </summary>
    public static WebApplicationBuilder AddXiHan(this WebApplicationBuilder builder, Action<XiHanAppBuilder> configure)
    {
        var xiHanBuilder = new XiHanAppBuilder();

        configure(xiHanBuilder);

        var xiHanApp = xiHanBuilder.Build();

        // 将曦寒服务合并到 ASP.NET Core DI
        foreach (var feature in xiHanApp.Features)
        {
            if (feature is IHostedFeature hosted)
            {
                builder.Services.AddSingleton(hosted);
            }
        }

        builder.Services.AddSingleton(xiHanApp);

        return builder;
    }

    /// <summary>
    /// 在 ASP.NET Core 中间件管道中注册曦寒管道。
    /// </summary>
    public static IApplicationBuilder UseXiHan(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var xiHanApp = context.RequestServices.GetRequiredService<XiHanApp>();

            if (xiHanApp.Pipeline is not null)
            {
                var pipelineContext = new Kernel.Pipeline.PipelineContext
                {
                    TraceId = context.TraceIdentifier,
                    UserId = context.User?.Identity?.Name,
                };

                await xiHanApp.Pipeline(pipelineContext);
            }

            await next();
        });
    }
}
