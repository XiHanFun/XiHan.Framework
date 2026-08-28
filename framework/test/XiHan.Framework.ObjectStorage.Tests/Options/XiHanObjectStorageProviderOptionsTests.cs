// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;
using XiHan.Framework.ObjectStorage.Tests.Fakes;

namespace XiHan.Framework.ObjectStorage.Tests.Options;

/// <summary>
/// 对象存储提供程序注册表测试
/// </summary>
/// <remarks>
/// 这张注册表是「提供程序名称 → 实现类型」的唯一真相来源，管理器完全依赖它做解析。
/// 名称去空白、大小写不敏感、类型必须实现 <see cref="IFileStorageProvider"/> 三条约束都在这里兜底，
/// 一旦松动就会在运行期变成难定位的解析失败。
/// </remarks>
public class XiHanObjectStorageProviderOptionsTests
{
    /// <summary>
    /// 新建的注册表为空
    /// </summary>
    [Fact]
    public void ProviderTypes_WhenNothingRegistered_IsEmpty()
    {
        var options = new XiHanObjectStorageProviderOptions();

        Assert.NotNull(options.ProviderTypes);
        Assert.Empty(options.ProviderTypes);
    }

    /// <summary>
    /// 注册后可按任意大小写取回实现类型
    /// </summary>
    [Fact]
    public void AddProvider_WhenRegistered_LookupIsCaseInsensitive()
    {
        var options = new XiHanObjectStorageProviderOptions();

        options.AddProvider("Local", typeof(LocalFileStorageProvider));

        Assert.True(options.ProviderTypes.TryGetValue("LOCAL", out var byUpperCase));
        Assert.Equal(typeof(LocalFileStorageProvider), byUpperCase);
        Assert.True(options.ProviderTypes.TryGetValue("local", out var byLowerCase));
        Assert.Equal(typeof(LocalFileStorageProvider), byLowerCase);
    }

    /// <summary>
    /// 注册时名称两端的空白被裁掉
    /// </summary>
    [Fact]
    public void AddProvider_WithPaddedName_StoresTrimmedKey()
    {
        var options = new XiHanObjectStorageProviderOptions();

        options.AddProvider("  Custom  ", typeof(RecordingFileStorageProvider));

        Assert.True(options.ProviderTypes.ContainsKey("Custom"));
        Assert.Equal(typeof(RecordingFileStorageProvider), options.ProviderTypes["Custom"]);
    }

    /// <summary>
    /// 同名重复注册以后者为准，且不会产生两条记录
    /// </summary>
    [Fact]
    public void AddProvider_WhenNameRepeats_LastRegistrationWins()
    {
        var options = new XiHanObjectStorageProviderOptions();

        options.AddProvider("Local", typeof(RecordingFileStorageProvider));
        options.AddProvider("local", typeof(AlternateFileStorageProvider));

        Assert.Single(options.ProviderTypes);
        Assert.Equal(typeof(AlternateFileStorageProvider), options.ProviderTypes["Local"]);
    }

    /// <summary>
    /// 提供程序名称为 null 时抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddProvider_WhenNameNull_ThrowsArgumentNullException()
    {
        var options = new XiHanObjectStorageProviderOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddProvider(null!, typeof(RecordingFileStorageProvider)));
    }

    /// <summary>
    /// 提供程序名称为空或纯空白时抛 ArgumentException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddProvider_WhenNameBlank_ThrowsArgumentException(string providerName)
    {
        var options = new XiHanObjectStorageProviderOptions();

        Assert.Throws<ArgumentException>(() => options.AddProvider(providerName, typeof(RecordingFileStorageProvider)));
    }

    /// <summary>
    /// 实现类型为 null 时抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddProvider_WhenTypeNull_ThrowsArgumentNullException()
    {
        var options = new XiHanObjectStorageProviderOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddProvider("Custom", null!));
    }

    /// <summary>
    /// 实现类型未实现存储接口时抛 InvalidOperationException 并点名接口
    /// </summary>
    [Fact]
    public void AddProvider_WhenTypeDoesNotImplementInterface_ThrowsInvalidOperationException()
    {
        var options = new XiHanObjectStorageProviderOptions();

        var exception = Assert.Throws<InvalidOperationException>(() => options.AddProvider("Custom", typeof(string)));

        Assert.Contains(nameof(IFileStorageProvider), exception.Message);
        Assert.Contains(typeof(string).FullName!, exception.Message);
        Assert.Empty(options.ProviderTypes);
    }

    /// <summary>
    /// 校验只要求可赋值给接口，抽象基类本身也能登记
    /// </summary>
    /// <remarks>
    /// 这条记录当前的宽松语义：注册表不做「可实例化」检查，抽象类要到 DI 解析阶段才会暴露问题。
    /// </remarks>
    [Fact]
    public void AddProvider_WithAbstractBaseType_IsAccepted()
    {
        var options = new XiHanObjectStorageProviderOptions();

        options.AddProvider("Abstract", typeof(FileStorageProviderBase));

        Assert.Equal(typeof(FileStorageProviderBase), options.ProviderTypes["Abstract"]);
    }
}
