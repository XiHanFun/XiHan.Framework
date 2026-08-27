// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// 阿里云 OSS 存储配置测试
/// </summary>
public class AliyunOssStorageOptionsTests
{
    /// <summary>
    /// 配置节名称固定为 XiHan:ObjectStorage:AliyunOss
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:ObjectStorage:AliyunOss", AliyunOssStorageOptions.SectionName);
    }

    /// <summary>
    /// 未配置时凭据为空串、默认走公网、无 CDN 域名
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyCredentialsOnPublicNetwork()
    {
        var options = new AliyunOssStorageOptions();

        Assert.Equal(string.Empty, options.AccessKeyId);
        Assert.Equal(string.Empty, options.AccessKeySecret);
        Assert.Equal(string.Empty, options.Endpoint);
        Assert.Equal(string.Empty, options.DefaultBucket);
        Assert.Null(options.CdnDomain);
        Assert.False(options.UseInternal);
    }

    /// <summary>
    /// 所有属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        var options = new AliyunOssStorageOptions
        {
            AccessKeyId = "id",
            AccessKeySecret = "secret",
            Endpoint = "oss-cn-hangzhou.aliyuncs.com",
            DefaultBucket = "assets",
            CdnDomain = "https://cdn.example.com",
            UseInternal = true
        };

        Assert.Equal("id", options.AccessKeyId);
        Assert.Equal("secret", options.AccessKeySecret);
        Assert.Equal("oss-cn-hangzhou.aliyuncs.com", options.Endpoint);
        Assert.Equal("assets", options.DefaultBucket);
        Assert.Equal("https://cdn.example.com", options.CdnDomain);
        Assert.True(options.UseInternal);
    }
}
