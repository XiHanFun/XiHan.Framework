namespace XiHan.Framework.Kernel.Modularity;

/// <summary>
/// 表示框架中的最小模块抽象。
/// </summary>
/// <remarks>
/// 模块是框架能力组织的基础单元。
/// 在新架构中，模块化只是可选运行时能力之一，而不是整个框架唯一的组织方式。
/// 因此本契约只保留最小服务注册能力，不在最小内核中固化完整生命周期编排。
/// </remarks>
public interface IXiHanModule
{
    /// <summary>
    /// 配置模块服务。
    /// </summary>
    /// <param name="context">服务配置上下文。</param>
    void ConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// 异步配置模块服务。
    /// </summary>
    /// <param name="context">服务配置上下文。</param>
    /// <returns>表示异步配置操作的任务。</returns>
    Task ConfigureServicesAsync(ServiceConfigurationContext context);
}
