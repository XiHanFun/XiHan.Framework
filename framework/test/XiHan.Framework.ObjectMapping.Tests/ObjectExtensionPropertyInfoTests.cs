// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 对象扩展属性信息测试
/// </summary>
/// <remarks>
/// 构造函数会按属性类型自动补默认验证特性并算出默认值，这两件事是扩展属性「开箱即用」的基础：
/// 非空基元类型与枚举自动带 Required，枚举额外带 EnumDataType。
/// 默认值取值链是 DefaultValueFactory &gt; DefaultValue &gt; 类型默认值，工厂每次调用都会重新求值。
/// </remarks>
public class ObjectExtensionPropertyInfoTests
{
    /// <summary>
    /// 宿主扩展信息为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenObjectExtensionNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ObjectExtensionPropertyInfo(null!, typeof(string), "Name"));

        Assert.Equal("objectExtension", exception.ParamName);
    }

    /// <summary>
    /// 属性类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenTypeNull_ThrowsArgumentNullException()
    {
        var owner = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.Throws<ArgumentNullException>(
            () => new ObjectExtensionPropertyInfo(owner, null!, "Name"));

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 属性名为 null 时抛出参数异常
    /// </summary>
    /// <remarks>
    /// 这里用 ThrowsAny 而不是钉死 ArgumentNullException：Guard.NotNull 对 string 走的是
    /// 专门的字符串重载（抛 ArgumentException），与泛型重载（抛 ArgumentNullException）不同，
    /// 两者都属于「参数非法」这一契约，真正需要断言的是参数名。
    /// </remarks>
    [Fact]
    public void Constructor_WhenNameNull_ThrowsArgumentException()
    {
        var owner = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var exception = Assert.ThrowsAny<ArgumentException>(
            () => new ObjectExtensionPropertyInfo(owner, typeof(string), null!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 构造后名称、类型与宿主引用均被保留
    /// </summary>
    [Fact]
    public void Constructor_KeepsNameTypeAndOwner()
    {
        var owner = new ObjectExtensionInfo(typeof(FakeExtensibleObject));

        var sut = new ObjectExtensionPropertyInfo(owner, typeof(string), "Name");

        Assert.Equal("Name", sut.Name);
        Assert.Equal(typeof(string), sut.Type);
        Assert.Same(owner, sut.ObjectExtension);
    }

    /// <summary>
    /// 非空基元类型自动带上 Required 验证特性
    /// </summary>
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(TimeSpan))]
    public void Constructor_ForNonNullablePrimitiveType_AddsRequiredAttribute(Type propertyType)
    {
        var sut = CreateProperty(propertyType);

        Assert.Single(sut.Attributes);
        Assert.Contains(sut.Attributes, attribute => attribute is RequiredAttribute);
    }

    /// <summary>
    /// 引用类型与可空基元类型不自动加任何特性
    /// </summary>
    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(Guid?))]
    [InlineData(typeof(object))]
    public void Constructor_ForNullableOrReferenceType_AddsNoAttribute(Type propertyType)
    {
        var sut = CreateProperty(propertyType);

        Assert.Empty(sut.Attributes);
    }

    /// <summary>
    /// 枚举类型同时带上 Required 与 EnumDataType 两个特性
    /// </summary>
    [Fact]
    public void Constructor_ForEnumType_AddsRequiredAndEnumDataTypeAttributes()
    {
        var sut = CreateProperty(typeof(FakeExtensionEnum));

        Assert.Equal(2, sut.Attributes.Count);
        Assert.Contains(sut.Attributes, attribute => attribute is RequiredAttribute);
        var enumAttribute = sut.Attributes.OfType<EnumDataTypeAttribute>().Single();
        Assert.Equal(typeof(FakeExtensionEnum), enumAttribute.EnumType);
    }

    /// <summary>
    /// 默认值初始化为属性类型的 default
    /// </summary>
    [Fact]
    public void Constructor_InitializesDefaultValueFromPropertyType()
    {
        Assert.Equal(0, CreateProperty(typeof(int)).DefaultValue);
        Assert.Equal(Guid.Empty, CreateProperty(typeof(Guid)).DefaultValue);
        Assert.Equal(FakeExtensionEnum.None, CreateProperty(typeof(FakeExtensionEnum)).DefaultValue);
        Assert.Null(CreateProperty(typeof(string)).DefaultValue);
        Assert.Null(CreateProperty(typeof(int?)).DefaultValue);
    }

    /// <summary>
    /// 查找配置的默认字段名与前端契约一致
    /// </summary>
    [Fact]
    public void Constructor_InitializesLookupWithDocumentedDefaults()
    {
        var sut = CreateProperty(typeof(string));

        Assert.NotNull(sut.Lookup);
        Assert.Null(sut.Lookup.Url);
        Assert.Equal("items", sut.Lookup.ResultListPropertyName);
        Assert.Equal("text", sut.Lookup.DisplayPropertyName);
        Assert.Equal("id", sut.Lookup.ValuePropertyName);
        Assert.Equal("filter", sut.Lookup.FilterParamName);
    }

    /// <summary>
    /// 界面配置默认顺序为 0 且编辑模态框可编辑
    /// </summary>
    [Fact]
    public void Constructor_InitializesUiWithZeroOrderAndEditableModal()
    {
        var sut = CreateProperty(typeof(string));

        Assert.NotNull(sut.Ui);
        Assert.Equal(0, sut.Ui.Order);
        Assert.NotNull(sut.Ui.EditModal);
        Assert.False(sut.Ui.EditModal.IsReadOnly);
    }

    /// <summary>
    /// 策略配置默认三段都为空且不要求全部满足
    /// </summary>
    [Fact]
    public void Constructor_InitializesPolicyWithEmptyRequirements()
    {
        var sut = CreateProperty(typeof(string));

        Assert.NotNull(sut.Policy);
        Assert.Empty(sut.Policy.GlobalFeatures.Features);
        Assert.Empty(sut.Policy.Features.Features);
        Assert.Empty(sut.Policy.Permissions.PermissionNames);
        Assert.False(sut.Policy.GlobalFeatures.RequiresAll);
        Assert.False(sut.Policy.Features.RequiresAll);
        Assert.False(sut.Policy.Permissions.RequiresAll);
    }

    /// <summary>
    /// 显示名与映射配对检查开关默认未设置，验证器与配置字典为空
    /// </summary>
    [Fact]
    public void Constructor_LeavesOptionalMembersUnset()
    {
        var sut = CreateProperty(typeof(string));

        Assert.Null(sut.DisplayName);
        Assert.Null(sut.CheckPairDefinitionOnMapping);
        Assert.Null(sut.DefaultValueFactory);
        Assert.Empty(sut.Validators);
        Assert.Empty(sut.Configuration);
    }

    /// <summary>
    /// 未做任何配置时取到类型默认值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenNothingConfigured_ReturnsTypeDefault()
    {
        Assert.Equal(0, CreateProperty(typeof(int)).GetDefaultValue());
        Assert.Null(CreateProperty(typeof(string)).GetDefaultValue());
    }

    /// <summary>
    /// 设置了默认值时取该值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenDefaultValueSet_ReturnsIt()
    {
        var sut = CreateProperty(typeof(string));
        sut.DefaultValue = "默认名";

        Assert.Equal("默认名", sut.GetDefaultValue());
    }

    /// <summary>
    /// 默认值工厂优先级高于默认值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenFactorySet_FactoryWinsOverDefaultValue()
    {
        var sut = CreateProperty(typeof(string));
        sut.DefaultValue = "默认名";
        sut.DefaultValueFactory = () => "工厂值";

        Assert.Equal("工厂值", sut.GetDefaultValue());
    }

    /// <summary>
    /// 默认值工厂每次取值都会重新求值，而不是只算一次缓存住
    /// </summary>
    /// <remarks>
    /// 「每个实例拿到独立的默认值」这类场景（例如默认集合、默认时间戳）完全依赖这一点。
    /// </remarks>
    [Fact]
    public void GetDefaultValue_WhenFactorySet_IsEvaluatedOnEachCall()
    {
        var sut = CreateProperty(typeof(int));
        var invokedCount = 0;
        sut.DefaultValueFactory = () => ++invokedCount;

        Assert.Equal(1, sut.GetDefaultValue());
        Assert.Equal(2, sut.GetDefaultValue());
        Assert.Equal(2, invokedCount);
    }

    /// <summary>
    /// 默认值被显式置为 null 时回落到类型默认值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenDefaultValueClearedToNull_FallsBackToTypeDefault()
    {
        var sut = CreateProperty(typeof(int));
        sut.DefaultValue = 5;
        sut.DefaultValue = null;

        Assert.Equal(0, sut.GetDefaultValue());
    }

    /// <summary>
    /// 显示名可读写并保持同一实例
    /// </summary>
    [Fact]
    public void DisplayName_RoundTrips()
    {
        var sut = CreateProperty(typeof(string));
        var displayName = new FakeLocalizableString("显示名");

        sut.DisplayName = displayName;

        Assert.Same(displayName, sut.DisplayName);
    }

    /// <summary>
    /// 该类型必须同时满足基础扩展属性信息与可本地化显示名两套契约
    /// </summary>
    [Fact]
    public void Type_ImplementsPublicContracts()
    {
        var sut = CreateProperty(typeof(string));

        Assert.IsAssignableFrom<IBasicObjectExtensionPropertyInfo>(sut);
        Assert.IsAssignableFrom<IHasNameWithLocalizableDisplayName>(sut);
    }

    /// <summary>
    /// 特性集合可变，供模块在注册期追加自定义验证特性
    /// </summary>
    [Fact]
    public void Attributes_IsMutable()
    {
        var sut = CreateProperty(typeof(string));

        sut.Attributes.Add(new StringLengthAttribute(10));

        Assert.Contains(sut.Attributes, attribute => attribute is StringLengthAttribute);
    }

    /// <summary>
    /// 创建一个挂在临时宿主上的扩展属性信息
    /// </summary>
    /// <param name="propertyType">属性类型</param>
    /// <returns>扩展属性信息</returns>
    private static ObjectExtensionPropertyInfo CreateProperty(Type propertyType)
    {
        var owner = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        return new ObjectExtensionPropertyInfo(owner, propertyType, "Property");
    }
}
