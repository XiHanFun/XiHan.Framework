// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Localization.Abstractions.Enums;
using XiHan.Framework.Localization.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Localization.Abstractions.Tests.Enums;

/// <summary>
/// 枚举本地化服务接口契约测试
/// </summary>
/// <remarks>
/// 抽象包只声明接口，没有实现，所以这里测的是"接口形状"本身：
/// 两个同名 Get 重载在按类型/按名称调用时不会互相抢占、可选查询参数默认值确实是 null、
/// TryGet 的 out 结果在失败时可以是 null。这些一旦改动会让所有下游实现同时炸开。
/// </remarks>
public class IEnumLocalizationServiceTests
{
    /// <summary>
    /// 省略查询参数时，实现收到的必须是 null（而不是某个默认实例）
    /// </summary>
    [Fact]
    public void Get_WhenQueryOmitted_ImplementationReceivesNullQuery()
    {
        var service = CreateService();

        _ = service.Get("UserStatus");

        Assert.Equal(1, service.ReceivedQueries.Count);
        Assert.Null(service.ReceivedQueries[0]);
    }

    /// <summary>
    /// 显式传入查询参数时原样透传
    /// </summary>
    [Fact]
    public void Get_WhenQueryProvided_ImplementationReceivesSameInstance()
    {
        var service = CreateService();
        var query = new EnumLocalizationQuery { CultureName = "zh-CN", IncludeHidden = true };

        _ = service.Get("UserStatus", query);

        Assert.Equal(1, service.ReceivedQueries.Count);
        Assert.Same(query, service.ReceivedQueries[0]);
    }

    /// <summary>
    /// 按类型调用时选中的是 Type 重载，不会被 string 重载抢走
    /// </summary>
    [Fact]
    public void Get_ByType_ResolvesThroughTypeOverload()
    {
        var service = CreateService("DayOfWeek");

        var definition = service.Get(typeof(DayOfWeek));

        Assert.Equal("DayOfWeek", definition.EnumName);
    }

    /// <summary>
    /// 未登记的类型名，TryGet 返回 false 且结果为 null
    /// </summary>
    [Fact]
    public void TryGet_WhenUnknownName_ReturnsFalseAndNullResult()
    {
        var service = CreateService();

        var found = service.TryGet("NotRegistered", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    /// <summary>
    /// 已登记的类型名，TryGet 返回 true 且带出描述
    /// </summary>
    [Fact]
    public void TryGet_WhenKnownName_ReturnsTrueAndDefinition()
    {
        var service = CreateService();

        var found = service.TryGet("UserStatus", out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("UserStatus", result!.EnumName);
    }

    /// <summary>
    /// 批量读取只返回已登记的类型名，不为缺失项占位
    /// </summary>
    [Fact]
    public void GetMany_SkipsUnknownNames()
    {
        var service = CreateService();

        var many = service.GetMany(["UserStatus", "NotRegistered"]);

        Assert.Equal(1, many.Count);
        Assert.True(many.ContainsKey("UserStatus"));
        Assert.False(many.ContainsKey("NotRegistered"));
    }

    /// <summary>
    /// 批量读取空列表时返回空字典而不是 null
    /// </summary>
    [Fact]
    public void GetMany_WhenNamesEmpty_ReturnsEmptyDictionary()
    {
        var service = CreateService();

        var many = service.GetMany([]);

        Assert.NotNull(many);
        Assert.Empty(many);
    }

    /// <summary>
    /// 构造一个预置了枚举描述的服务替身
    /// </summary>
    /// <param name="enumName">登记的枚举类型名</param>
    /// <returns>服务替身</returns>
    private static FakeEnumLocalizationService CreateService(string enumName = "UserStatus")
    {
        var definition = new LocalizedEnumDefinition
        {
            EnumName = enumName,
            FullName = "XiHan.Demo." + enumName,
            CultureName = "zh-CN",
            UnderlyingTypeName = "Int32",
            Items =
            [
                new LocalizedEnumItem { Name = "Active", ValueText = "1", Label = "启用", Order = 1 }
            ]
        };

        return new FakeEnumLocalizationService(new Dictionary<string, LocalizedEnumDefinition>
        {
            [enumName] = definition
        });
    }
}
