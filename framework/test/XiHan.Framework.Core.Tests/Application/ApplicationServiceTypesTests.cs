// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 应用服务类型枚举测试
/// </summary>
/// <remarks>
/// 这是个 <see cref="FlagsAttribute"/> 位枚举，底层类型与各位的数值参与位运算判断，
/// 一旦重排数值，所有按位过滤应用服务／集成服务的调用点都会静默改变行为，因此逐个锁死。
/// </remarks>
public class ApplicationServiceTypesTests
{
    /// <summary>
    /// 各枚举项的数值固定不变
    /// </summary>
    /// <param name="value">枚举项</param>
    /// <param name="expected">期望数值</param>
    [Theory]
    [InlineData(ApplicationServiceTypes.ApplicationServices, 1)]
    [InlineData(ApplicationServiceTypes.IntegrationServices, 2)]
    [InlineData(ApplicationServiceTypes.All, 3)]
    public void EnumValues_AreStable(ApplicationServiceTypes value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    /// <summary>
    /// 底层类型为字节，位枚举特性已标注
    /// </summary>
    [Fact]
    public void Enum_IsFlagsBackedByByte()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(ApplicationServiceTypes)));
        Assert.True(typeof(ApplicationServiceTypes).IsDefined(typeof(FlagsAttribute), false));
    }

    /// <summary>
    /// 全部等于应用服务与集成服务的按位或
    /// </summary>
    [Fact]
    public void All_IsUnionOfApplicationAndIntegrationServices()
    {
        Assert.Equal(ApplicationServiceTypes.All, ApplicationServiceTypes.ApplicationServices | ApplicationServiceTypes.IntegrationServices);
    }

    /// <summary>
    /// 全部同时包含两个子项，而子项互不包含
    /// </summary>
    [Fact]
    public void HasFlag_FollowsBitwiseSemantics()
    {
        Assert.True(ApplicationServiceTypes.All.HasFlag(ApplicationServiceTypes.ApplicationServices));
        Assert.True(ApplicationServiceTypes.All.HasFlag(ApplicationServiceTypes.IntegrationServices));
        Assert.False(ApplicationServiceTypes.ApplicationServices.HasFlag(ApplicationServiceTypes.IntegrationServices));
        Assert.False(ApplicationServiceTypes.IntegrationServices.HasFlag(ApplicationServiceTypes.ApplicationServices));
    }

    /// <summary>
    /// 枚举只定义三个成员，没有隐藏项
    /// </summary>
    [Fact]
    public void Enum_DefinesExactlyThreeMembers()
    {
        var names = Enum.GetNames<ApplicationServiceTypes>().OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] expected =
        [
            nameof(ApplicationServiceTypes.All),
            nameof(ApplicationServiceTypes.ApplicationServices),
            nameof(ApplicationServiceTypes.IntegrationServices)
        ];

        Assert.Equal(expected, names);
    }

    /// <summary>
    /// 没有代表"无"的零值项，调用方不能用默认值表达空集合
    /// </summary>
    [Fact]
    public void Enum_HasNoZeroValue()
    {
        Assert.False(Enum.IsDefined(default(ApplicationServiceTypes)));
    }
}
