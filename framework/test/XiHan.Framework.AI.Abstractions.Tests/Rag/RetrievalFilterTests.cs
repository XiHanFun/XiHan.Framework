// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Abstractions.Tests.Rag;

/// <summary>
/// 检索过滤条件测试
/// </summary>
/// <remarks>
/// 这个类型是多租户数据隔离的执行面：TenantId 的 null 与 0 是两种完全不同的语义
/// （null=不限租户、0=平台全局），把它们混淆会导致跨租户召回，属安全事故而非功能瑕疵。
/// </remarks>
public class RetrievalFilterTests
{
    /// <summary>
    /// 新实例不施加任何限定
    /// </summary>
    [Fact]
    public void Defaults_WhenNewInstance_LimitNothing()
    {
        var filter = new RetrievalFilter();

        Assert.Null(filter.TenantId);
        Assert.Null(filter.DocumentId);
    }

    /// <summary>
    /// 租户 0 表示平台全局，与不限租户的 null 严格区分
    /// </summary>
    /// <remarks>
    /// 若实现里用 “TenantId 为假值即不过滤” 的写法，租户 0 会退化成不过滤，
    /// 平台全局检索将读到全部租户数据。此处断言 0 是一个已赋值的限定条件。
    /// </remarks>
    [Fact]
    public void TenantId_WhenZero_IsAnExplicitScopeNotAbsentValue()
    {
        var platformScoped = new RetrievalFilter { TenantId = 0 };
        var unscoped = new RetrievalFilter();

        Assert.True(platformScoped.TenantId.HasValue);
        Assert.Equal(0L, platformScoped.TenantId!.Value);
        Assert.False(unscoped.TenantId.HasValue);
        Assert.NotEqual(unscoped.TenantId, platformScoped.TenantId);
    }

    /// <summary>
    /// 租户与文档两个限定条件互相独立
    /// </summary>
    /// <param name="tenantId">租户限定</param>
    /// <param name="documentId">文档限定</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, null)]
    [InlineData(1024L, null)]
    [InlineData(null, "doc-1")]
    [InlineData(1024L, "doc-1")]
    public void Initializer_WithAnyCombination_KeepsBothConditionsIndependent(long? tenantId, string? documentId)
    {
        var filter = new RetrievalFilter
        {
            TenantId = tenantId,
            DocumentId = documentId
        };

        Assert.Equal(tenantId, filter.TenantId);
        Assert.Equal(documentId, filter.DocumentId);
    }

    /// <summary>
    /// 属性为 init-only，过滤条件构造后不可被中途放宽
    /// </summary>
    /// <remarks>隔离条件若能在传递途中被改写，就失去了作为安全边界的意义。</remarks>
    [Theory]
    [InlineData(nameof(RetrievalFilter.TenantId))]
    [InlineData(nameof(RetrievalFilter.DocumentId))]
    public void Properties_AreInitOnly(string propertyName)
    {
        var property = typeof(RetrievalFilter).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;
        var setter = property.SetMethod!;

        var isInitOnly = setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.Name == "IsExternalInit");

        Assert.True(isInitOnly);
    }

    /// <summary>
    /// 经 System.Text.Json 往返后租户 0 仍是 0，不退化为 null
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    /// <remarks>过滤条件会随检索请求跨进程传递，序列化环节吞掉 0 与吞掉隔离条件等价。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithPlatformScope_KeepsZeroTenant(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new RetrievalFilter
        {
            TenantId = 0,
            DocumentId = "doc-9"
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<RetrievalFilter>(json, serializerOptions)!;

        Assert.True(restored.TenantId.HasValue);
        Assert.Equal(0L, restored.TenantId!.Value);
        Assert.Equal("doc-9", restored.DocumentId);
    }

    /// <summary>
    /// 经 System.Text.Json 往返后未限定的条件仍为 null
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithoutConditions_KeepsBothNull()
    {
        var restored = JsonSerializer.Deserialize<RetrievalFilter>(JsonSerializer.Serialize(new RetrievalFilter()))!;

        Assert.Null(restored.TenantId);
        Assert.Null(restored.DocumentId);
    }

    /// <summary>
    /// 类型为 sealed，隔离条件不允许被派生类扩展出旁路
    /// </summary>
    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(RetrievalFilter).IsSealed);
    }
}
