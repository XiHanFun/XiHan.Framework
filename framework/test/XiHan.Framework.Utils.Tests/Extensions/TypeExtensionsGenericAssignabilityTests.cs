// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 类型扩展中泛型可派生判定与基类链回溯的回归测试
/// </summary>
/// <remarks>
/// IsAssignableFromGeneric 原本在内层 while 里写 if (cur.BaseType is not null) { cur = cur.BaseType; }：
/// 一旦爬到 System.Object（BaseType 为 null）或本身是接口且仍未命中，cur 就不再前进，
/// 而循环条件 cur is not null 永远成立——死循环挂死线程。
/// 接口场景尤其隐蔽：候选列表第一个元素是具体类，爬到 object 就挂了，后面的接口一个都轮不到。
/// 用例统一加超时，缺陷未修时会以超时失败而不是无限挂住。
/// GetBaseClasses(stoppingType) 一条锁的是「停止类型本身不在结果里」这一实际语义（注释已同步订正）。
/// </remarks>
public class TypeExtensionsGenericAssignabilityTests
{
    /// <summary>
    /// 与目标泛型完全无关的类型返回 false，且不会死循环
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void IsAssignableFromGeneric_WhenUnrelated_ReturnsFalse()
    {
        Assert.False(typeof(List<>).IsAssignableFromGeneric(typeof(string)));
    }

    /// <summary>
    /// 无关类型爬到 object 之后应继续走完候选列表并返回 false
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void IsAssignableFromGeneric_WhenUnrelatedCustomType_ReturnsFalse()
    {
        Assert.False(typeof(List<>).IsAssignableFromGeneric(typeof(Puppy)));
    }

    /// <summary>
    /// 目标是泛型接口时，具体类走完基类链后应继续检查其实现的接口
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void IsAssignableFromGeneric_WithGenericInterface_ChecksInterfacesAfterClassChain()
    {
        Assert.True(typeof(IEnumerable<>).IsAssignableFromGeneric(typeof(List<int>)));
    }

    /// <summary>
    /// 目标是泛型接口且实参不实现它时返回 false
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void IsAssignableFromGeneric_WithGenericInterface_WhenNotImplemented_ReturnsFalse()
    {
        Assert.False(typeof(IDictionary<,>).IsAssignableFromGeneric(typeof(Puppy)));
    }

    /// <summary>
    /// 构造泛型与自身的泛型定义匹配
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void IsAssignableFromGeneric_WhenSameDefinition_ReturnsTrue()
    {
        Assert.True(typeof(List<>).IsAssignableFromGeneric(typeof(List<string>)));
    }

    /// <summary>
    /// 停止类型是排他边界：它自己不出现在结果里，其上的基类也不再回溯
    /// </summary>
    [Fact]
    public void GetBaseClasses_WithStoppingType_ExcludesStoppingTypeItself()
    {
        var result = typeof(Puppy).GetBaseClasses(typeof(Animal));

        Assert.Equal([typeof(Dog)], result);
        Assert.DoesNotContain(typeof(Animal), result);
        Assert.DoesNotContain(typeof(object), result);
    }

    /// <summary>
    /// 停止类型取直接基类时结果为空
    /// </summary>
    [Fact]
    public void GetBaseClasses_WhenStoppingTypeIsDirectBase_ReturnsEmpty()
    {
        Assert.Empty(typeof(Puppy).GetBaseClasses(typeof(Dog)));
    }

    /// <summary>
    /// 停止类型不在基类链上时退化为普通回溯
    /// </summary>
    [Fact]
    public void GetBaseClasses_WhenStoppingTypeNotOnChain_ClimbsToObject()
    {
        var result = typeof(Puppy).GetBaseClasses(typeof(string));

        Assert.Equal([typeof(object), typeof(Animal), typeof(Dog)], result);
    }

    /// <summary>
    /// 测试用基类
    /// </summary>
    private class Animal
    {
    }

    /// <summary>
    /// 测试用中间类
    /// </summary>
    private class Dog : Animal
    {
    }

    /// <summary>
    /// 测试用派生类
    /// </summary>
    private sealed class Puppy : Dog
    {
    }
}
