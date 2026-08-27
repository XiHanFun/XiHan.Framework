// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Definitions;

/// <summary>
/// 设置定义测试
/// </summary>
/// <remarks>
/// 设置定义是整个设置系统的契约中心：默认值、分组、可见性、加密标记与校验函数都被
/// 设置管理器直接读取，因此这里逐项锁死构造器默认值，避免默认语义悄悄漂移。
/// </remarks>
public class SettingDefinitionTests
{
    /// <summary>
    /// 只给名称时，其余属性取构造器约定的默认值
    /// </summary>
    [Fact]
    public void Ctor_WithOnlyName_AppliesDocumentedDefaults()
    {
        var definition = new SettingDefinition("Foo");

        Assert.Equal("Foo", definition.Name);
        Assert.Null(definition.DefaultValue);
        Assert.Equal(string.Empty, definition.DisplayName);
        Assert.Equal(string.Empty, definition.Description);
        Assert.Equal("General", definition.Group);
        Assert.False(definition.IsVisibleToClients);
        Assert.False(definition.IsEncrypted);
        Assert.Null(definition.Validator);
        Assert.NotNull(definition.Providers);
        Assert.Empty(definition.Providers);
    }

    /// <summary>
    /// 显式传入全部参数时逐一落到对应属性
    /// </summary>
    [Fact]
    public void Ctor_WithAllArguments_AssignsEveryProperty()
    {
        var definition = new SettingDefinition(
            "Smtp.Password",
            defaultValue: "init",
            displayName: "邮件密码",
            description: "外发邮件账号密码",
            group: "Mailing",
            isVisibleToClients: true,
            isEncrypted: true,
            validator: value => !string.IsNullOrWhiteSpace(value));

        Assert.Equal("Smtp.Password", definition.Name);
        Assert.Equal("init", definition.DefaultValue);
        Assert.Equal("邮件密码", definition.DisplayName);
        Assert.Equal("外发邮件账号密码", definition.Description);
        Assert.Equal("Mailing", definition.Group);
        Assert.True(definition.IsVisibleToClients);
        Assert.True(definition.IsEncrypted);
        Assert.NotNull(definition.Validator);
    }

    /// <summary>
    /// 名称为 null 时构造失败并指明参数名
    /// </summary>
    [Fact]
    public void Ctor_WhenNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SettingDefinition(null!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 校验函数原样保留，可被外部直接调用
    /// </summary>
    [Fact]
    public void Validator_WhenProvided_IsInvokedAsGiven()
    {
        var definition = new SettingDefinition("Foo", validator: value => value == "ok");

        var validator = definition.Validator;

        Assert.NotNull(validator);
        Assert.True(validator!.Invoke("ok"));
        Assert.False(validator.Invoke("bad"));
        Assert.False(validator.Invoke(null));
    }

    /// <summary>
    /// 添加提供者返回自身以支持链式调用
    /// </summary>
    [Fact]
    public void AddProvider_ReturnsSameInstanceForChaining()
    {
        var definition = new SettingDefinition("Foo");
        var provider = new FakeSettingValueProvider("P1", null);

        var returned = definition.AddProvider(provider);

        Assert.Same(definition, returned);
        Assert.Same(provider, Assert.Single(definition.Providers));
    }

    /// <summary>
    /// 提供者按添加顺序排列——顺序即优先级，不能被打乱
    /// </summary>
    [Fact]
    public void AddProvider_PreservesInsertionOrder()
    {
        var first = new FakeSettingValueProvider("P1", null);
        var second = new FakeSettingValueProvider("P2", null);
        var third = new FakeSettingValueProvider("P3", null);

        var definition = new SettingDefinition("Foo")
            .AddProvider(first)
            .AddProvider(second)
            .AddProvider(third);

        Assert.Equal(new[] { "P1", "P2", "P3" }, definition.Providers.Select(x => x.Name).ToArray());
    }

    /// <summary>
    /// 每个定义持有独立的提供者列表，互不串扰
    /// </summary>
    [Fact]
    public void Providers_AreIsolatedBetweenDefinitions()
    {
        var first = new SettingDefinition("First");
        var second = new SettingDefinition("Second");

        first.AddProvider(new FakeSettingValueProvider("P1", null));

        Assert.Single(first.Providers);
        Assert.Empty(second.Providers);
    }

    /// <summary>
    /// 设置定义是普通类而非记录，同名同值的两个实例不相等
    /// </summary>
    /// <remarks>
    /// 定义表以名称为键去重，若误改成 record 会让"同名不同实例"被吞掉，这里显式锁住引用相等语义。
    /// </remarks>
    [Fact]
    public void SettingDefinition_UsesReferenceEquality()
    {
        var first = new SettingDefinition("Foo", "bar");
        var second = new SettingDefinition("Foo", "bar");

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }
}
