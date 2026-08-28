// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using System.Text;
using XiHan.Framework.Core.Extensions.Configuration;

namespace XiHan.Framework.Core.Tests.Extensions.Configuration;

/// <summary>
/// 配置帮助类测试
/// </summary>
/// <remarks>
/// 这个帮助类固定了整个框架的配置源叠放顺序：基础文件 → 私密文件 → 环境专属文件 → 环境变量 → 命令行 → 调用方追加。
/// 顺序即优先级，后加的覆盖先加的；任何一层错位都会让"环境变量覆盖不了配置文件"这类问题在部署时才暴露，
/// 因此用例逐层用同一个键做覆盖对照，而不是各测各的。
/// 真实文件写在系统临时目录下的独立子目录里，用例结束递归删除。
/// </remarks>
public sealed class ConfigurationHelperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _builtConfigurations = [];

    /// <summary>
    /// 构造函数，建立本用例专属的临时目录
    /// </summary>
    public ConfigurationHelperTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// 释放，先关掉配置根（会带上文件变更监听），再递归删除临时目录
    /// </summary>
    public void Dispose()
    {
        foreach (var configuration in _builtConfigurations)
        {
            try
            {
                configuration.Dispose();
            }
            catch
            {
                // 忽略：配置根已被提前释放
            }
        }

        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // 忽略：文件变更监听可能仍持有目录句柄
        }
    }

    /// <summary>
    /// 读取基础配置文件里的键
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_ReadsBaseJsonFile()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Name": "基础配置", "Shared": "来自基础" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions { BasePath = _root });

        Assert.Equal("基础配置", configuration["Sample:Name"]);
        Assert.Equal("来自基础", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 私密文件叠在基础文件之上
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_SecretsFileOverridesBaseFile()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");
        WriteJson("appsettings.secrets.json", """{ "Sample": { "Shared": "来自私密" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions { BasePath = _root });

        Assert.Equal("来自私密", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 环境专属文件叠在私密文件之上
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_EnvironmentFileOverridesSecretsFile()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");
        WriteJson("appsettings.secrets.json", """{ "Sample": { "Shared": "来自私密" } }""");
        WriteJson("appsettings.Staging.json", """{ "Sample": { "Shared": "来自环境" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions
        {
            BasePath = _root,
            EnvironmentName = "Staging"
        });

        Assert.Equal("来自环境", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 未指定环境名时不加载任何环境专属文件
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_WithoutEnvironmentName_IgnoresEnvironmentFile()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");
        WriteJson("appsettings.Staging.json", """{ "Sample": { "Shared": "来自环境" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions { BasePath = _root });

        Assert.Equal("来自基础", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 环境专属文件缺失时不影响构建
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_WhenEnvironmentFileMissing_StillBuilds()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions
        {
            BasePath = _root,
            EnvironmentName = "Staging"
        });

        Assert.Equal("来自基础", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 命令行参数叠在所有文件之上
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_CommandLineArgsOverrideFiles()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");
        WriteJson("appsettings.Staging.json", """{ "Sample": { "Shared": "来自环境" } }""");

        var configuration = Build(new XiHanConfigurationBuilderOptions
        {
            BasePath = _root,
            EnvironmentName = "Staging",
            CommandLineArgs = ["--Sample:Shared=来自命令行"]
        });

        Assert.Equal("来自命令行", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 带前缀的环境变量被读入并剥掉前缀
    /// </summary>
    /// <remarks>
    /// 前缀取随机值，避免与并行用例或宿主环境里的同名变量互相干扰，用完立即清除。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_ReadsPrefixedEnvironmentVariables()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");

        var prefix = "XIHANTEST" + Guid.NewGuid().ToString("N")[..8] + "_";
        var variableName = prefix + "Sample__Shared";
        Environment.SetEnvironmentVariable(variableName, "来自环境变量");

        try
        {
            var configuration = Build(new XiHanConfigurationBuilderOptions
            {
                BasePath = _root,
                EnvironmentVariablesPrefix = prefix
            });

            Assert.Equal("来自环境变量", configuration["Sample:Shared"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    /// <summary>
    /// 调用方追加的配置源排在最后，优先级最高
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_BuilderActionRunsLast()
    {
        WriteJson("appsettings.json", """{ "Sample": { "Shared": "来自基础" } }""");

        var configuration = Build(
            new XiHanConfigurationBuilderOptions
            {
                BasePath = _root,
                CommandLineArgs = ["--Sample:Shared=来自命令行"]
            },
            builder => builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sample:Shared"] = "来自调用方"
            }));

        Assert.Equal("来自调用方", configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 基础路径为空时回填成当前工作目录
    /// </summary>
    /// <remarks>
    /// 回填是对传入选项对象的原地修改，调用方之后能读到被补上的值，
    /// 这个副作用会影响复用同一份选项的第二次构建，属于对外可见行为。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_WhenBasePathEmpty_FillsCurrentDirectory()
    {
        XiHanConfigurationBuilderOptions options = new();

        Build(options);

        Assert.Equal(Directory.GetCurrentDirectory(), options.BasePath);
    }

    /// <summary>
    /// 完全不给选项时按默认值构建且不抛错
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_WithoutOptions_BuildsWithDefaults()
    {
        var configuration = Build(null);

        Assert.NotNull(configuration);
        Assert.NotEmpty(configuration.Providers);
    }

    /// <summary>
    /// 基础配置文件缺失时不抛错，得到一份没有该键的配置
    /// </summary>
    /// <remarks>
    /// 可选为真是硬约定：控制台工具、单元测试宿主往往没有 appsettings.json，
    /// 这里缺文件就炸会让框架完全无法在这些场景里使用。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void BuildConfiguration_WhenBaseFileMissing_DoesNotThrow()
    {
        var configuration = Build(new XiHanConfigurationBuilderOptions { BasePath = _root });

        Assert.Null(configuration["Sample:Shared"]);
    }

    /// <summary>
    /// 构建配置并登记待释放
    /// </summary>
    /// <param name="options">配置生成器选项</param>
    /// <param name="builderAction">调用方追加动作</param>
    /// <returns>配置根</returns>
    private IConfigurationRoot Build(XiHanConfigurationBuilderOptions? options, Action<IConfigurationBuilder>? builderAction = null)
    {
        var configuration = ConfigurationHelper.BuildConfiguration(options, builderAction);
        if (configuration is IDisposable disposable)
        {
            _builtConfigurations.Add(disposable);
        }

        return configuration;
    }

    /// <summary>
    /// 往临时目录里写一个 JSON 文件
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="content">文件内容</param>
    private void WriteJson(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_root, fileName), content, new UTF8Encoding(false));
    }
}
