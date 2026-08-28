// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Extensions.Data;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions.Data;

/// <summary>
/// 额外属性扩展方法测试
/// </summary>
/// <remarks>
/// 这批扩展方法是业务代码读写扩展属性的唯一入口，重点覆盖三处非显然语义：
/// 1. GetProperty 的「值为 null」与「键不存在」走同一条 ?? defaultValue 分支，二者不可区分；
/// 2. GetProperty&lt;T&gt; 只支持扩展基元类型（含可空与枚举），其余一律抛 XiHanException；
/// 3. SetDefaultsForExtraProperties 依赖 ObjectExtensionManager 全局单例，因此每个用例都用
///    自己专属的标记类型注册，避免相互污染。
/// </remarks>
public class HasExtraPropertiesExtensionsTests
{
    /// <summary>
    /// 存在指定键时 HasProperty 返回 true
    /// </summary>
    [Fact]
    public void HasProperty_WhenKeyPresent_ReturnsTrue()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Name"] = "曦寒";

        Assert.True(source.HasProperty("Name"));
        Assert.False(source.HasProperty("Missing"));
    }

    /// <summary>
    /// 值为 null 的键仍然算存在
    /// </summary>
    [Fact]
    public void HasProperty_WhenValueNull_StillReturnsTrue()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Name"] = null;

        Assert.True(source.HasProperty("Name"));
    }

    /// <summary>
    /// 源对象为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void HasProperty_WhenSourceNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => HasExtraPropertiesExtensions.HasProperty(null!, "Name"));

        Assert.Equal("source", exception.ParamName);
    }

    /// <summary>
    /// 属性名为空白时抛出 ArgumentException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HasProperty_WhenNameBlank_ThrowsArgumentException(string name)
    {
        var source = new FakeExtensibleObject();

        var exception = Assert.Throws<ArgumentException>(() => source.HasProperty(name));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 非泛型 GetProperty 命中时返回原始装箱值
    /// </summary>
    [Fact]
    public void GetProperty_WhenKeyPresent_ReturnsStoredValueAsIs()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = 18;

        Assert.Equal(18, source.GetProperty("Age"));
    }

    /// <summary>
    /// 键不存在时返回给定的默认值
    /// </summary>
    [Fact]
    public void GetProperty_WhenKeyMissing_ReturnsDefaultValue()
    {
        var source = new FakeExtensibleObject();

        Assert.Null(source.GetProperty("Age"));
        Assert.Equal("兜底", source.GetProperty("Age", (object?)"兜底"));
    }

    /// <summary>
    /// 存了 null 值与键不存在同样返回默认值：两者无法区分
    /// </summary>
    [Fact]
    public void GetProperty_WhenStoredValueNull_FallsBackToDefaultValue()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = null;

        Assert.Equal("兜底", source.GetProperty("Age", (object?)"兜底"));
    }

    /// <summary>
    /// 泛型 GetProperty 对同类型值直接返回
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTypeMatches_ReturnsValue()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = 18;

        Assert.Equal(18, source.GetProperty<int>("Age"));
    }

    /// <summary>
    /// 泛型 GetProperty 会把字符串转换成目标基元类型
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenStoredAsText_ConvertsToTargetPrimitive()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = "18";
        source.ExtraProperties["Enabled"] = "true";
        source.ExtraProperties["Total"] = "9007199254740993";

        Assert.Equal(18, source.GetProperty<int>("Age"));
        Assert.True(source.GetProperty<bool>("Enabled"));
        Assert.Equal(9007199254740993L, source.GetProperty<long>("Total"));
    }

    /// <summary>
    /// 泛型 GetProperty 会把数值转换成字符串
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTargetIsString_ConvertsFromNumber()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = 18;

        Assert.Equal("18", source.GetProperty<string>("Age"));
    }

    /// <summary>
    /// 泛型 GetProperty 支持可空基元类型，转换结果落在底层类型上
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTargetIsNullablePrimitive_UnwrapsUnderlyingType()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Age"] = "18";

        Assert.Equal(18, source.GetProperty<int?>("Age"));
    }

    /// <summary>
    /// 泛型 GetProperty 走 TypeConverter 处理 Guid
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTargetIsGuid_UsesTypeConverter()
    {
        var expected = Guid.NewGuid();
        var source = new FakeExtensibleObject();
        source.ExtraProperties["FromText"] = expected.ToString();
        source.ExtraProperties["FromGuid"] = expected;

        Assert.Equal(expected, source.GetProperty<Guid>("FromText"));
        Assert.Equal(expected, source.GetProperty<Guid>("FromGuid"));
        Assert.Equal(expected, source.GetProperty<Guid?>("FromText"));
    }

    /// <summary>
    /// 泛型 GetProperty 支持枚举名称解析
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTargetIsEnum_ParsesByName()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Kind"] = "Second";

        Assert.Equal(FakeExtensionEnum.Second, source.GetProperty<FakeExtensionEnum>("Kind"));
    }

    /// <summary>
    /// 键不存在时泛型 GetProperty 返回给定默认值，不做任何类型转换
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenKeyMissing_ReturnsDefaultValue()
    {
        var source = new FakeExtensibleObject();

        Assert.Equal(0, source.GetProperty<int>("Age"));
        Assert.Equal(7, source.GetProperty("Age", 7));
        Assert.Null(source.GetProperty<string>("Name"));
    }

    /// <summary>
    /// 值为 null 时泛型 GetProperty 提前返回默认值，不会因类型不受支持而抛异常
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenStoredValueNull_ReturnsDefaultBeforeTypeCheck()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Complex"] = null;

        Assert.Null(source.GetProperty<List<int>>("Complex"));
    }

    /// <summary>
    /// 目标类型不是扩展基元类型时抛出 XiHanException，提示改用非泛型重载
    /// </summary>
    [Fact]
    public void GetPropertyGeneric_WhenTargetTypeNotPrimitive_ThrowsXiHanException()
    {
        var source = new FakeExtensibleObject();
        source.ExtraProperties["Complex"] = new List<int> { 1 };

        var exception = Assert.Throws<XiHanException>(() => source.GetProperty<List<int>>("Complex"));

        Assert.Contains("不支持非原始类型", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// SetProperty 写入值并返回源对象本身以支持链式调用
    /// </summary>
    [Fact]
    public void SetProperty_WritesValueAndReturnsSource()
    {
        var source = new FakeExtensibleObject();

        var returned = source.SetProperty("Name", "曦寒").SetProperty("Age", 18);

        Assert.Same(source, returned);
        Assert.Equal("曦寒", source.ExtraProperties["Name"]);
        Assert.Equal(18, source.ExtraProperties["Age"]);
    }

    /// <summary>
    /// SetProperty 允许写入 null，并覆盖已有值
    /// </summary>
    [Fact]
    public void SetProperty_OverwritesExistingValueIncludingNull()
    {
        var source = new FakeExtensibleObject();
        source.SetProperty("Name", "旧值");

        source.SetProperty("Name", null);

        Assert.True(source.ExtraProperties.ContainsKey("Name"));
        Assert.Null(source.ExtraProperties["Name"]);
    }

    /// <summary>
    /// SetProperty 的属性名为空白时抛出 ArgumentException
    /// </summary>
    [Fact]
    public void SetProperty_WhenNameBlank_ThrowsArgumentException()
    {
        var source = new FakeExtensibleObject();

        var exception = Assert.Throws<ArgumentException>(() => source.SetProperty("  ", "值"));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// RemoveProperty 删除已有键并返回源对象
    /// </summary>
    [Fact]
    public void RemoveProperty_RemovesExistingKeyAndReturnsSource()
    {
        var source = new FakeExtensibleObject();
        source.SetProperty("Name", "曦寒");

        var returned = source.RemoveProperty("Name");

        Assert.Same(source, returned);
        Assert.False(source.ExtraProperties.ContainsKey("Name"));
    }

    /// <summary>
    /// 删除不存在的键是幂等的空操作，不抛异常
    /// </summary>
    [Fact]
    public void RemoveProperty_WhenKeyMissing_IsNoOp()
    {
        var source = new FakeExtensibleObject();

        source.RemoveProperty("Missing");

        Assert.Empty(source.ExtraProperties);
    }

    /// <summary>
    /// SetDefaultsForExtraProperties 会为未赋值的扩展属性填入默认值
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_FillsMissingPropertiesWithDefaults()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(DefaultsTarget), typeof(int), "Age");
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(DefaultsTarget), typeof(string), "Name", property =>
        {
            property.DefaultValue = "默认名";
        });

        var source = new DefaultsTarget();
        var returned = source.SetDefaultsForExtraProperties();

        Assert.Same(source, returned);
        Assert.Equal(0, source.ExtraProperties["Age"]);
        Assert.Equal("默认名", source.ExtraProperties["Name"]);
    }

    /// <summary>
    /// 已经存在的扩展属性不会被默认值覆盖
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_DoesNotOverwriteExistingValues()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(KeepExistingTarget), typeof(string), "Name", property =>
        {
            property.DefaultValue = "默认名";
        });

        var source = new KeepExistingTarget();
        source.ExtraProperties["Name"] = "已有值";

        source.SetDefaultsForExtraProperties();

        Assert.Equal("已有值", source.ExtraProperties["Name"]);
    }

    /// <summary>
    /// 类型未注册扩展属性时不做任何事情
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_WhenTypeNotRegistered_LeavesDictionaryEmpty()
    {
        var source = new UnregisteredDefaultsTarget();

        source.SetDefaultsForExtraProperties();

        Assert.Empty(source.ExtraProperties);
    }

    /// <summary>
    /// 显式传入对象类型时按该类型而不是 TSource 取扩展属性定义
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_WhenObjectTypeGiven_UsesThatTypeDefinition()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty(typeof(ExplicitTypeTarget), typeof(string), "Name", property =>
        {
            property.DefaultValue = "来自显式类型";
        });

        var source = new FakeExtensibleObject();

        source.SetDefaultsForExtraProperties(typeof(ExplicitTypeTarget));

        Assert.Equal("来自显式类型", source.ExtraProperties["Name"]);
    }

    /// <summary>
    /// 非扩展方法重载在对象未实现 IHasExtraProperties 时抛出 ArgumentException
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_WhenSourceDoesNotImplementInterface_ThrowsArgumentException()
    {
        object source = new();

        var exception = Assert.Throws<ArgumentException>(
            () => HasExtraPropertiesExtensions.SetDefaultsForExtraProperties(source, typeof(FakeExtensibleObject)));

        Assert.Equal("source", exception.ParamName);
        Assert.Contains(nameof(IHasExtraProperties), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 非扩展方法重载的对象类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void SetDefaultsForExtraProperties_WhenObjectTypeNull_ThrowsArgumentNullException()
    {
        object source = new FakeExtensibleObject();

        var exception = Assert.Throws<ArgumentNullException>(
            () => HasExtraPropertiesExtensions.SetDefaultsForExtraProperties(source, null!));

        Assert.Equal("objectType", exception.ParamName);
    }

    /// <summary>
    /// SetExtraPropertiesToRegularProperties 把同名额外属性回填到常规属性并从字典移除
    /// </summary>
    [Fact]
    public void SetExtraPropertiesToRegularProperties_MovesMatchingEntriesToRealProperties()
    {
        var source = new FakeRegularPropertyObject();
        source.ExtraProperties["Title"] = "标题";
        source.ExtraProperties["Count"] = 5;

        source.SetExtraPropertiesToRegularProperties();

        Assert.Equal("标题", source.Title);
        Assert.Equal(5, source.Count);
        Assert.Empty(source.ExtraProperties);
    }

    /// <summary>
    /// 没有 setter 的常规属性不会被回填，对应的额外属性原样保留
    /// </summary>
    [Fact]
    public void SetExtraPropertiesToRegularProperties_KeepsEntriesWithoutSettableProperty()
    {
        var source = new FakeRegularPropertyObject();
        source.ExtraProperties["ReadOnlyText"] = "不该被写入";
        source.ExtraProperties["NotAProperty"] = "保留";

        source.SetExtraPropertiesToRegularProperties();

        Assert.Equal("只读", source.ReadOnlyText);
        Assert.Equal("不该被写入", source.ExtraProperties["ReadOnlyText"]);
        Assert.Equal("保留", source.ExtraProperties["NotAProperty"]);
    }

    /// <summary>
    /// HasSameExtraProperties 按 HasSameItems 的口径比较两个对象的额外属性
    /// </summary>
    [Fact]
    public void HasSameExtraProperties_ComparesUnderlyingDictionaries()
    {
        var left = new FakeExtensibleObject();
        var right = new FakeExtensibleObject();
        var other = new FakeExtensibleObject();

        left.SetProperty("Name", "曦寒");
        right.SetProperty("Name", "曦寒");
        other.SetProperty("Name", "别的");

        Assert.True(left.HasSameExtraProperties(right));
        Assert.False(left.HasSameExtraProperties(other));
    }

    /// <summary>
    /// HasSameExtraProperties 任一入参为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void HasSameExtraProperties_WhenAnyOperandNull_ThrowsArgumentNullException()
    {
        var source = new FakeExtensibleObject();

        var first = Assert.Throws<ArgumentNullException>(
            () => HasExtraPropertiesExtensions.HasSameExtraProperties(null!, source));
        var second = Assert.Throws<ArgumentNullException>(() => source.HasSameExtraProperties(null!));

        Assert.Equal("source", first.ParamName);
        Assert.Equal("other", second.ParamName);
    }

    /// <summary>
    /// SetDefaultsForExtraProperties 专属标记类型
    /// </summary>
    private sealed class DefaultsTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 「不覆盖已有值」用例专属标记类型
    /// </summary>
    private sealed class KeepExistingTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 未注册扩展属性的标记类型
    /// </summary>
    private sealed class UnregisteredDefaultsTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 显式指定对象类型用例专属标记类型
    /// </summary>
    private sealed class ExplicitTypeTarget : FakeExtensibleObject
    {
    }
}
