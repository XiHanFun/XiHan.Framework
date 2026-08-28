// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 泛型扩展方法测试
/// </summary>
/// <remarks>
/// 判空相关用例一律采用 <c>GenericExtensions.IsNullOrEmpty(...)</c> 静态调用而非扩展方法语法。
/// 原因：该方法是无约束泛型，一旦调用方同时 using 了 XiHan.Framework.Utils.Collections，
/// 集合实参会被更精确的 CollectionExtensions.IsNullOrEmpty(ICollection&lt;T&gt;) 重载抢走，
/// 就测不到本方法了；静态调用可以把被测目标钉死。
/// </remarks>
public class GenericExtensionsTests
{
    /// <summary>
    /// 泛型类型返回"名称&lt;类型实参&gt;"形式
    /// </summary>
    [Fact]
    public void GetGenericTypeName_WhenGenericType_ReturnsNameWithArguments()
    {
        Assert.Equal("List<Int32>", typeof(List<int>).GetGenericTypeName());
        Assert.Equal("Dictionary<String,Int32>", typeof(Dictionary<string, int>).GetGenericTypeName());
    }

    /// <summary>
    /// 非泛型类型直接返回类型名
    /// </summary>
    [Fact]
    public void GetGenericTypeName_WhenNonGenericType_ReturnsTypeName()
    {
        Assert.Equal("Int32", typeof(int).GetGenericTypeName());
        Assert.Equal("String", typeof(string).GetGenericTypeName());
    }

    /// <summary>
    /// 实例重载按运行时类型取名，而不是静态类型
    /// </summary>
    [Fact]
    public void GetGenericTypeName_OnInstance_UsesRuntimeType()
    {
        object instance = new List<string>();

        Assert.Equal("List<String>", instance.GetGenericTypeName());
    }

    /// <summary>
    /// null 判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNull_ReturnsTrue()
    {
        Assert.True(GenericExtensions.IsNullOrEmpty<string>(null));
        Assert.True(GenericExtensions.IsNullOrEmpty<int?>(null));
        Assert.True(GenericExtensions.IsNullOrEmpty<List<int>>(null));
    }

    /// <summary>
    /// 空字符串与纯空白字符串判为空
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void IsNullOrEmpty_WhenEmptyOrWhiteSpaceString_ReturnsTrue(string value)
    {
        Assert.True(GenericExtensions.IsNullOrEmpty(value));
    }

    /// <summary>
    /// 含可见字符的字符串判为非空
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    [InlineData("0")]
    public void IsNullOrEmpty_WhenNonBlankString_ReturnsFalse(string value)
    {
        Assert.False(GenericExtensions.IsNullOrEmpty(value));
    }

    /// <summary>
    /// 空 List 判为空
    /// </summary>
    /// <remarks>
    /// 回归防线：早期版本缺少 ICollection 分支，非 null 的空集合一律被判成"非空"，
    /// 导致调用方的空集合短路失效。下面几个空集合用例与非空用例都是为了钉死这一点。
    /// </remarks>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyList_ReturnsTrue()
    {
        var empty = new List<int>();

        Assert.True(GenericExtensions.IsNullOrEmpty(empty));
    }

    /// <summary>
    /// 空数组判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyArray_ReturnsTrue()
    {
        var empty = Array.Empty<string>();

        Assert.True(GenericExtensions.IsNullOrEmpty(empty));
    }

    /// <summary>
    /// 空字典判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyDictionary_ReturnsTrue()
    {
        var empty = new Dictionary<string, int>();

        Assert.True(GenericExtensions.IsNullOrEmpty(empty));
    }

    /// <summary>
    /// 空 HashSet 判为空
    /// </summary>
    /// <remarks>
    /// HashSet 只实现泛型 ICollection&lt;T&gt;，不实现非泛型 ICollection，
    /// 因此它走的是 IEnumerable 兜底分支，与 List/数组/字典不是同一条代码路径。
    /// </remarks>
    [Fact]
    public void IsNullOrEmpty_WhenEmptyHashSet_ReturnsTrue()
    {
        var empty = new HashSet<int>();

        Assert.True(GenericExtensions.IsNullOrEmpty(empty));
    }

