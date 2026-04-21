namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示使用外部服务提供器的应用抽象。
/// </summary>
public interface IXiHanApplicationWithExternalServiceProvider : IXiHanApplication
{
    /// <summary>
    /// 设置服务提供器。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    void SetServiceProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// 使用指定服务提供器完成初始化。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>
    /// 使用指定服务提供器完成初始化。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    void Initialize(IServiceProvider serviceProvider);
}
