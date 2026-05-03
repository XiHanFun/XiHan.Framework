namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用初始化阶段扩展点。
/// </summary>
public interface IOnApplicationInitialization
{
    /// <summary>
    /// 在应用初始化阶段执行逻辑。
    /// </summary>
    void OnApplicationInitialization(ApplicationInitializationContext context);

    /// <summary>
    /// 在应用初始化阶段执行异步逻辑。
    /// </summary>
    Task OnApplicationInitializationAsync(ApplicationInitializationContext context);
}
