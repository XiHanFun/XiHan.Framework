using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Utils.HardwareInfos;

namespace XiHan.Framework.Web.Test.Controllers;

/// <summary>
/// HomeController
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// 测试
    /// </summary>
    /// <returns></returns>
    [HttpPost("SystemInfo")]
    public async Task<IActionResult> SystemInfo()
    {
        var systemInfo = await HardwareInfoManager.GetSystemHardwareInfoAsync();
        return Ok(systemInfo);
    }
}
