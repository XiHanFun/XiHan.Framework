// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Attributes;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Attributes;

/// <summary>
/// 后台作业名称特性测试
/// </summary>
/// <remarks>
/// 作业名是持久化进存储的稳定标识：入队时写进记录，执行时反查注册表。
/// 因此这里锁的是三件事——标注优先、无标注回退完整类型名、特性不随继承传播
/// （特性若被继承，父子两个参数类型会算出同一个名字，注册表按名索引会互相覆盖）。
/// </remarks>
public class BackgroundJobNameAttributeTests
{
    /// <summary>
    /// 构造时保存名称
    /// </summary>
    [Fact]
    public void Constructor_KeepsGivenName()
    {
        var attribute = new BackgroundJobNameAttribute("order-created");

        Assert.Equal("order-created", attribute.Name);
    }

    /// <summary>
    /// 名称为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenNameNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BackgroundJobNameAttribute(null!));
    }

    /// <summary>
    /// 名称为空串或纯空白时抛出参数异常
    /// </summary>
    /// <param name="name">名称</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WhenNameBlank_ThrowsArgumentException(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BackgroundJobNameAttribute(name));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 参数类型带标注时取标注名
    /// </summary>
    [Fact]
    public void GetName_WhenAttributePresent_ReturnsAttributeName()
    {
        Assert.Equal("xihan-tests-named-args", BackgroundJobNameAttribute.GetName(typeof(NamedJobArgs)));
    }

    /// <summary>
    /// 参数类型无标注时回退为完整类型名
    /// </summary>
    [Fact]
    public void GetName_WhenAttributeMissing_ReturnsFullTypeName()
    {
        Assert.Equal(typeof(UnnamedJobArgs).FullName, BackgroundJobNameAttribute.GetName(typeof(UnnamedJobArgs)));
    }

    /// <summary>
    /// 特性不被继承：派生参数类型仍回退为自己的完整类型名
    /// </summary>
    [Fact]
    public void GetName_WhenTypeDerivesFromAnnotatedType_DoesNotInheritName()
    {
        var name = BackgroundJobNameAttribute.GetName(typeof(DerivedNamedJobArgs));

        Assert.Equal(typeof(DerivedNamedJobArgs).FullName, name);
        Assert.NotEqual("xihan-tests-named-args", name);
    }

    /// <summary>
    /// 类型为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void GetName_WhenTypeNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BackgroundJobNameAttribute.GetName(null!));
    }

    /// <summary>
    /// 特性实现名称提供器接口，便于按接口统一解析
    /// </summary>
    [Fact]
    public void Attribute_ImplementsNameProvider()
    {
        var attribute = new BackgroundJobNameAttribute("any");

        Assert.IsAssignableFrom<IBackgroundJobNameProvider>(attribute);
    }

    /// <summary>
    /// 特性只允许标注在类上且不被继承
    /// </summary>
    [Fact]
    public void Attribute_UsageIsClassOnlyAndNotInherited()
    {
        var usage = typeof(BackgroundJobNameAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.Inherited);
    }
}
