// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Metadata.Tests;

/// <summary>
/// 框架元数据信息测试
/// </summary>
/// <remarks>
/// 验证 XiHanMetadata 的静态标识信息与派生信息（版本、目标框架）的契约，
/// 这些常量被日志横幅、NuGet 元数据与文档引用，写错会波及多处。
/// </remarks>
public class XiHanMetadataTests
{
    /// <summary>
    /// 标识信息与仓库约定的值一致
    /// </summary>
    [Fact]
    public void IdentityFields_MatchRepositoryConventions()
    {
        Assert.Equal("XiHan.Framework", XiHanMetadata.Name);
        Assert.Equal("曦寒框架", XiHanMetadata.DisplayName);
        Assert.Equal("XiHanFun", XiHanMetadata.Author);
        Assert.Equal("XiHanFun", XiHanMetadata.Organization);
        Assert.Equal("MIT", XiHanMetadata.License);
        Assert.StartsWith("Copyright", XiHanMetadata.Copyright);
        Assert.Contains("XiHanFun", XiHanMetadata.Copyright);
        Assert.Contains("github.com/XiHanFun/XiHan.Framework", XiHanMetadata.RepositoryUrl);
    }

    /// <summary>
    /// 版本信息从程序集派生且自洽
    /// </summary>
    [Fact]
    public void VersionFields_AreDerivedFromAssembly_AndConsistent()
    {
        // 版本由 version.props 经程序集特性写入，这里只验证自洽性与合理性
        Assert.False(string.IsNullOrWhiteSpace(XiHanMetadata.Version));
        Assert.True(XiHanMetadata.FullVersion >= new Version(3, 0, 0), "框架已进入 3.x 线");
        Assert.Equal(XiHanMetadata.FullVersion.Major, XiHanMetadata.MajorVersion);
        Assert.Equal(XiHanMetadata.FullVersion.Minor, XiHanMetadata.MinorVersion);
        Assert.Equal(XiHanMetadata.FullVersion.Build, XiHanMetadata.PatchVersion);
        Assert.Contains(XiHanMetadata.Version, XiHanMetadata.GetSummary());
    }

    /// <summary>
    /// 目标框架标识与支持平台符合声明
    /// </summary>
    [Fact]
    public void TargetFramework_And_Platforms_MatchDeclaredSupport()
    {
        Assert.Equal("net10.0", XiHanMetadata.TargetFramework);
        Assert.Contains("net10.0", XiHanMetadata.SupportedFrameworks);
        Assert.Contains("Windows", XiHanMetadata.SupportedPlatforms);
        Assert.Contains("Linux", XiHanMetadata.SupportedPlatforms);
        Assert.Contains("MacOS", XiHanMetadata.SupportedPlatforms);
        Assert.Contains("快速", XiHanMetadata.Description);
    }

    /// <summary>
    /// 摘要与详情文本包含核心标识
    /// </summary>
    [Fact]
    public void Summary_And_Details_ContainCoreIdentity()
    {
        var summary = XiHanMetadata.GetSummary();
        var details = XiHanMetadata.GetDetails();

        Assert.Contains(XiHanMetadata.Name, summary);
        Assert.Contains(XiHanMetadata.DisplayName, summary);
        Assert.Contains(XiHanMetadata.Author, details);
        Assert.Contains(XiHanMetadata.License, details);
        Assert.Contains(XiHanMetadata.DocumentationUrl, details);
        Assert.False(string.IsNullOrWhiteSpace(XiHanMetadata.Logo));
        Assert.NotEmpty(XiHanMetadata.Keywords);
        Assert.Contains("xihan", XiHanMetadata.Keywords);
    }
}
