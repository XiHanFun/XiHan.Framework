// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 类型扩展方法测试
/// </summary>
/// <remarks>
/// IsAssignableTo(Type,Type)/IsAssignableFrom(Type,Type) 与 <see cref="Type"/> 自带的同名实例方法冲突，
/// 实例语法永远走 BCL，所以这里用静态调用点名本仓实现。
/// IsAssignableFromGeneric 只覆盖"首轮即命中"的分支，非命中场景见交付报告的疑似缺陷段落（会死循环）。
/// </remarks>
public class TypeExtensionsTests
{
    /// <summary>
    /// 可空值类型判定
    /// </summary>
    [Fact]
    public void IsNullableTypeAndIsNotNullableType_AreComplementary()
    {
        Assert.True(typeof(int?).IsNullableType());
        Assert.False(typeof(int).IsNullableType());
        Assert.False(typeof(string).IsNullableType());

        Assert.False(typeof(int?).IsNotNullableType());
        Assert.True(typeof(int).IsNotNullableType());
    }

    /// <summary>
    /// 集合类型判定把字符串排除在外
    /// </summary>
    [Fact]
    public void IsEnumerable_ExcludesString()
    {
        Assert.True(typeof(List<int>).IsEnumerable());
        Assert.True(typeof(int[]).IsEnumerable());
        Assert.False(typeof(string).IsEnumerable());
        Assert.False(typeof(int).IsEnumerable());
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void IsEnumerable_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.IsEnumerable(null!));
    }

    /// <summary>
    /// 派生判定默认排除抽象类，打开开关后放行
    /// </summary>
    [Fact]
    public void IsAssignableTo_RespectsAbstractSwitch()
    {
        Assert.True(TypeExtensions.IsAssignableTo(typeof(Dog), typeof(Puppy)));
        Assert.False(TypeExtensions.IsAssignableTo(typeof(Animal), typeof(Dog)));
        Assert.True(TypeExtensions.IsAssignableTo(typeof(Animal), typeof(Dog), true));
        Assert.False(TypeExtensions.IsAssignableTo(typeof(Puppy), typeof(Dog)));
    }

    /// <summary>
    /// 泛型形式的派生判定与非泛型一致
    /// </summary>
    [Fact]
    public void IsAssignableToGenericParameterForm_MatchesNonGenericForm()
    {
        Assert.True(typeof(Dog).IsAssignableTo<Puppy>());
        Assert.False(typeof(Animal).IsAssignableTo<Dog>());
        Assert.True(typeof(Animal).IsAssignableTo<Dog>(true));
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void IsAssignableTo_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.IsAssignableTo(null!, typeof(Dog)));
    }

    /// <summary>
    /// 泛型定义派生判定覆盖接口、基类与自身
    /// </summary>
    [Fact]
    public void IsAssignableToGeneric_MatchesInterfaceAndSelf()
    {
        Assert.True(typeof(DogShelter).IsAssignableToGeneric(typeof(IShelter<>)));
        Assert.True(typeof(List<int>).IsAssignableToGeneric(typeof(List<>)));
        Assert.False(typeof(Dog).IsAssignableToGeneric(typeof(IShelter<>)));
    }

    /// <summary>
    /// 泛型参数形式的派生判定
    /// </summary>
    [Fact]
    public void IsAssignableToGeneric_WithTypeParameter_Works()
    {
        Assert.True(typeof(List<int>).IsAssignableToGeneric<List<int>>());
        Assert.True(typeof(DogShelter).IsAssignableToGeneric<IShelter<Dog>>());
    }

    /// <summary>
    /// 目标类型不是泛型时抛参数异常
    /// </summary>
    [Fact]
    public void IsAssignableToGeneric_WhenTargetIsNotGeneric_Throws()
    {
        Assert.Throws<ArgumentException>(() => typeof(Dog).IsAssignableToGeneric(typeof(Animal)));
    }

    /// <summary>
    /// 基类判定在目标是普通类型时走运行时语义
    /// </summary>
    [Fact]
    public void IsAssignableFrom_WithPlainBaseType_UsesRuntimeSemantics()
    {
        Assert.True(TypeExtensions.IsAssignableFrom(typeof(Dog), typeof(Animal)));
        Assert.False(TypeExtensions.IsAssignableFrom(typeof(Animal), typeof(Dog)));
        Assert.True(typeof(Animal).IsAssignableFrom<Dog>());
        Assert.False(typeof(Dog).IsAssignableFrom<Animal>());
    }

    /// <summary>
    /// 基类判定在目标是开放泛型定义时走泛型分支
    /// </summary>
    [Fact]
    public void IsAssignableFrom_WithOpenGenericBaseType_UsesGenericBranch()
    {
        Assert.True(TypeExtensions.IsAssignableFrom(typeof(List<int>), typeof(List<>)));
    }

    /// <summary>
    /// 泛型定义与构造类型首轮即命中
    /// </summary>
    [Fact]
    public void IsAssignableFromGeneric_WhenConstructedTypeMatchesDefinition_ReturnsTrue()
    {
        Assert.True(TypeExtensions.IsAssignableFromGeneric(typeof(List<>), typeof(List<int>)));
        Assert.True(typeof(List<>).IsAssignableFromGeneric<List<int>>());
        Assert.True(TypeExtensions.IsAssignableFromGeneric(typeof(IShelter<>), typeof(IShelter<Dog>)));
    }

    /// <summary>
    /// 调用方类型不是泛型时抛参数异常
    /// </summary>
    [Fact]
    public void IsAssignableFromGeneric_WhenCallerIsNotGeneric_Throws()
    {
        Assert.Throws<ArgumentException>(() => TypeExtensions.IsAssignableFromGeneric(typeof(Dog), typeof(Animal)));
    }

    /// <summary>
    /// 基类链按从远到近排列，可选是否包含 object
    /// </summary>
    [Fact]
    public void GetBaseClasses_ReturnsChainFromRootToDirectBase()
    {
        Assert.Equal([typeof(object), typeof(Animal), typeof(Dog)], typeof(Puppy).GetBaseClasses());
        Assert.Equal([typeof(Animal), typeof(Dog)], typeof(Puppy).GetBaseClasses(false));
    }

    /// <summary>
    /// 指定停止类型后不再向上回溯
    /// </summary>
    [Fact]
    public void GetBaseClasses_WithStoppingType_StopsClimbing()
    {
        var result = typeof(Puppy).GetBaseClasses(typeof(Animal));

        Assert.Contains(typeof(Dog), result);
        Assert.DoesNotContain(typeof(object), result);
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetBaseClasses_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.GetBaseClasses(null!));
    }

    /// <summary>
    /// 取可空类型的实际类型
    /// </summary>
    [Fact]
    public void GetNonNullableTypeAndGetUnNullableType_UnwrapNullable()
    {
        Assert.Equal(typeof(int), typeof(int?).GetNonNullableType());
        Assert.Equal(typeof(int), typeof(int).GetNonNullableType());
        Assert.Equal(typeof(int), typeof(int?).GetUnNullableType());
        Assert.Equal(typeof(string), typeof(string).GetUnNullableType());
    }

    /// <summary>
    /// 有描述特性时输出"全名(描述)"，没有时输出空串
    /// </summary>
    [Fact]
    public void GetDescription_UsesAttributeOrReturnsEmpty()
    {
        var description = typeof(Described).GetDescription();

        Assert.Contains("类型描述", description);
        Assert.StartsWith(typeof(Described).FullName!, description);
        Assert.Equal(string.Empty, typeof(Dog).GetDescription());
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetDescription_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.GetDescription(null!));
    }

    /// <summary>
    /// 全名带程序集名与所在模块名
    /// </summary>
    [Fact]
    public void GetFullNameWithAssemblyNameAndModule_AppendContainerName()
    {
        var type = typeof(Dog);

        Assert.Equal($"{type.FullName},XiHan.Framework.Utils.Tests", type.GetFullNameWithAssemblyName());
        Assert.Equal($"{type.FullName},XiHan.Framework.Utils.Tests", type.GetFullNameWithModule());
    }

    /// <summary>
    /// 显示名称对内置类型使用关键字写法
    /// </summary>
    [Fact]
    public void GetDisplayName_UsesKeywordsForBuiltInTypes()
    {
        Assert.Equal("int", typeof(int).GetDisplayName());
        Assert.Equal("string", typeof(string).GetDisplayName());
        Assert.Equal("bool", typeof(bool).GetDisplayName());
        Assert.Equal("object", typeof(object).GetDisplayName());
    }

    /// <summary>
    /// 显示名称对数组保留维度记号
    /// </summary>
    [Fact]
    public void GetDisplayName_KeepsArrayRank()
    {
        Assert.Equal("int[]", typeof(int[]).GetDisplayName());
        Assert.Equal("int[,]", typeof(int[,]).GetDisplayName());
    }

    /// <summary>
    /// 显示名称对泛型展开类型实参
    /// </summary>
    [Fact]
    public void GetDisplayName_ExpandsGenericArguments()
    {
        Assert.Equal("System.Collections.Generic.List<int>", typeof(List<int>).GetDisplayName());
        Assert.Equal("System.Collections.Generic.Dictionary<string, int>", typeof(Dictionary<string, int>).GetDisplayName());
    }

    /// <summary>
    /// 短显示名称去掉命名空间
    /// </summary>
    [Fact]
    public void GetShortDisplayName_DropsNamespace()
    {
        Assert.Equal("List<int>", typeof(List<int>).GetShortDisplayName());
        Assert.Equal("int", typeof(int).GetShortDisplayName());
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetDisplayName_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.GetDisplayName(null!));
        Assert.Throws<ArgumentNullException>(() => TypeExtensions.GetShortDisplayName(null!));
    }

    /// <summary>
    /// 测试用抽象基类
    /// </summary>
    private abstract class Animal
    {
    }

    /// <summary>
    /// 测试用中间类
    /// </summary>
    private class Dog : Animal
    {
    }

    /// <summary>
    /// 测试用叶子类
    /// </summary>
    private sealed class Puppy : Dog
    {
    }

    /// <summary>
    /// 测试用泛型接口
    /// </summary>
    /// <typeparam name="T">收容对象类型</typeparam>
    private interface IShelter<T>
    {
    }

    /// <summary>
    /// 实现泛型接口的测试类
    /// </summary>
    private sealed class DogShelter : IShelter<Dog>
    {
    }

    /// <summary>
    /// 带描述特性的测试类
    /// </summary>
    [Description("类型描述")]
    private sealed class Described
    {
    }
}