    /// <summary>
    /// 惰性空序列判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenLazyEmptySequence_ReturnsTrue()
    {
        Assert.True(GenericExtensions.IsNullOrEmpty(EmptyLazySequence()));
    }

    /// <summary>
    /// 非空集合判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNonEmptyCollection_ReturnsFalse()
    {
        Assert.False(GenericExtensions.IsNullOrEmpty(new List<int> { 1 }));
        Assert.False(GenericExtensions.IsNullOrEmpty(new[] { "a" }));
        Assert.False(GenericExtensions.IsNullOrEmpty(new Dictionary<string, int> { ["k"] = 1 }));
        Assert.False(GenericExtensions.IsNullOrEmpty(new HashSet<int> { 1 }));
    }

    /// <summary>
    /// 惰性非空序列判为非空，且只推进一步，不整体枚举
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenLazyNonEmptySequence_PullsOnlyFirstElement()
    {
        List<int> pulled = [];

        var result = GenericExtensions.IsNullOrEmpty(CountingSequence(pulled));

        Assert.False(result);
        Assert.Single(pulled);
    }

    /// <summary>
    /// 值类型不受判空逻辑影响，默认值也算非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenValueType_ReturnsFalse()
    {
        Assert.False(GenericExtensions.IsNullOrEmpty(0));
        Assert.False(GenericExtensions.IsNullOrEmpty(42));
        Assert.False(GenericExtensions.IsNullOrEmpty(false));
        Assert.False(GenericExtensions.IsNullOrEmpty(Guid.Empty));
        Assert.False(GenericExtensions.IsNullOrEmpty(DateTime.MinValue));
        Assert.False(GenericExtensions.IsNullOrEmpty(0m));
    }

    /// <summary>
    /// 有值的可空值类型判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNullableWithValue_ReturnsFalse()
    {
        int? value = 0;

        Assert.False(GenericExtensions.IsNullOrEmpty(value));
    }

    /// <summary>
    /// DBNull 判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenDbNull_ReturnsTrue()
    {
        Assert.True(GenericExtensions.IsNullOrEmpty(DBNull.Value));
    }

    /// <summary>
    /// 普通对象判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenPlainObject_ReturnsFalse()
    {
        Assert.False(GenericExtensions.IsNullOrEmpty(new Holder()));
    }

    /// <summary>
    /// 泛型属性可读出值
    /// </summary>
    [Fact]
    public void GetPropertyValue_WhenGenericProperty_ReturnsValue()
    {
        var holder = new Holder
        {
            Items = ["a", "b"],
            Level = 7
        };

        var items = holder.GetPropertyValue<Holder, List<string>>(nameof(Holder.Items));
        var level = holder.GetPropertyValue<Holder, int?>(nameof(Holder.Level));

        Assert.Equal(new[] { "a", "b" }, items);
        Assert.True(level.HasValue);
        Assert.Equal(7, level!.Value);
    }

    /// <summary>
    /// 属性不存在时抛参数异常
    /// </summary>
    [Fact]
    public void GetPropertyValue_WhenPropertyMissing_Throws()
    {
        var holder = new Holder();

        var ex = Assert.Throws<ArgumentException>(() => holder.GetPropertyValue<Holder, List<string>>("NotExists"));
        Assert.Contains("NotExists", ex.Message);
    }

    /// <summary>
    /// 属性不是泛型类型时抛参数异常
    /// </summary>
    [Fact]
    public void GetPropertyValue_WhenPropertyIsNotGeneric_Throws()
    {
        var holder = new Holder();

        Assert.Throws<ArgumentException>(() => holder.GetPropertyValue<Holder, string>(nameof(Holder.Name)));
    }

    /// <summary>
    /// 可写泛型属性写入成功并返回真
    /// </summary>
    [Fact]
    public void SetPropertyValue_WhenWritableGenericProperty_SetsValueAndReturnsTrue()
    {
        var holder = new Holder();

        var changed = holder.SetPropertyValue<Holder, List<string>>(nameof(Holder.Items), ["x"]);

        Assert.True(changed);
        Assert.Equal(new[] { "x" }, holder.Items);
    }

