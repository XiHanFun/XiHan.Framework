using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHanModuleLib.Extensions.DependencyInjection;

namespace XiHanModuleLib;

/// <summary>
/// FeatureName 模块
/// </summary>
public class FeatureNameModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddFeatureName(services.GetConfiguration());
    }
}
