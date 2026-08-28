// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Options;

/// <summary>
/// 曦寒设置选项测试
/// </summary>
/// <remarks>
/// 两个类型列表都做了基类型约束，塞入不相干的类型必须当场失败，
/// 否则错误要拖到反射实例化阶段才暴露，报错位置离出错点很远。
/// </remarks>
public class XiHanSettingOptionsTests
{
    /// <summary>
    /// 配置节名称保持稳定
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Settings", XiHanSettingOptions.SectionName);
    }

    /// <summary>
    /// 构造后各集合为空、解密失败回退开关默认打开
    /// </summary>
    [Fact]
    public void Ctor_AppliesDocumentedDefaults()
    {
        var options = new XiHanSettingOptions();

        Assert.NotNull(options.DefinitionProviders);
        Assert.Empty(options.DefinitionProviders);
        Assert.NotNull(options.ValueProviders);
        Assert.Empty(options.ValueProviders);
        Assert.NotNull(options.DeletedSettings);
        Assert.Empty(options.DeletedSettings);
        Assert.True(options.ReturnOriginalValueIfDecryptFailed);
    }

    /// <summary>
    /// 泛型添加定义提供者类型
    /// </summary>
    [Fact]
    public void DefinitionProviders_AcceptsProviderType()
    {
        var options = new XiHanSettingOptions();

        options.DefinitionProviders.Add<AlphaSettingDefinitionProvider>();

        Assert.Contains(typeof(AlphaSettingDefinitionProvider), options.DefinitionProviders);
    }

    /// <summary>
    /// 泛型添加值提供者类型并保持添加顺序
    /// </summary>
    /// <remarks>
    /// 值提供者列表的顺序即覆盖优先级，越靠后优先级越高，顺序不能被集合实现打乱。
    /// </remarks>
    [Fact]
    public void ValueProviders_PreservesInsertionOrder()
    {
        var options = new XiHanSettingOptions();

        options.ValueProviders.Add<DefaultValueSettingValueProvider>();
        options.ValueProviders.Add<ConfigurationSettingValueProvider>();
        options.ValueProviders.Add<GlobalSettingValueProvider>();
        options.ValueProviders.Add<UserSettingValueProvider>();

        Assert.Equal(4, options.ValueProviders.Count);
        Assert.Equal(typeof(DefaultValueSettingValueProvider), options.ValueProviders[0]);
        Assert.Equal(typeof(ConfigurationSettingValueProvider), options.ValueProviders[1]);
        Assert.Equal(typeof(GlobalSettingValueProvider), options.ValueProviders[2]);
        Assert.Equal(typeof(UserSettingValueProvider), options.ValueProviders[3]);
    }

    /// <summary>
    /// 塞入未实现值提供者接口的类型时抛出参数异常
    /// </summary>
    [Fact]
    public void ValueProviders_WhenTypeIsNotAssignable_ThrowsArgumentException()
    {
        var options = new XiHanSettingOptions();

        var exception = Assert.Throws<ArgumentException>(() => options.ValueProviders.Add(typeof(string)));

        Assert.Equal("item", exception.ParamName);
    }

    /// <summary>
    /// 塞入未实现定义提供者接口的类型时抛出参数异常
    /// </summary>
    [Fact]
    public void DefinitionProviders_WhenTypeIsNotAssignable_ThrowsArgumentException()
    {
        var options = new XiHanSettingOptions();

        Assert.Throws<ArgumentException>(() => options.DefinitionProviders.Add(typeof(FakeSettingStore)));
    }

    /// <summary>
    /// 已删除设置集合是去重集合
    /// </summary>
    [Fact]
    public void DeletedSettings_DeduplicatesEntries()
    {
        var options = new XiHanSettingOptions();

        Assert.True(options.DeletedSettings.Add("Foo"));
        Assert.False(options.DeletedSettings.Add("Foo"));
        Assert.Single(options.DeletedSettings);
    }

    /// <summary>
    /// 解密失败回退开关可关闭
    /// </summary>
    [Fact]
    public void ReturnOriginalValueIfDecryptFailed_IsMutable()
    {
        var options = new XiHanSettingOptions
        {
            ReturnOriginalValueIfDecryptFailed = false
        };

        Assert.False(options.ReturnOriginalValueIfDecryptFailed);
    }
}
