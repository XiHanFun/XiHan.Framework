namespace XiHanModuleLib.Services;

/// <summary>
/// FeatureName 服务
/// </summary>
public interface IFeatureNameService
{
    /// <summary>
    /// 生成问候语
    /// </summary>
    /// <param name="name">称呼</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>问候语</returns>
    Task<string> GreetAsync(string name, CancellationToken cancellationToken = default);
}
