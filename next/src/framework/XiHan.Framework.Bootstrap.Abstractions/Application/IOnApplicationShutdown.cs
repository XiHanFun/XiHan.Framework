namespace XiHan.Framework.Bootstrap.Abstractions.Application;

/// <summary>
/// 表示应用关闭阶段扩展点。
/// </summary>
public interface IOnApplicationShutdown
{
    /// <summary>
    /// 在应用关闭阶段执行逻辑。
    /// </summary>
    void OnApplicationShutdown(ApplicationShutdownContext context);

    /// <summary>
    /// 在应用关闭阶段执行异步逻辑。
    /// </summary>
    Task OnApplicationShutdownAsync(ApplicationShutdownContext context);
}
