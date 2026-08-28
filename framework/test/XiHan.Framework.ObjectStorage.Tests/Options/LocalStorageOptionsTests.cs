// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// 本地存储配置测试
/// </summary>
/// <remarks>
/// 默认值直接决定「未配置时文件落在哪、直链长什么样」，而 SectionName 决定配置能否被绑上，
/// 二者都属于对外契约。
/// </remarks>
public class LocalStorageOptionsTests
{
    /// <summary>
    /// 配置节名称固定为 XiHan:ObjectStorage:Local
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:ObjectStorage:Local", LocalStorageOptions.SectionName);
    }

    /// <summary>
    /// 默认根目录落在 wwwroot 下，默认直链前缀为 /uploads
    /// </summary>
    [Fact]
    public void Defaults_PointToWebRootUploads()
    {
        var options = new LocalStorageOptions();

        Assert.Equal("wwwroot/Uploads", options.RootPath);
        Assert.Equal("/uploads", options.UrlPrefix);
    }

    /// <summary>
    /// 两个属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        var options = new LocalStorageOptions
        {
            RootPath = "/data/files",
            UrlPrefix = "/static"
        };

        Assert.Equal("/data/files", options.RootPath);
        Assert.Equal("/static", options.UrlPrefix);
    }
}
