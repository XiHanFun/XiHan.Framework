// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Constants;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// 对象存储总配置测试
/// </summary>
/// <remarks>
/// 这份选项决定「零配置起步时用哪个 Provider」以及「路由键怎么匹配」。
/// 路由映射字典的比较器是大小写不敏感的，属于对外可见语义（配置文件里写 Avatar 还是 avatar 都要命中），
/// 必须显式断言，不能只测字典里有没有这个 key。
/// </remarks>
public class XiHanObjectStorageOptionsTests
{
    /// <summary>
    /// 配置节名称固定为 XiHan:ObjectStorage
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:ObjectStorage", XiHanObjectStorageOptions.SectionName);
    }

    /// <summary>
    /// 零配置时默认且只启用本地存储
    /// </summary>
    [Fact]
    public void Defaults_FallBackToLocalProvider()
    {
        var options = new XiHanObjectStorageOptions();

        Assert.Equal(ObjectStorageProviderNames.Local, options.DefaultProvider);
        Assert.Equal(new[] { ObjectStorageProviderNames.Local }, options.EnabledProviders);
    }

    /// <summary>
    /// 零配置时路由映射为空且不启用严格匹配
    /// </summary>
    [Fact]
    public void Defaults_HaveEmptyRouteMappingsAndLooseMatch()
    {
        var options = new XiHanObjectStorageOptions();

        Assert.NotNull(options.RouteProviderMappings);
        Assert.Empty(options.RouteProviderMappings);
        Assert.False(options.StrictRouteMatch);
    }

    /// <summary>
    /// 路由映射按大小写不敏感匹配
    /// </summary>
    [Fact]
    public void RouteProviderMappings_LookupIsCaseInsensitive()
    {
        var options = new XiHanObjectStorageOptions();
        options.RouteProviderMappings["Avatar"] = ObjectStorageProviderNames.Minio;

        Assert.True(options.RouteProviderMappings.TryGetValue("AVATAR", out var byUpperCase));
        Assert.Equal(ObjectStorageProviderNames.Minio, byUpperCase);
        Assert.True(options.RouteProviderMappings.TryGetValue("avatar", out var byLowerCase));
        Assert.Equal(ObjectStorageProviderNames.Minio, byLowerCase);
    }

    /// <summary>
    /// 大小写不同的同名路由键会互相覆盖而不是并存
    /// </summary>
    [Fact]
    public void RouteProviderMappings_SameKeyDifferentCase_Overwrites()
    {
        var options = new XiHanObjectStorageOptions();
        options.RouteProviderMappings["Avatar"] = ObjectStorageProviderNames.Minio;
        options.RouteProviderMappings["avatar"] = ObjectStorageProviderNames.AliyunOss;

        Assert.Equal(1, options.RouteProviderMappings.Count);
        Assert.Equal(ObjectStorageProviderNames.AliyunOss, options.RouteProviderMappings["Avatar"]);
    }

    /// <summary>
    /// 所有属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = ObjectStorageProviderNames.Minio,
            EnabledProviders = [ObjectStorageProviderNames.Local, ObjectStorageProviderNames.Minio],
            StrictRouteMatch = true
        };

        Assert.Equal(ObjectStorageProviderNames.Minio, options.DefaultProvider);
        Assert.Equal(2, options.EnabledProviders.Length);
        Assert.True(options.StrictRouteMatch);
    }
}
