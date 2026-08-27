// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// 腾讯云 COS 存储配置测试
/// </summary>
public class TencentCosStorageOptionsTests
{
    /// <summary>
    /// 配置节名称固定为 XiHan:ObjectStorage:TencentCos
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:ObjectStorage:TencentCos", TencentCosStorageOptions.SectionName);
    }

    /// <summary>
    /// 未配置时凭据与地域为空串、无 CDN 域名
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyCredentials()
    {
        var options = new TencentCosStorageOptions();

        Assert.Equal(string.Empty, options.SecretId);
        Assert.Equal(string.Empty, options.SecretKey);
        Assert.Equal(string.Empty, options.AppId);
        Assert.Equal(string.Empty, options.Region);
        Assert.Equal(string.Empty, options.DefaultBucket);
        Assert.Null(options.CdnDomain);
    }

    /// <summary>
    /// 所有属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        var options = new TencentCosStorageOptions
        {
            SecretId = "id",
            SecretKey = "key",
            AppId = "1250000000",
            Region = "ap-guangzhou",
            DefaultBucket = "assets",
            CdnDomain = "https://cdn.example.com"
        };

        Assert.Equal("id", options.SecretId);
        Assert.Equal("key", options.SecretKey);
        Assert.Equal("1250000000", options.AppId);
        Assert.Equal("ap-guangzhou", options.Region);
        Assert.Equal("assets", options.DefaultBucket);
        Assert.Equal("https://cdn.example.com", options.CdnDomain);
    }
}
