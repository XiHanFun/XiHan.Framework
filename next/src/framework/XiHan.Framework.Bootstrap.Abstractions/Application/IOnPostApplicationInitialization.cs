namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用后置初始化阶段扩展点。
/// </summary>
public interface IOnPostApplicationInitialization
{
    /// <summary>
    /// 在应用后置初始化阶段执行逻辑。
    /// </summary>
    void OnPostApplicationInitialization(ApplicationInitializationContext context);

    /// <summary>
    /// 在应用后置初始化阶段执行异步逻辑。
    /// </summary>
    Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context);
}
