namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用预初始化阶段扩展点。
/// </summary>
public interface IOnPreApplicationInitialization
{
    /// <summary>
    /// 在应用预初始化阶段执行逻辑。
    /// </summary>
    void OnPreApplicationInitialization(ApplicationInitializationContext context);

    /// <summary>
    /// 在应用预初始化阶段执行异步逻辑。
    /// </summary>
    Task OnPreApplicationInitializationAsync(ApplicationInitializationContext context);
}
