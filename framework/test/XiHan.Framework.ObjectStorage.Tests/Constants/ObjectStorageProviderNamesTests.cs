// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Constants;

namespace XiHan.Framework.ObjectStorage.Tests.Constants;

/// <summary>
/// 对象存储提供程序名称常量测试
/// </summary>
/// <remarks>
/// 这些字面量同时出现在 appsettings 配置、注册开关的 case 分支和运行期的按名解析里，属于对外协议，
/// 改动会让存量配置静默失效，因此必须逐个锁死，不能只断言「非空」。
/// </remarks>
public class ObjectStorageProviderNamesTests
{
    /// <summary>
    /// 常量字面量不允许漂移
    /// </summary>
    [Fact]
    public void ProviderNames_Values_AreStable()
    {
        Assert.Equal("Local", ObjectStorageProviderNames.Local);
        Assert.Equal("MinIO", ObjectStorageProviderNames.Minio);
        Assert.Equal("AliyunOSS", ObjectStorageProviderNames.AliyunOss);
        Assert.Equal("TencentCOS", ObjectStorageProviderNames.TencentCos);
    }

    /// <summary>
    /// 常量的大写形式与内部注册开关的 case 分支一一对应
    /// </summary>
    /// <remarks>
    /// RegisterProvider 用 ToUpperInvariant 后再匹配 case，这里锁死归一化结果，
    /// 防止有人把常量改成带空格或下划线的形式后注册链路直接落到 default 分支抛异常。
    /// </remarks>
    [Theory]
    [InlineData("Local", "LOCAL")]
    [InlineData("MinIO", "MINIO")]
    [InlineData("AliyunOSS", "ALIYUNOSS")]
    [InlineData("TencentCOS", "TENCENTCOS")]
    public void ProviderNames_UpperInvariant_MatchesRegistrationSwitchCase(string providerName, string expectedSwitchCase)
    {
        Assert.Equal(expectedSwitchCase, providerName.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// 四个提供程序名称互不重复
    /// </summary>
    [Fact]
    public void ProviderNames_AreDistinctIgnoringCase()
    {
        string[] names =
        [
            ObjectStorageProviderNames.Local,
            ObjectStorageProviderNames.Minio,
            ObjectStorageProviderNames.AliyunOss,
            ObjectStorageProviderNames.TencentCos
        ];

        Assert.Equal(4, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
