using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Bootstrap.Application;

/// <summary>
/// 表示应用创建选项。
/// </summary>
public sealed class XiHanApplicationCreationOptions
{
    /// <summary>
    /// 使用指定服务集合初始化选项。
    /// </summary>
    /// <param name="services">服务集合。</param>
    public XiHanApplicationCreationOptions(IServiceCollection services)
    {
        Services = services;
        InstanceId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 获取服务集合。
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 获取或设置应用名称。
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// 获取应用实例标识。
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 获取或设置是否跳过服务配置阶段。
    /// </summary>
    public bool SkipConfigureServices { get; set; }
}
