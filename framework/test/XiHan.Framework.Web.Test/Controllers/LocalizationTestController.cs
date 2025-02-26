using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Provider;
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.Web.Test.Localization;

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
    private readonly IXiHanStringLocalizer _localizer;
    private readonly ILocalizationResourceManager _resourceManager;
    private readonly IStringLocalizerFactory _factory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="factory">本地化器工厂</param>
    /// <param name="resourceManager">资源管理器</param>
    public LocalizationTestController(
        IStringLocalizerFactory factory,
        ILocalizationResourceManager resourceManager)
    {
        _factory = factory;
        _resourceManager = resourceManager;

        // 获取XiHan自定义的本地化器实现
        _localizer = (IXiHanStringLocalizer)factory.Create(typeof(TestResource));
    }

    /// <summary>
    /// 获取当前支持的语言
    /// </summary>
    /// <returns>支持的语言列表</returns>
    [HttpGet]
    public IActionResult GetSupportedLanguages()
    {
        var cultures = _localizer.GetSupportedCultures();
        return Ok(new
        {
            Cultures = cultures,
            CurrentCulture = CultureInfo.CurrentCulture.Name,
            CurrentUICulture = CultureInfo.CurrentUICulture.Name
        });
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="culture">文化代码，如：en、zh-CN等</param>
    /// <returns>本地化字符串</returns>
    [HttpGet]
    public IActionResult GetString(string key, string? culture = null)
    {
        var result = string.IsNullOrEmpty(culture) ? _localizer[key] : _localizer.GetWithCulture(key, culture);
        return Ok(new
        {
            Key = key,
            result.Value,
            result.ResourceNotFound,
            Culture = string.IsNullOrEmpty(culture) ? CultureInfo.CurrentUICulture.Name : culture
        });
    }

    /// <summary>
    /// 获取带参数的本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="culture">文化代码</param>
    /// <returns>格式化后的本地化字符串</returns>
    [HttpGet]
    public IActionResult GetFormattedString(string key, string? culture = null)
    {
        LocalizedString result;
        var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        result = string.IsNullOrEmpty(culture) ? _localizer[key, currentTime] : _localizer.GetWithCulture(key, culture, currentTime);

        return Ok(new
        {
            Key = key,
            result.Value,
            result.ResourceNotFound,
            Culture = string.IsNullOrEmpty(culture) ? CultureInfo.CurrentUICulture.Name : culture,
            Parameter = currentTime
        });
    }

    /// <summary>
    /// 获取所有本地化字符串
    /// </summary>
    /// <param name="culture">文化代码</param>
    /// <returns>所有本地化字符串</returns>
    [HttpGet]
    public IActionResult GetAllStrings(string? culture = null)
    {
        if (!string.IsNullOrEmpty(culture))
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
        }

        var cultures = _localizer.GetSupportedCultures();
        Console.WriteLine($"支持的文化: {string.Join(", ", cultures)}");
        Console.WriteLine($"当前UI文化: {CultureInfo.CurrentUICulture.Name}");

        var strings = _localizer.GetAllStrings(includeParentCultures: true).ToList();
        Console.WriteLine($"找到本地化字符串数量: {strings.Count}");

        var resourcePath = _localizer.GetResourceBasePath();
        Console.WriteLine($"资源基础路径: {resourcePath}");

        return Ok(new
        {
            Culture = CultureInfo.CurrentUICulture.Name,
            Strings = strings.Select(s => new { s.Name, s.Value, s.ResourceNotFound }),
            SupportedCultures = cultures,
            ResourceBasePath = resourcePath
        });
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    /// <param name="culture">文化代码</param>
    /// <returns>切换结果</returns>
    [HttpGet]
    public IActionResult SwitchLanguage(string culture)
    {
        if (string.IsNullOrEmpty(culture))
        {
            return BadRequest("文化代码不能为空");
        }

        try
        {
            var cultureInfo = new CultureInfo(culture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            return Ok(new
            {
                Success = true,
                NewCulture = cultureInfo.Name,
                cultureInfo.DisplayName,
                cultureInfo.NativeName
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ReadResourceFiles([FromServices] IVirtualFileSystem fileSystem)
    {
        var results = new Dictionary<string, object>();

        // 获取TestResource的类型名称
        _ = typeof(TestResource).Name;

        // 尝试多种路径可能
        var possiblePaths = new List<string>
        {
            $"TestResource/TestResource.en.json",
            $"TestResource/TestResource.zh-CN.json",
        };

        foreach (var path in possiblePaths)
        {
            Console.WriteLine($"尝试虚拟文件系统获取: {path}");
            var file = fileSystem.GetFile(path);
            results[path] = new
            {
                file.Exists,
                Length = file.Exists ? file.Length : 0
            };

            if (file.Exists)
            {
                try
                {
                    using var stream = file.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    results[$"{path}_内容"] = content[..Math.Min(100, content.Length)] + "...";
                }
                catch (Exception ex)
                {
                    results[$"{path}_错误"] = ex.Message;
                }
            }
        }

        return Ok(results);
    }

    [HttpGet]
    public IActionResult ReadResourceDirect()
    {
        try
        {
            var results = new Dictionary<string, object>();
            var basePath = Path.Combine(AppContext.BaseDirectory, "Localization", "TestResource");

            var files = new[] {
                Path.Combine(basePath, "TestResource.en.json"),
                Path.Combine(basePath, "TestResource.zh-CN.json")
            };

            foreach (var file in files)
            {
                if (!System.IO.File.Exists(file))
                {
                    results[file] = new { Exists = false };
                    continue;
                }

                var content = System.IO.File.ReadAllText(file);
                results[file] = new
                {
                    Exists = true,
                    ContentLength = content.Length,
                    ContentPreview = content[..Math.Min(100, content.Length)]
                };
            }

            // 直接检查资源字符串提供者
            var resourceProvider = HttpContext.RequestServices.GetRequiredService<IResourceStringProvider>();
            var resource = HttpContext.RequestServices.GetRequiredService<TestResource>();

            // 手动调用获取字符串方法
            var welcomeEn = resourceProvider.GetString(resource, "Welcome", "en");
            var welcomeZh = resourceProvider.GetString(resource, "Welcome", "zh-CN");

            results["DirectResourceTest"] = new
            {
                EnglishWelcome = welcomeEn,
                ChineseWelcome = welcomeZh
            };

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, ex.StackTrace });
        }
    }

    [HttpGet]
    public IActionResult CheckResourceNames()
    {
        var allResources = _resourceManager.GetResources();
        var resourceNames = allResources.Select(r => r.ResourceName).ToList();

        var typeLocalizer = HttpContext.RequestServices.GetService<IXiHanStringLocalizer>();
        var testStr = typeLocalizer?["Welcome"].Value ?? "未找到";

        var results = new Dictionary<string, object>
        {
            { "RegisteredResources", resourceNames },
            { "ResourceCount", resourceNames.Count },
            { "TypeLocalizerTest", testStr }
        };

        // 测试不同方式获取资源
        var factory = HttpContext.RequestServices.GetRequiredService<IStringLocalizerFactory>();
        try
        {
            foreach (var name in resourceNames)
            {
                var localizer = factory.Create(name, "");
                results[$"Resource_{name}"] = new
                {
                    Welcome = localizer["Welcome"].Value,
                    Found = !localizer["Welcome"].ResourceNotFound
                };
            }
        }
        catch (Exception ex)
        {
            results["FactoryError"] = ex.Message;
        }

        return Ok(results);
    }

    [HttpGet]
    public IActionResult GetSimpleString(string key = "Welcome")
    {
        // 使用标准的字符串本地化器方式
        var localizedString = _localizer[key];
        return Ok(new
        {
            Key = key,
            localizedString.Value,
            localizedString.ResourceNotFound,
            CurrentCulture = CultureInfo.CurrentUICulture.Name
        });
    }
}
