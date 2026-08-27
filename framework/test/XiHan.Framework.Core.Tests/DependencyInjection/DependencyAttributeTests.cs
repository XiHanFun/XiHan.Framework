// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 依赖特性测试
/// </summary>
/// <remarks>
/// 依赖特性的三个开关直接决定约定注册走 Add / TryAdd / Replace 哪条分支，
/// 默认值一旦漂移会让所有未显式声明的服务改变注册语义，因此逐个锁死。
/// </remarks>
public class DependencyAttributeTests
{
    /// <summary>
    /// 无参构造时三个开关都保持中立
    /// </summary>
    [Fact]
    public void Constructor_WhenParameterless_LeavesAllSwitchesNeutral()
    {
        var attribute = new DependencyAttribute();

        Assert.Null(attribute.Lifetime);
        Assert.False(attribute.TryRegister);
        Assert.False(attribute.ReplaceServices);
    }

    /// <summary>
    /// 带生命周期构造时写入生命周期
    /// </summary>
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Constructor_WithLifetime_KeepsGivenLifetime(ServiceLifetime lifetime)
    {
        var attribute = new DependencyAttribute(lifetime);

        Assert.Equal(lifetime, attribute.Lifetime);
    }

    /// <summary>
    /// 可从类型上读回特性声明
    /// </summary>
    [Fact]
    public void GetCustomAttribute_ReadsDeclaredSwitchesFromType()
    {
        var attribute = typeof(DepAnnotatedService).GetCustomAttribute<DependencyAttribute>(true);

        Assert.NotNull(attribute);
        Assert.Equal(ServiceLifetime.Scoped, attribute.Lifetime);
        Assert.True(attribute.TryRegister);
        Assert.True(attribute.ReplaceServices);
    }

    /// <summary>
    /// 特性可被子类继承读取
    /// </summary>
    [Fact]
    public void GetCustomAttribute_WhenDeclaredOnBaseClass_IsInheritedByDerived()
    {
        var attribute = typeof(DepDerivedService).GetCustomAttribute<DependencyAttribute>(true);

        Assert.NotNull(attribute);
        Assert.Equal(ServiceLifetime.Scoped, attribute.Lifetime);
    }

    /// <summary>
    /// 未声明特性的类型读不到特性
    /// </summary>
    [Fact]
    public void GetCustomAttribute_WhenNotDeclared_ReturnsNull()
    {
        Assert.Null(typeof(DepPlainService).GetCustomAttribute<DependencyAttribute>(true));
    }
}

/// <summary>
/// 声明了完整依赖特性的服务
/// </summary>
[Dependency(ServiceLifetime.Scoped, TryRegister = true, ReplaceServices = true)]
internal class DepAnnotatedService;

/// <summary>
/// 继承自带依赖特性基类的服务
/// </summary>
internal class DepDerivedService : DepAnnotatedService;

/// <summary>
/// 未声明依赖特性的服务
/// </summary>
internal class DepPlainService;
