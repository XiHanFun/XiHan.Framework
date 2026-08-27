// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Core.Data;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 租户配置的测试
/// </summary>
/// <remarks>
/// 这个类型同时承担两个角色：配置绑定的目标（无参构造 + 可写属性）和租户存储的返回载体。
/// 因此用例分三块钉住：三个构造函数各自的初始化差异、空值参数的异常契约、连接字符串取值语义。
/// 它没有重写相等性，租户存储返回的又是克隆体，所以「按值不相等」这条也必须显式记录，
/// 免得调用方误用 Equals 去判断是不是同一个租户。
/// </remarks>
public class TenantConfigurationTests
{
    /// <summary>
    /// 无参构造默认把租户置为激活态
    /// </summary>
    /// <remarks>
    /// 配置绑定走的就是无参构造，默认值一旦翻转成 false，配置里没写 IsActive 的租户会被整体判定为停用。
    /// </remarks>
    [Fact]
    public void Constructor_Parameterless_ActivatesTenantByDefault()
    {
        var configuration = new TenantConfiguration();

        Assert.True(configuration.IsActive);
        Assert.Equal(0L, configuration.Id);
        Assert.Null(configuration.ConnectionStrings);
        Assert.Null(configuration.EditionId);
    }

    /// <summary>
    /// 唯一标识与名称构造会初始化出空的连接字符串集合
    /// </summary>
    /// <remarks>
    /// 无参构造留 null、带名称构造给空集合，这个差异是真实存在的，调用方对 ConnectionStrings 判空的写法依赖它。
    /// </remarks>
    [Fact]
    public void Constructor_WithIdAndName_InitializesEmptyConnectionStrings()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户");

