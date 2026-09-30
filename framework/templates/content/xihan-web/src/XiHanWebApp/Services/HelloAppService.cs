using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;

namespace XiHanWebApp.Services;

/// <summary>
/// 问候服务（由动态 API 自动暴露为接口）
/// </summary>
[DynamicApi]
public class HelloAppService : ApplicationServiceBase
{
    /// <summary>
    /// 返回问候语
    /// </summary>
    /// <param name="name">称呼</param>
    /// <returns>问候语</returns>
    public string GetGreeting(string name)
    {
        return $"你好，{name}！";
    }
}
