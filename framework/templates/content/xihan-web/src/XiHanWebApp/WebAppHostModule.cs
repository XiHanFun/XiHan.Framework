using XiHan.Framework.Core.Modularity;
#if (Data)
using XiHan.Framework.Data;
#endif
#if (Observability)
using XiHan.Framework.Core.Application;
using XiHan.Framework.Observability;
using XiHan.Framework.Web.Core.Extensions;
#endif
using XiHan.Framework.Web.Api;
using XiHan.Framework.Web.Docs;

namespace XiHanWebApp;

/// <summary>
/// 应用启动模块
/// </summary>
[DependsOn(
#if (Data)
    typeof(XiHanDataModule),
#endif
#if (Observability)
    typeof(XiHanObservabilityModule),
#endif
    typeof(XiHanWebApiModule),
    typeof(XiHanWebDocsModule)
)]
public class WebAppHostModule : XiHanModule
{
#if (Observability)
    /// <summary>
    /// 健康检查端点路径
    /// </summary>
    public const string HealthCheckPath = "/health";

    /// <summary>
    /// 应用初始化：映射健康检查端点
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseEndpoints(endpoints => endpoints.MapHealthChecks(HealthCheckPath));
    }
#endif
}