        Assert.Equal(7L, configuration.Id);
        Assert.Equal("曦寒租户", configuration.Name);
        Assert.True(configuration.IsActive);
        Assert.NotNull(configuration.ConnectionStrings);
        Assert.Empty(configuration.ConnectionStrings);
    }

    /// <summary>
    /// 唯一标识与名称构造不会顺带推导规范化名称
    /// </summary>
    /// <remarks>
    /// 规范化名称的兜底推导发生在租户存储的克隆环节，不在构造函数里；
    /// 这条断言把职责边界钉住，避免两处各推一份导致口径分裂。
    /// </remarks>
    [Fact]
    public void Constructor_WithIdAndName_LeavesNormalizedNameUnset()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户");

        Assert.Null(configuration.NormalizedName);
    }

    /// <summary>
    /// 完整构造把身份字段一次性填齐
    /// </summary>
    [Fact]
    public void Constructor_WithNormalizedName_SetsAllIdentityFields()
    {
        var editionId = Guid.NewGuid();

        var configuration = new TenantConfiguration(7L, "曦寒租户", "XIHAN", editionId);

        Assert.Equal(7L, configuration.Id);
        Assert.Equal("曦寒租户", configuration.Name);
        Assert.Equal("XIHAN", configuration.NormalizedName);
        Assert.Equal<Guid?>(editionId, configuration.EditionId);
        Assert.True(configuration.IsActive);
        Assert.NotNull(configuration.ConnectionStrings);
    }

    /// <summary>
    /// 完整构造的版本唯一标识可省略，省略时为 null
    /// </summary>
    [Fact]
    public void Constructor_WithoutEditionId_LeavesEditionIdNull()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户", "XIHAN");

        Assert.Null(configuration.EditionId);
    }

    /// <summary>
    /// 名称为 null 时构造直接抛参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenNameNull_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new TenantConfiguration(7L, null!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 规范化名称为 null 时构造直接抛参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenNormalizedNameNull_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new TenantConfiguration(7L, "曦寒租户", null!));

        Assert.Equal("normalizedName", exception.ParamName);
    }

    /// <summary>
    /// 保留公共无参构造函数，配置绑定依赖它
    /// </summary>
    /// <remarks>
    /// 租户列表是从 IConfiguration 绑定出来的，绑定器要求目标类型能被无参实例化；
    /// 只留带参构造会让整节配置静默绑不上，属于必须钉死的形状。
    /// </remarks>
    [Fact]
    public void Type_HasPublicParameterlessConstructor()
    {
        Assert.NotNull(typeof(TenantConfiguration).GetConstructor(Type.EmptyTypes));
    }

    /// <summary>
    /// JSON 往返后身份字段与连接字符串保持一致
    /// </summary>
    /// <remarks>
    /// 租户配置会被缓存成 JSON 再取回，往返里最容易丢的是连接字符串集合——
    /// 它是 Dictionary 的派生类型，一旦被当作普通对象序列化，键值会整体丢失。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_PreservesIdentityAndConnectionStrings()
    {
        var editionId = Guid.NewGuid();
        var original = new TenantConfiguration(7L, "曦寒租户", "XIHAN", editionId)
        {
            IsActive = false
        };
        original.ConnectionStrings!.Default = "Server=.;Database=Xihan";

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TenantConfiguration>(json);

        Assert.NotNull(restored);
        Assert.Equal(7L, restored.Id);
        Assert.Equal("曦寒租户", restored.Name);
        Assert.Equal("XIHAN", restored.NormalizedName);
        Assert.Equal<Guid?>(editionId, restored.EditionId);
        Assert.False(restored.IsActive);
        Assert.NotNull(restored.ConnectionStrings);
        Assert.Equal("Server=.;Database=Xihan", restored.ConnectionStrings.Default);
    }

    /// <summary>
    /// 身份与状态字段全部可写，满足配置绑定需要
    /// </summary>
    [Fact]
    public void Properties_AreWritableForConfigurationBinding()
    {
        var editionId = Guid.NewGuid();
        var configuration = new TenantConfiguration
        {
            Id = 3L,
            Name = "曦寒租户",
            NormalizedName = "XIHAN",
            IsActive = false,
            EditionId = editionId,
            ConnectionStrings = new ConnectionStrings { Default = "Server=.;Database=Xihan" }
        };

        Assert.Equal(3L, configuration.Id);
        Assert.Equal("曦寒租户", configuration.Name);
        Assert.Equal("XIHAN", configuration.NormalizedName);
        Assert.False(configuration.IsActive);
        Assert.Equal<Guid?>(editionId, configuration.EditionId);
        Assert.Equal("Server=.;Database=Xihan", configuration.ConnectionStrings!.Default);
    }

    /// <summary>
    /// 未重写相等性，值相同的两个实例互不相等
    /// </summary>
    /// <remarks>
    /// 租户存储每次查询返回的都是克隆体，调用方若用 Equals 判断「是不是同一个租户」必然踩空，
    /// 正确做法是比较 <see cref="TenantConfiguration.Id"/>。这条断言把当前的引用语义显式记录下来。
    /// </remarks>
    [Fact]
    public void Equals_TwoInstancesWithSameValues_AreNotEqual()
    {
        var left = new TenantConfiguration(7L, "曦寒租户", "XIHAN");
        var right = new TenantConfiguration(7L, "曦寒租户", "XIHAN");

        Assert.NotSame(left, right);
        Assert.False(left.Equals(right));
        Assert.Equal(left.Id, right.Id);
    }

    /// <summary>
    /// 默认连接字符串读写的是约定键名
    /// </summary>
    [Fact]
    public void ConnectionStrings_Default_ReadsAndWritesConventionalKey()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户");

        configuration.ConnectionStrings!.Default = "Server=.;Database=Xihan";

        Assert.Equal("Server=.;Database=Xihan", configuration.ConnectionStrings.Default);
        Assert.Equal("Server=.;Database=Xihan", configuration.ConnectionStrings[ConnectionStrings.DefaultConnectionStringName]);
        Assert.Equal("Default", ConnectionStrings.DefaultConnectionStringName);
    }

    /// <summary>
    /// 未配置默认连接字符串时取值为 null
    /// </summary>
    [Fact]
    public void ConnectionStrings_Default_WhenMissing_IsNull()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户");

        Assert.Null(configuration.ConnectionStrings!.Default);
    }

    /// <summary>
    /// 默认连接字符串写入 null 会落成空串而不是移除键
    /// </summary>
    /// <remarks>
    /// 这是取值语义上最容易踩的一处：写 null 之后再读回来拿到的是空串，判空必须用 IsNullOrEmpty 而不是 is null。
    /// </remarks>
    [Fact]
    public void ConnectionStrings_DefaultSetToNull_StoresEmptyString()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户");

        configuration.ConnectionStrings!.Default = null;

        Assert.Equal(string.Empty, configuration.ConnectionStrings.Default);
        Assert.True(configuration.ConnectionStrings.ContainsKey(ConnectionStrings.DefaultConnectionStringName));
    }

    /// <summary>
    /// 除默认库外还能承载多个具名连接字符串
    /// </summary>
    [Fact]
    public void ConnectionStrings_SupportsNamedEntriesBesideDefault()
    {
        var configuration = new TenantConfiguration(7L, "曦寒租户")
        {
            ConnectionStrings = new ConnectionStrings
            {
                Default = "Server=.;Database=Xihan",
                ["Report"] = "Server=.;Database=XihanReport"
            }
        };

        Assert.Equal(2, configuration.ConnectionStrings!.Count);
        Assert.Equal("Server=.;Database=XihanReport", configuration.ConnectionStrings["Report"]);
        Assert.Equal("Server=.;Database=Xihan", configuration.ConnectionStrings.Default);
    }
}
