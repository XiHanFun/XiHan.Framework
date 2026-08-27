// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Reflection;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 反射帮助类测试
/// </summary>
/// <remarks>
/// 只覆盖不依赖运行环境的确定性能力：类型判定、特性读取、属性路径取值、常量收集。
/// 扫描当前进程全部程序集的那批方法（GetAllAssemblies/GetSubClasses/GetNuGetPackages 等）
/// 结果随宿主与加载顺序变化，不适合写成断言，见交付报告的未覆盖说明。
/// </remarks>
public class ReflectionHelperTests
{
    /// <summary>
    /// 自身即目标泛型定义时判为可赋值
    /// </summary>
    [Fact]
    public void IsAssignableToGenericType_WhenTypeItselfMatches_ReturnsTrue()
    {
        Assert.True(ReflectionHelper.IsAssignableToGenericType(typeof(List<int>), typeof(List<>)));
    }

    /// <summary>
    /// 实现了目标泛型接口时判为可赋值
    /// </summary>
    [Fact]
    public void IsAssignableToGenericType_WhenImplementsGenericInterface_ReturnsTrue()
    {
        Assert.True(ReflectionHelper.IsAssignableToGenericType(typeof(DogShelter), typeof(IShelter<>)));
    }

    /// <summary>
    /// 完全无关的类型判为不可赋值
    /// </summary>
    [Fact]
    public void IsAssignableToGenericType_WhenUnrelated_ReturnsFalse()
    {
        Assert.False(ReflectionHelper.IsAssignableToGenericType(typeof(string), typeof(List<>)));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void IsAssignableToGenericType_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.IsAssignableToGenericType(null!, typeof(List<>)));
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.IsAssignableToGenericType(typeof(string), null!));
    }

    /// <summary>
    /// 取出实现的具体泛型类型
    /// </summary>
    [Fact]
    public void GetImplementedGenericTypes_ReturnsConstructedInterface()
    {
        var implemented = ReflectionHelper.GetImplementedGenericTypes(typeof(DogShelter), typeof(IShelter<>));

        Assert.Contains(typeof(IShelter<string>), implemented);
    }

    /// <summary>
    /// 没有实现时返回空列表
    /// </summary>
    [Fact]
    public void GetImplementedGenericTypes_WhenNotImplemented_ReturnsEmpty()
    {
        Assert.Empty(ReflectionHelper.GetImplementedGenericTypes(typeof(string), typeof(IShelter<>)));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetImplementedGenericTypes_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetImplementedGenericTypes(null!, typeof(IShelter<>)));
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetImplementedGenericTypes(typeof(string), null!));
    }

    /// <summary>
    /// 成员上有特性时取到特性，没有时取到默认值
    /// </summary>
    [Fact]
    public void GetSingleAttributeOrDefault_ReturnsAttributeOrGivenDefault()
    {
        var withAttribute = GetProperty(nameof(Annotated.WithAttribute));
        var plain = GetProperty(nameof(Annotated.Plain));
        var fallback = new DescriptionAttribute("兜底");

        Assert.Equal("成员描述", ReflectionHelper.GetSingleAttributeOrDefault<DescriptionAttribute>(withAttribute)!.Description);
        Assert.Null(ReflectionHelper.GetSingleAttributeOrDefault<DescriptionAttribute>(plain));
        Assert.Same(fallback, ReflectionHelper.GetSingleAttributeOrDefault(plain, fallback));
    }

    /// <summary>
    /// 成员为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetSingleAttributeOrDefault_WhenMemberIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReflectionHelper.GetSingleAttributeOrDefault<DescriptionAttribute>(null!));
    }

    /// <summary>
    /// 成员没有特性时回落到声明类型上的特性
    /// </summary>
    [Fact]
    public void GetSingleAttributeOfMemberOrDeclaringTypeOrDefault_FallsBackToDeclaringType()
    {
        var plain = GetProperty(nameof(Annotated.Plain));
        var withAttribute = GetProperty(nameof(Annotated.WithAttribute));

        var fromType = ReflectionHelper.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<DescriptionAttribute>(plain);
        var fromMember = ReflectionHelper.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<DescriptionAttribute>(withAttribute);

        Assert.Equal("类型描述", fromType!.Description);
        Assert.Equal("成员描述", fromMember!.Description);
    }

    /// <summary>
    /// 成员与声明类型上的特性会被一并取出
    /// </summary>
    [Fact]
    public void GetAttributesOfMemberOrDeclaringType_MergesMemberAndTypeAttributes()
    {
        var withAttribute = GetProperty(nameof(Annotated.WithAttribute));

        var descriptions = ReflectionHelper
            .GetAttributesOfMemberOrDeclaringType<DescriptionAttribute>(withAttribute)
            .Select(a => a.Description)
            .ToList();

        Assert.Contains("成员描述", descriptions);
        Assert.Contains("类型描述", descriptions);
    }

    /// <summary>
    /// 成员为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void AttributeReaders_WhenMemberIsNull_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReflectionHelper.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<DescriptionAttribute>(null!));
        Assert.Throws<ArgumentNullException>(() =>
            ReflectionHelper.GetAttributesOfMemberOrDeclaringType<DescriptionAttribute>(null!).ToList());
    }

    /// <summary>
    /// 按属性路径逐级取值
    /// </summary>
    [Fact]
    public void GetValueByPath_WalksNestedProperties()
    {
        var root = new Root();

        Assert.Equal("根", ReflectionHelper.GetValueByPath(root, typeof(Root), nameof(Root.Title)));
        Assert.Equal("子", ReflectionHelper.GetValueByPath(root, typeof(Root), $"{nameof(Root.Child)}.{nameof(Child.Name)}"));
    }

    /// <summary>
    /// 路径以类型全名开头时会先剥掉前缀
    /// </summary>
    [Fact]
    public void GetValueByPath_StripsTypeFullNamePrefix()
    {
        var root = new Root();
        var path = $"{typeof(Root).FullName}.{nameof(Root.Title)}";

        Assert.Equal("根", ReflectionHelper.GetValueByPath(root, typeof(Root), path));
    }

    /// <summary>
    /// 路径中出现不存在的属性时返回 null
    /// </summary>
    [Fact]
    public void GetValueByPath_WhenPropertyMissing_ReturnsNull()
    {
        var root = new Root();

        Assert.Null(ReflectionHelper.GetValueByPath(root, typeof(Root), "NotExists"));
        Assert.Null(ReflectionHelper.GetValueByPath(root, typeof(Root), $"{nameof(Root.Child)}.NotExists"));
    }

    /// <summary>
    /// 入参非法时抛异常
    /// </summary>
    [Fact]
    public void GetValueByPath_WhenArgumentInvalid_Throws()
    {
        var root = new Root();

        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetValueByPath(null!, typeof(Root), "Title"));
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetValueByPath(root, null!, "Title"));
        Assert.Throws<ArgumentException>(() => ReflectionHelper.GetValueByPath(root, typeof(Root), "   "));
    }

    /// <summary>
    /// 递归收集公有常量，包含嵌套类型里的常量
    /// </summary>
    [Fact]
    public void GetPublicConstantsRecursively_IncludesNestedTypes()
    {
        var constants = ReflectionHelper.GetPublicConstantsRecursively(typeof(Constants));

        Assert.Contains("第一", constants);
        Assert.Contains("第二", constants);
        Assert.Contains("第三", constants);
    }

    /// <summary>
    /// 没有常量的类型返回空数组
    /// </summary>
    [Fact]
    public void GetPublicConstantsRecursively_WhenNoConstants_ReturnsEmpty()
    {
        Assert.Empty(ReflectionHelper.GetPublicConstantsRecursively(typeof(Root)));
    }

    /// <summary>
    /// 类型为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetPublicConstantsRecursively_WhenTypeIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetPublicConstantsRecursively(null!));
    }

    /// <summary>
    /// 取指定程序集的全部类型
    /// </summary>
    [Fact]
    public void GetAllTypes_OfGivenAssembly_ContainsItsOwnTypes()
    {
        var assembly = typeof(ReflectionHelperTests).Assembly;

        var types = ReflectionHelper.GetAllTypes(assembly);

        Assert.Contains(typeof(ReflectionHelperTests), types);
    }

    /// <summary>
    /// 程序集为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetAllTypes_WhenAssemblyIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReflectionHelper.GetAllTypes((Assembly)null!));
    }

    /// <summary>
    /// 取测试类型的属性成员信息
    /// </summary>
    private static PropertyInfo GetProperty(string name)
    {
        return typeof(Annotated).GetProperty(name)!;
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
    private sealed class DogShelter : IShelter<string>
    {
    }

    /// <summary>
    /// 类型与成员都带描述特性的测试类
    /// </summary>
    [Description("类型描述")]
    private sealed class Annotated
    {
        /// <summary>
        /// 带成员级描述
        /// </summary>
        [Description("成员描述")]
        public string WithAttribute { get; set; } = string.Empty;

        /// <summary>
        /// 不带成员级描述
        /// </summary>
        public string Plain { get; set; } = string.Empty;
    }

    /// <summary>
    /// 测试用根对象
    /// </summary>
    private sealed class Root
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = "根";

        /// <summary>
        /// 子对象
        /// </summary>
        public Child Child { get; set; } = new();
    }

    /// <summary>
    /// 测试用子对象
    /// </summary>
    private sealed class Child
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = "子";
    }

    /// <summary>
    /// 测试用常量容器
    /// </summary>
    private static class Constants
    {
        /// <summary>
        /// 第一个常量
        /// </summary>
        public const string First = "第一";

        /// <summary>
        /// 第二个常量
        /// </summary>
        public const string Second = "第二";

        /// <summary>
        /// 嵌套常量容器
        /// </summary>
        public static class Nested
        {
            /// <summary>
            /// 第三个常量
            /// </summary>
            public const string Third = "第三";
        }
    }
}
