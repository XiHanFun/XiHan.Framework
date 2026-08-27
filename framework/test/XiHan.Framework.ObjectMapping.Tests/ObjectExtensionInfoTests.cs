// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 对象扩展信息测试
/// </summary>
/// <remarks>
/// 该类型的构造函数是公开的，所以这批用例全部直接 new，不碰全局单例，天然隔离。
/// 需要特别钉死的是 AddOrUpdateProperty 的「Update」到底更新了什么：
/// 它只重复执行配置委托，属性类型在首次注册时就固化，第二次传入不同类型不会改写。
/// </remarks>
public class ObjectExtensionInfoTests
{
    /// <summary>
    /// 类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenTypeNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ObjectExtensionInfo(null!));

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 构造后类型被记录，配置字典与对象级验证器均为空
    /// </summary>
    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        Assert.Equal(typeof(FakeExtensibleObject), sut.Type);
        Assert.Empty(sut.Configuration);
        Assert.Empty(sut.Validators);
        Assert.Empty(sut.GetProperties());
    }

    /// <summary>
    /// 未注册的属性名 HasProperty 返回 false
    /// </summary>
    [Fact]
    public void HasProperty_WhenPropertyNotRegistered_ReturnsFalse()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        Assert.False(sut.HasProperty("Name"));
    }

    /// <summary>
    /// 属性名为 null 时 HasProperty 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void HasProperty_WhenPropertyNameNull_ThrowsArgumentNullException()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentNullException>(() => sut.HasProperty(null!));

        Assert.Equal("propertyName", exception.ParamName);
    }

    /// <summary>
    /// 泛型重载按给定的属性类型建立扩展属性
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_Generic_RegistersPropertyWithGivenType()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var returned = sut.AddOrUpdateProperty<string>("Name");

        Assert.Same(sut, returned);
        Assert.True(sut.HasProperty("Name"));
        var property = sut.GetPropertyOrNull("Name");
        Assert.NotNull(property);
        Assert.Equal("Name", property.Name);
        Assert.Equal(typeof(string), property.Type);
    }

    /// <summary>
    /// 扩展属性持有对宿主扩展信息的反向引用
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_LinksPropertyBackToOwner()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        sut.AddOrUpdateProperty<string>("Name");

        Assert.Same(sut, sut.GetPropertyOrNull("Name")!.ObjectExtension);
    }

    /// <summary>
    /// 重复注册同名属性复用同一份属性信息实例
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_CalledTwice_ReusesSamePropertyInfoInstance()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        sut.AddOrUpdateProperty<string>("Name");
        var first = sut.GetPropertyOrNull("Name");
        sut.AddOrUpdateProperty<string>("Name");
        var second = sut.GetPropertyOrNull("Name");

        Assert.Same(first, second);
    }

    /// <summary>
    /// 同名属性第二次注册传入不同类型时，属性类型保持首次注册的值
    /// </summary>
    /// <remarks>
    /// 「AddOrUpdate」更新的是配置委托的执行，而不是属性类型本身：内部走的是 GetOrAdd。
    /// 这条语义如果被改动，所有依赖属性类型稳定的持久化映射都会跟着变，必须锁死。
    /// </remarks>
    [Fact]
    public void AddOrUpdateProperty_WhenReRegisteredWithOtherType_KeepsFirstRegisteredType()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        sut.AddOrUpdateProperty<int>("Age");
        sut.AddOrUpdateProperty<string>("Age");

        Assert.Equal(typeof(int), sut.GetPropertyOrNull("Age")!.Type);
    }

    /// <summary>
    /// 属性配置委托在每次注册时都会执行
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_InvokesConfigureActionOnEveryCall()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        var invokedCount = 0;

        sut.AddOrUpdateProperty<string>("Name", _ => invokedCount++);
        sut.AddOrUpdateProperty<string>("Name", property => property.Ui.Order = 9);

        Assert.Equal(1, invokedCount);
        Assert.Equal(9, sut.GetPropertyOrNull("Name")!.Ui.Order);
    }

    /// <summary>
    /// 属性类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WhenPropertyTypeNull_ThrowsArgumentNullException()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentNullException>(() => sut.AddOrUpdateProperty((Type)null!, "Name"));

        Assert.Equal("propertyType", exception.ParamName);
    }

    /// <summary>
    /// 属性名为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WhenPropertyNameNull_ThrowsArgumentNullException()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentNullException>(() => sut.AddOrUpdateProperty(typeof(string), null!));

        Assert.Equal("propertyName", exception.ParamName);
    }

    /// <summary>
    /// GetProperties 按界面顺序升序排列，而不是注册顺序
    /// </summary>
    [Fact]
    public void GetProperties_OrdersByUiOrderAscending()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        sut.AddOrUpdateProperty<string>("Third", property => property.Ui.Order = 30);
        sut.AddOrUpdateProperty<string>("First", property => property.Ui.Order = 10);
        sut.AddOrUpdateProperty<string>("Second", property => property.Ui.Order = 20);

        var names = sut.GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(new[] { "First", "Second", "Third" }, names);
    }

    /// <summary>
    /// 界面顺序允许为负数，负数排在默认的 0 之前
    /// </summary>
    [Fact]
    public void GetProperties_SupportsNegativeUiOrder()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        sut.AddOrUpdateProperty<string>("Default");
        sut.AddOrUpdateProperty<string>("Leading", property => property.Ui.Order = -1);

        var names = sut.GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(new[] { "Leading", "Default" }, names);
    }

    /// <summary>
    /// 未注册的属性名 GetPropertyOrNull 返回 null
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenPropertyNotRegistered_ReturnsNull()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        Assert.Null(sut.GetPropertyOrNull("Missing"));
    }

    /// <summary>
    /// 属性名为 null 时 GetPropertyOrNull 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenPropertyNameNull_ThrowsArgumentNullException()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentNullException>(() => sut.GetPropertyOrNull(null!));

        Assert.Equal("propertyName", exception.ParamName);
    }

    /// <summary>
    /// 属性名为空串时 GetPropertyOrNull 抛出 ArgumentException
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenPropertyNameEmpty_ThrowsArgumentException()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentException>(() => sut.GetPropertyOrNull(string.Empty));

        Assert.Equal("propertyName", exception.ParamName);
    }

    /// <summary>
    /// 属性名区分大小写
    /// </summary>
    [Fact]
    public void Properties_AreCaseSensitive()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        sut.AddOrUpdateProperty<string>("Name");

        Assert.True(sut.HasProperty("Name"));
        Assert.False(sut.HasProperty("name"));
    }

    /// <summary>
    /// 对象级验证器集合可变，供模块在注册期追加
    /// </summary>
    [Fact]
    public void Validators_IsMutable()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        sut.Validators.Add(_ => { });

        Assert.Single(sut.Validators);
    }

    /// <summary>
    /// 配置字典可存放任意键值对
    /// </summary>
    [Fact]
    public void Configuration_StoresArbitraryEntries()
    {
        var sut = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        sut.Configuration["Key"] = "值";

        Assert.Single(sut.Configuration);
        Assert.Equal("值", sut.Configuration["Key"]);
    }
}
