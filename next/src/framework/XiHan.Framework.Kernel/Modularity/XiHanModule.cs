namespace XiHan.Framework.Kernel.Modularity;

/// <summary>
/// 提供最小模块基类。
/// </summary>
/// <remarks>
/// 更复杂的生命周期接口位于引导层抽象中，由上层运行时按需选择实现。
/// </remarks>
public abstract class XiHanModule : IXiHanModule
{
    /// <inheritdoc />
    public virtual void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <inheritdoc />
    public virtual Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        ConfigureServices(context);
        return Task.CompletedTask;
    }
}