    /// <summary>
    /// 只读泛型属性写入失败并返回假，且不改变原值
    /// </summary>
    [Fact]
    public void SetPropertyValue_WhenReadOnlyProperty_ReturnsFalse()
    {
        var holder = new Holder();

        var changed = holder.SetPropertyValue<Holder, List<int>>(nameof(Holder.ReadOnlyTags), [1, 2]);

        Assert.False(changed);
        Assert.Empty(holder.ReadOnlyTags);
    }

    /// <summary>
    /// 属性不存在时写入抛参数异常
    /// </summary>
    [Fact]
    public void SetPropertyValue_WhenPropertyMissing_Throws()
    {
        var holder = new Holder();

        Assert.Throws<ArgumentException>(() => holder.SetPropertyValue<Holder, List<string>>("NotExists", []));
    }

    /// <summary>
    /// 属性信息列表包含名称、类型名与字符串化的值
    /// </summary>
    [Fact]
    public void GetProperties_ReturnsNameTypeAndStringifiedValue()
    {
        var holder = new Holder { Name = "曦寒" };

        var properties = holder.GetProperties(true);

        var name = properties.Single(p => p.PropertyName == nameof(Holder.Name));
        Assert.Equal("String", name.PropertyType);
        Assert.Equal("曦寒", name.PropertyValue);
        Assert.Contains(properties, p => p.PropertyName == nameof(Holder.Items));
    }

    /// <summary>
    /// 属性值为 null 时，值字段为 null 而不是抛异常
    /// </summary>
    [Fact]
    public void GetProperties_WhenValueIsNull_KeepsNullValue()
    {
        var holder = new Holder { Items = null! };

        var properties = holder.GetProperties(false);

        var items = properties.Single(p => p.PropertyName == nameof(Holder.Items));
        Assert.Null(items.PropertyValue);
    }

    /// <summary>
    /// 默认包含两端边界
    /// </summary>
    [Fact]
    public void IsBetween_WithDefaultBounds_IncludesEndpoints()
    {
        Assert.True(GenericExtensions.IsBetween<int>(1, 1, 10));
        Assert.True(GenericExtensions.IsBetween<int>(10, 1, 10));
        Assert.True(GenericExtensions.IsBetween<int>(5, 1, 10));
        Assert.False(GenericExtensions.IsBetween<int>(0, 1, 10));
        Assert.False(GenericExtensions.IsBetween<int>(11, 1, 10));
    }

    /// <summary>
    /// 关闭边界包含后端点被排除
    /// </summary>
    [Fact]
    public void IsBetween_WhenEndpointsExcluded_ReturnsFalseOnBoundary()
    {
        Assert.False(GenericExtensions.IsBetween<int>(1, 1, 10, leftEqual: false));
        Assert.False(GenericExtensions.IsBetween<int>(10, 1, 10, rightEqual: false));
        Assert.True(GenericExtensions.IsBetween<int>(2, 1, 10, false, false));
    }

    /// <summary>
    /// 范围判断与边界开关语义一致
    /// </summary>
    [Fact]
    public void IsInRange_RespectsBoundaryFlags()
    {
        Assert.True(GenericExtensions.IsInRange<int>(1, 1, 10));
        Assert.True(GenericExtensions.IsInRange<int>(10, 1, 10));
        Assert.False(GenericExtensions.IsInRange<int>(1, 1, 10, minEqual: false));
        Assert.False(GenericExtensions.IsInRange<int>(10, 1, 10, maxEqual: false));
        Assert.False(GenericExtensions.IsInRange<int>(-1, 1, 10));
    }

    /// <summary>
    /// 惰性空序列（不实现 ICollection）
    /// </summary>
    private static IEnumerable<int> EmptyLazySequence()
    {
        yield break;
    }

    /// <summary>
    /// 每产出一个元素就记录一次，用来观察判空过程推进了几步
    /// </summary>
    private static IEnumerable<int> CountingSequence(List<int> pulled)
    {
        for (var i = 0; i < 3; i++)
        {
            pulled.Add(i);
            yield return i;
        }
    }

    /// <summary>
    /// 测试用承载类型：混合泛型属性、非泛型属性与只读属性
    /// </summary>
    private sealed class Holder
    {
        public List<string> Items { get; set; } = [];

        public int? Level { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<int> ReadOnlyTags { get; } = [];
    }
}
