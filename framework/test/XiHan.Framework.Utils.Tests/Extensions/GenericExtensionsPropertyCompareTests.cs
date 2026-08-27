// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 泛型扩展中属性下钻与属性差异对比的回归测试
/// </summary>
/// <remarks>
/// 锁两条缺陷：
/// 一是 GetPropertyDeepestValue 的 while 循环体内既不更新 entity 也不更新 propertyName，
/// 只要属性值非 null 就会拿同一个实例反复取同一个属性，永远转不出去（挂死线程）；
/// 二是 GetPropertiesDetailedCompare 里的 ConvertTo(type) 实际绑定到泛型重载
/// ConvertTo&lt;T&gt;(object?, T defaultValue)，T 被推断成 System.Type、type 成了默认值，
/// 于是 before/after 恒等于同一个 Type 对象，任何差异都比不出来（返回空列表）。
/// </remarks>
public class GenericExtensionsPropertyCompareTests
{
    /// <summary>
    /// 沿同名属性逐级下钻，返回最深一层的非 null 实体
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetPropertyDeepestValue_DrillsDownToDeepestNonNullEntity()
    {
        var deepest = new Chain<int> { Payload = 3 };
        var middle = new Chain<int> { Payload = 2, Next = deepest };
        var root = new Chain<int> { Payload = 1, Next = middle };

        var result = root.GetPropertyDeepestValue("Next");

        Assert.Same(deepest, result);
    }

    /// <summary>
    /// 属性本身就是 null 时原样返回入参实体
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetPropertyDeepestValue_WhenPropertyIsNull_ReturnsSelf()
    {
        var root = new Chain<int> { Payload = 1 };

        Assert.Same(root, root.GetPropertyDeepestValue("Next"));
    }

    /// <summary>
    /// 只差一级时返回下一级实体
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetPropertyDeepestValue_WithSingleLevel_ReturnsNext()
    {
        var next = new Chain<int> { Payload = 2 };
        var root = new Chain<int> { Payload = 1, Next = next };

        Assert.Same(next, root.GetPropertyDeepestValue("Next"));
    }

    /// <summary>
    /// 属性值真的不同时才记差异，比较的是属性真实值
    /// </summary>
    [Fact]
    public void GetPropertiesDetailedCompare_ReportsRealValueDifference()
    {
        var oldEntity = new Person { Name = "旧", Age = 18 };
        var newEntity = new Person { Name = "新", Age = 18 };

        var diff = oldEntity.GetPropertiesDetailedCompare(newEntity, null);

        var item = Assert.Single(diff);
        Assert.Equal("Name", item.PropertyName);
        Assert.Equal("旧", item.Before);
        Assert.Equal("新", item.After);
    }

    /// <summary>
    /// 值类型属性的差异同样能被识别
    /// </summary>
    [Fact]
    public void GetPropertiesDetailedCompare_DetectsValueTypeDifference()
    {
        var oldEntity = new Person { Name = "同", Age = 18 };
        var newEntity = new Person { Name = "同", Age = 20 };

        var diff = oldEntity.GetPropertiesDetailedCompare(newEntity, null);

        var item = Assert.Single(diff);
        Assert.Equal("Age", item.PropertyName);
        Assert.Equal("18", item.Before);
        Assert.Equal("20", item.After);
    }

    /// <summary>
    /// 两个实体完全相同时不产生任何差异记录
    /// </summary>
    [Fact]
    public void GetPropertiesDetailedCompare_WhenIdentical_ReturnsEmpty()
    {
        var oldEntity = new Person { Name = "同", Age = 18 };
        var newEntity = new Person { Name = "同", Age = 18 };

        Assert.Empty(oldEntity.GetPropertiesDetailedCompare(newEntity, null));
    }

    /// <summary>
    /// 一侧为 null 时记录真实值，而不是把类型名写进差异
    /// </summary>
    [Fact]
    public void GetPropertiesDetailedCompare_WhenOneSideIsNull_UsesRealValues()
    {
        var oldEntity = new Person { Name = null, Age = 18 };
        var newEntity = new Person { Name = "新", Age = 18 };

        var diff = oldEntity.GetPropertiesDetailedCompare(newEntity, null);

        var item = Assert.Single(diff);
        Assert.Equal("Name", item.PropertyName);
        Assert.Equal(string.Empty, item.Before);
        Assert.Equal("新", item.After);
    }

    /// <summary>
    /// 排除列表中的属性不出现在差异结果里
    /// </summary>
    [Fact]
    public void GetPropertiesDetailedCompare_WithSpecialList_ExcludesNamedProperties()
    {
        var oldEntity = new Person { Name = "旧", Age = 18 };
        var newEntity = new Person { Name = "新", Age = 20 };

        var diff = oldEntity.GetPropertiesDetailedCompare(newEntity, ["Name"]);

        var item = Assert.Single(diff);
        Assert.Equal("Age", item.PropertyName);
    }

    /// <summary>
    /// 差异说明 Json 里带上真实的新旧值
    /// </summary>
    [Fact]
    public void GetPropertiesChangedNote_ContainsRealValues()
    {
        // 这里刻意用 ASCII 取值：断言只关心"新旧真实值进了 Json"，
        // 不想被 Json 的编码器/缩进/命名策略这些与本缺陷无关的设置牵连。
        var oldEntity = new Person { Name = "old", Age = 18 };
        var newEntity = new Person { Name = "new", Age = 18 };

        var note = oldEntity.GetPropertiesChangedNote(newEntity, null);

        Assert.Contains("Name", note);
        Assert.Contains("old", note);
        Assert.Contains("new", note);
    }

    /// <summary>
    /// 测试用链式实体：属性类型与实体类型相同且为泛型，满足 GetPropertyValue 的约束
    /// </summary>
    private sealed class Chain<T>
    {
        public Chain<T>? Next { get; set; }

        public T? Payload { get; set; }
    }

    /// <summary>
    /// 测试用对比实体
    /// </summary>
    private sealed class Person
    {
        public string? Name { get; set; }

        public int Age { get; set; }
    }
}
