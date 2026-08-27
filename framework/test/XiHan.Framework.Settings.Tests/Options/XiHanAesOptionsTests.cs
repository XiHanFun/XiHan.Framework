// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Settings.Options;

namespace XiHan.Framework.Settings.Tests.Options;

/// <summary>
/// 曦寒 Aes 选项测试
/// </summary>
/// <remarks>
/// 节名会被写进 appsettings，属于对外契约，一旦改动所有部署的密钥配置全部失效，必须锁死。
/// 默认空密钥同样是契约：设置管理器据此 fail-closed 拒绝加密操作，绝不能改成内置占位密钥。
/// </remarks>
public class XiHanAesOptionsTests
{
    /// <summary>
    /// 配置节名称保持稳定
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Settings:Aes", XiHanAesOptions.SectionName);
    }

    /// <summary>
    /// 未配置时密钥与向量都是空串而不是 null
    /// </summary>
    [Fact]
    public void Ctor_LeavesKeyAndIvEmptyRatherThanNull()
    {
        var options = new XiHanAesOptions();

        Assert.Equal(string.Empty, options.Key);
        Assert.Equal(string.Empty, options.Iv);
    }

    /// <summary>
    /// 两个属性都可写，支持代码内直接赋值
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var options = new XiHanAesOptions
        {
            Key = "the-key",
            Iv = "the-iv"
        };

        Assert.Equal("the-key", options.Key);
        Assert.Equal("the-iv", options.Iv);
    }

    /// <summary>
    /// 选项能从约定节名绑定出来
    /// </summary>
    [Fact]
    public void SectionName_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanAesOptions.SectionName}:Key"] = "bound-key",
                [$"{XiHanAesOptions.SectionName}:Iv"] = "bound-iv"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanAesOptions>(configuration.GetSection(XiHanAesOptions.SectionName));
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanAesOptions>>().Value;

        Assert.Equal("bound-key", options.Key);
        Assert.Equal("bound-iv", options.Iv);
    }
}
