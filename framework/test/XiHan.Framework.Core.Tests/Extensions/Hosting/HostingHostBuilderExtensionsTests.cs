// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Extensions.Hosting;

namespace XiHan.Framework.Core.Tests.Extensions.Hosting;

/// <summary>
/// 主机构建器扩展方法测试
/// </summary>
/// <remarks>
/// 这个扩展只做一件事：把 appsettings.secrets.json 追加进主机的应用配置。
/// 路径常量对外可见（宿主会照着它放文件、写 .gitignore），逐字锁死。
/// <para>
/// 追加动作是延迟执行的，只有在 <c>Build()</c> 时才会真正跑，因此「加进去了没有」必须建完主机再验。
/// </para>
/// </remarks>
public class HostingHostBuilderExtensionsTests
{
    /// <summary>
    /// 私密配置文件的路径常量固定不变
    /// </summary>
    [Fact]
    public void AppSettingsSecretJsonPath_IsStable()
    {
        Assert.Equal(
            "appsettings.secrets.json",
            XiHan.Framework.Core.Extensions.Hosting.HostingHostBuilderExtensions.AppSettingsSecretJsonPath);
    }

    /// <summary>
    /// 扩展返回同一个构建器，可以继续链式调用
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void AddAppSettingsSecretsJson_ReturnsSameBuilderForChaining()
    {
        IHostBuilder hostBuilder = new HostBuilder();

        var returned = hostBuilder.AddAppSettingsSecretsJson();

        Assert.Same(hostBuilder, returned);
    }

    /// <summary>
    /// 建完主机之后应用配置里出现了私密配置文件这一源
    /// </summary>
    /// <remarks>
    /// 断言按扩展方法自身声明的语义写：它的职责就是把这个 JSON 源加进应用配置。
    /// 文件本身允许缺失（默认 optional 为真），因此不预先造文件，只验证源被登记。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void AddAppSettingsSecretsJson_RegistersJsonSourceInAppConfiguration()
    {
        IHostBuilder hostBuilder = new HostBuilder();
        hostBuilder.AddAppSettingsSecretsJson();

        using var host = hostBuilder.Build();
        var configuration = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();

        Assert.Contains(configuration.Providers, provider =>
            provider is JsonConfigurationProvider json
            && string.Equals(
                json.Source.Path,
                XiHan.Framework.Core.Extensions.Hosting.HostingHostBuilderExtensions.AppSettingsSecretJsonPath,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// 可以指定自定义路径与可选性
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void AddAppSettingsSecretsJson_HonorsCustomPath()
    {
        IHostBuilder hostBuilder = new HostBuilder();
        hostBuilder.AddAppSettingsSecretsJson(path: "custom.secrets.json");

        using var host = hostBuilder.Build();
        var configuration = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();

        Assert.Contains(configuration.Providers, provider =>
            provider is JsonConfigurationProvider json
            && string.Equals(json.Source.Path, "custom.secrets.json", StringComparison.Ordinal));
    }
}
