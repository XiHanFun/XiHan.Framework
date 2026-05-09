// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// ASP.NET Core 的 XiHan 集成扩展方法。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public static class XiHanWebApplicationExtensions
{
    /// <summary>
    /// 向 WebApplicationBuilder 注册 XiHan 框架，共享宿主 DI 容器。
    /// </summary>
    public static WebApplicationBuilder AddXiHan(this WebApplicationBuilder builder, Action<XiHanAppBuilder> configure)
    {
        var xiHanBuilder = new XiHanAppBuilder(builder.Services, builder.Configuration);
        configure(xiHanBuilder);
        var app = xiHanBuilder.Build();
        builder.Services.AddHostedFeatures(app.Features);
        builder.Services.AddSingleton(app);
        return builder;
    }

    /// <summary>
    /// 在 ASP.NET Core 中间件管道中注册 XiHan 管道。
    /// </summary>
    public static IApplicationBuilder UseXiHan(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var xiHanApp = context.RequestServices.GetService<XiHanApp>();
            if (xiHanApp?.Pipeline is not null)
            {
                var pipelineContext = new PipelineContext
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
