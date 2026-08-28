// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// MinIO 存储配置测试
/// </summary>
/// <remarks>
/// 凭据类字段默认必须是空串而不是 null：Provider 构造时会直接把它们交给 SDK，
/// 默认 null 会把「没配」变成 NullReferenceException 而不是可诊断的鉴权失败。
/// </remarks>
public class MinioStorageOptionsTests
{
    /// <summary>
    /// 配置节名称固定为 XiHan:ObjectStorage:Minio
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:ObjectStorage:Minio", MinioStorageOptions.SectionName);
    }

    /// <summary>
    /// 未配置时凭据为空串、不启用 SSL、区域为空
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyCredentialsWithoutSsl()
    {
        var options = new MinioStorageOptions();

        Assert.Equal(string.Empty, options.Endpoint);
        Assert.Equal(string.Empty, options.AccessKey);
        Assert.Equal(string.Empty, options.SecretKey);
        Assert.Equal(string.Empty, options.DefaultBucket);
        Assert.False(options.UseSSL);
        Assert.Null(options.Region);
    }

    /// <summary>
    /// 所有属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        var options = new MinioStorageOptions
        {
            Endpoint = "minio.local:9000",
            AccessKey = "ak",
            SecretKey = "sk",
            DefaultBucket = "assets",
            UseSSL = true,
            Region = "cn-north-1"
        };

        Assert.Equal("minio.local:9000", options.Endpoint);
        Assert.Equal("ak", options.AccessKey);
        Assert.Equal("sk", options.SecretKey);
        Assert.Equal("assets", options.DefaultBucket);
        Assert.True(options.UseSSL);
        Assert.Equal("cn-north-1", options.Region);
    }
}
