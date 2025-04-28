using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Extensions;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Web.Test.Controllers;

/// <summary>
/// 本地化测试控制器
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "v1")]
public class LocalizationTestController : ControllerBase
{
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ILocalizationResourceManager _resourceManager;
    private readonly LocalizationCacheManager _cacheManager;
    private readonly IVirtualFileSystem _virtualFileSystem;
    private readonly IOptions<XiHanLocalizationOptions> _localizationOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LocalizationTestController(
        IStringLocalizerFactory localizerFactory,
        ILocalizationResourceManager resourceManager,
        LocalizationCacheManager cacheManager,
        IVirtualFileSystem virtualFileSystem,
        IOptions<XiHanLocalizationOptions> localizationOptions)
    {
        _localizerFactory = localizerFactory;
        _resourceManager = resourceManager;
        _cacheManager = cacheManager;
        _virtualFileSystem = virtualFileSystem;
        _localizationOptions = localizationOptions;
    }

    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    /// <returns>支持的语言列表</returns>
    [HttpGet]
    public IActionResult GetSupportedLanguages()
    {
        return Ok(new
        {
            _localizationOptions.Value.SupportedCultures,
            _localizationOptions.Value.DefaultCulture
        });
    }

    /// <summary>
    /// 获取本地化字符串（使用当前语言文化）
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="culture">文化（可选）</param>
    /// <returns>本地化字符串</returns>
    [HttpGet]
    public IActionResult GetString(string key, string? culture = null)
    {
        var localizer = HttpContext.RequestServices.GetXiHanLocalizer("TestResource");
        var result = string.IsNullOrEmpty(culture) ? localizer[key] : localizer.GetWithCulture(key, culture);
        return Ok(new
        {
            Key = key,
            result.Value,
            Culture = culture ?? CultureInfo.CurrentUICulture.Name,
            result.ResourceNotFound
        });
    }

    /// <summary>
    /// 获取带格式化参数的本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="culture">文化（可选）</param>
    /// <returns>格式化后的本地化字符串</returns>
    [HttpGet]
    public IActionResult GetFormattedString(string key, string? culture = null)
    {
        var localizer = HttpContext.RequestServices.GetXiHanLocalizer("TestResource");
        var parameters = new object[] { DateTime.Now, "XiHan Framework" };
        var result = string.IsNullOrEmpty(culture) ? localizer[key, parameters] : localizer.GetWithCulture(key, culture, parameters);
        return Ok(new
        {
            Key = key,
            result.Value,
            Culture = culture ?? CultureInfo.CurrentUICulture.Name,
            Parameters = parameters,
            result.ResourceNotFound
        });
    }

    /// <summary>
    /// 获取资源的所有本地化字符串
    /// </summary>
    /// <param name="culture">文化（可选）</param>
    /// <returns>所有本地化字符串</returns>
    [HttpGet]
    public IActionResult GetAllStrings(string? culture = null)
    {
        var localizer = HttpContext.RequestServices.GetXiHanLocalizer("TestResource");

        if (string.IsNullOrEmpty(culture))
        {
            return Ok(localizer.GetAllStrings());
        }

        var oldCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        try
        {
            return Ok(localizer.GetAllStrings());
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    /// <param name="culture">文化</param>
    /// <returns>当前文化信息</returns>
    [HttpGet]
    public IActionResult SwitchLanguage(string culture)
    {
        if (!_localizationOptions.Value.SupportedCultures.Contains(culture))
        {
            return BadRequest($"不支持的语言文化: {culture}");
        }

        var oldCulture = CultureInfo.CurrentUICulture.Name;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        return Ok(new
        {
            OldCulture = oldCulture,
            CurrentCulture = CultureInfo.CurrentUICulture.Name,
            CurrentCultureDisplayName = CultureInfo.CurrentUICulture.DisplayName
        });
    }

    /// <summary>
    /// 读取资源文件
    /// </summary>
    /// <returns>资源文件列表</returns>
    [HttpGet]
    public IActionResult ReadResourceFiles()
    {
        var resourcePath = _localizationOptions.Value.ResourcesPath;

        // 使用GetDirectoryContents方法获取目录内容
        var files = _virtualFileSystem.GetDirectoryContents(resourcePath)
            .Where(f => f is { Exists: true, IsDirectory: false } && f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(f => new
            {
                f.PhysicalPath,
                f.Name,
                f.Length,
                f.LastModified
            })
            .ToList();

        return Ok(new
        {
            ResourcePath = resourcePath,
            Files = files
        });
    }

    /// <summary>
    /// 直接读取资源内容
    /// </summary>
    /// <returns>资源内容</returns>
    [HttpGet]
    public async Task<IActionResult> ReadResourceDirect()
    {
        var resourcePath = _localizationOptions.Value.ResourcesPath;
        var results = new List<object>();

        // 使用GetDirectoryContents方法获取资源文件
        foreach (var file in _virtualFileSystem.GetDirectoryContents(resourcePath)
                    .Where(f => f is { Exists: true, IsDirectory: false } && f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var stream = file.CreateReadStream();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                results.Add(new
                {
                    file.PhysicalPath,
                    Content = content
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    file.PhysicalPath,
                    Error = ex.Message
                });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// 检查资源名称
    /// </summary>
    /// <returns>已注册的资源名称</returns>
    [HttpGet]
    public IActionResult CheckResourceNames()
    {
        var resources = _resourceManager.GetResources();

        var resourceInfo = resources.Select(r => new
        {
            Name = r.ResourceName,
            Type = r.GetType().Name,
            SupportedCultures = r.GetSupportedCultures()
        }).ToList();

        return Ok(resourceInfo);
    }

    /// <summary>
    /// 简单字符串本地化测试
    /// </summary>
    /// <param name="key">资源键</param>
    /// <returns>本地化字符串</returns>
    [HttpGet]
    public IActionResult GetSimpleString([FromQuery] string key = "Welcome")
    {
        // 创建可本地化字符串
        var localizableString = new LocalizableString(key, "TestResource");
        var localizedValue = localizableString.Localize(HttpContext.RequestServices);

        // 使用缓存获取本地化字符串 (演示目的)
        var localizer = HttpContext.RequestServices.GetXiHanLocalizer("TestResource");
        var cachedValue = _cacheManager.GetOrAdd(
            "TestResource",
            CultureInfo.CurrentUICulture.Name,
            key,
            () => localizer[key]
        );

        return Ok(new
        {
            Key = key,
            LocalizedValue = localizedValue,
            CachedValue = cachedValue.Value,
            Culture = CultureInfo.CurrentUICulture.Name
        });
    }
}
