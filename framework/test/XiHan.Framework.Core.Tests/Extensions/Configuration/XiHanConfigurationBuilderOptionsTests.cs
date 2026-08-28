// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Core.Extensions.Configuration;

namespace XiHan.Framework.Core.Tests.Extensions.Configuration;

/// <summary>
/// 曦寒配置生成器选项测试
/// </summary>
/// <remarks>
/// 三个只读属性（文件名、是否可选、变更重载）是整个配置装配的硬约定：
/// 文件名决定了所有宿主都必须叫 appsettings，可选为真决定了缺文件不会炸启动，变更重载为真决定了热更新可用。
/// 它们刻意不给 setter，用例连「没有 setter」这件事一起锁死，防止后来者顺手放开导致宿主之间行为分叉。
/// </remarks>
public class XiHanConfigurationBuilderOptionsTests
{
    /// <summary>
    /// 三个只读属性的默认值固定不变
    /// </summary>
    [Fact]
    public void ReadOnlyDefaults_AreStable()
    {
        var options = new XiHanConfigurationBuilderOptions();

        Assert.Equal("appsettings", options.FileName);
        Assert.True(options.Optional);
        Assert.True(options.ReloadOnChange);
    }

    /// <summary>
    /// 三个只读属性没有公开 setter
    /// </summary>
    [Fact]
    public void ReadOnlyProperties_HaveNoPublicSetter()
    {
        var type = typeof(XiHanConfigurationBuilderOptions);

        Assert.Null(type.GetProperty(nameof(XiHanConfigurationBuilderOptions.FileName))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(XiHanConfigurationBuilderOptions.Optional))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(XiHanConfigurationBuilderOptions.ReloadOnChange))!.SetMethod);
    }

    /// <summary>
    /// 可写属性的默认值全部为空，表示"不启用"
    /// </summary>
    [Fact]
    public void WritableProperties_DefaultToNull()
    {
        var options = new XiHanConfigurationBuilderOptions();

        Assert.Null(options.UserSecretsAssembly);
        Assert.Null(options.UserSecretsId);
        Assert.Null(options.EnvironmentName);
        Assert.Null(options.BasePath);
        Assert.Null(options.EnvironmentVariablesPrefix);
        Assert.Null(options.CommandLineArgs);
    }

    /// <summary>
    /// 可写属性能被宿主逐项设置
    /// </summary>
    [Fact]
    public void WritableProperties_AreSettable()
    {
        var assembly = typeof(XiHanConfigurationBuilderOptionsTests).Assembly;

        var options = new XiHanConfigurationBuilderOptions
        {
            UserSecretsAssembly = assembly,
            UserSecretsId = "xihan-secrets",
            EnvironmentName = "Development",
            BasePath = "D:/config",
            EnvironmentVariablesPrefix = "XIHAN_",
            CommandLineArgs = ["--Sample:Name=命令行"]
        };

        Assert.Same(assembly, options.UserSecretsAssembly);
        Assert.Equal("xihan-secrets", options.UserSecretsId);
        Assert.Equal("Development", options.EnvironmentName);
        Assert.Equal("D:/config", options.BasePath);
        Assert.Equal("XIHAN_", options.EnvironmentVariablesPrefix);

        var commandLineArgs = Assert.Single(options.CommandLineArgs!);
        Assert.Equal("--Sample:Name=命令行", commandLineArgs);
    }

    /// <summary>
    /// 用户密钥的两种来源都保留，由使用方决定优先级
    /// </summary>
    /// <remarks>
    /// 选项自己不做二选一裁决：同时设置时由 <c>ConfigurationHelper</c> 按"标识优先于程序集"取舍，
    /// 这里只确认选项如实保存两者，避免把裁决逻辑误挪到数据类里。
    /// </remarks>
    [Fact]
    public void UserSecretsSources_AreBothStoredWithoutArbitration()
    {
        var options = new XiHanConfigurationBuilderOptions
        {
            UserSecretsAssembly = typeof(XiHanConfigurationBuilderOptionsTests).Assembly,
            UserSecretsId = "xihan-secrets"
        };

        Assert.NotNull(options.UserSecretsAssembly);
        Assert.NotNull(options.UserSecretsId);
    }

    /// <summary>
    /// 选项是纯数据类，没有额外的公开方法
    /// </summary>
    [Fact]
    public void Type_ExposesNoBehaviourMethods()
    {
        var declaredMethods = typeof(XiHanConfigurationBuilderOptions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.Empty(declaredMethods);
    }
}
