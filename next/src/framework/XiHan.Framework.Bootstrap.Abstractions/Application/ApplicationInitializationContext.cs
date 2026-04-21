namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用初始化上下文。
/// </summary>
public sealed class ApplicationInitializationContext
{
    /// <summary>
    /// 使用指定服务提供器初始化上下文。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    public ApplicationInitializationContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 获取服务提供器。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
