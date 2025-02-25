using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using XiHan.Framework.Localization.Core;
using XiHan.Framework.Localization.Resources;
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
        Console.WriteLine($"控制器初始化完成，本地化器: {(_localizer != null ? "已注入" : "未注入")}");
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
        var resourceTypeName = typeof(TestResource).Name;

        // 尝试多种路径可能
        var possiblePaths = new List<string>
        {
            $"Localization/{resourceTypeName}/{resourceTypeName}.en.json",
            $"Localization/{resourceTypeName}/{resourceTypeName}.zh-CN.json",
            $"Localization/TestResource/TestResource.en.json",
            $"Localization/TestResource/TestResource.zh-CN.json",
            Path.Combine(AppContext.BaseDirectory, "Localization", "TestResource", "TestResource.en.json"),
            Path.Combine(AppContext.BaseDirectory, "Localization", "TestResource", "TestResource.zh-CN.json")
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

    [HttpGet]
    public IActionResult ValidateJsonFiles()
    {
        var results = new Dictionary<string, object>();
        var basePath = Path.Combine(AppContext.BaseDirectory, "Localization", "TestResource");

        var files = new[] {
            Path.Combine(basePath, "TestResource.en.json"),
            Path.Combine(basePath, "TestResource.zh-CN.json")
        };

        foreach (var file in files)
        {
            try
            {
                if (!System.IO.File.Exists(file))
                {
                    results[file] = new { Exists = false };
                    continue;
                }

                var content = System.IO.File.ReadAllText(file);

                // 尝试解析JSON以验证格式
                var json = System.Text.Json.JsonDocument.Parse(content);
                var rootElement = json.RootElement;

                // 检查是否包含Welcome键
                var hasWelcome = rootElement.TryGetProperty("Welcome", out var welcomeValue);

                results[file] = new
                {
                    Exists = true,
                    IsValidJson = true,
                    HasWelcomeKey = hasWelcome,
                    WelcomeValue = hasWelcome ? welcomeValue.GetString() : null,
                    ContentPreview = content[..Math.Min(200, content.Length)]
                };
            }
            catch (Exception ex)
            {
                results[file] = new
                {
                    Exists = System.IO.File.Exists(file),
                    IsValidJson = false,
                    Error = ex.Message
                };
            }
        }

        return Ok(results);
    }
}
