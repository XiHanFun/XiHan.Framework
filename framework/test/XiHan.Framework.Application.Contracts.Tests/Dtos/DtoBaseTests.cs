// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// DTO 基类测试
/// </summary>
/// <remarks>
/// DtoBase 系列是所有应用层出参的根，本组用例锁定三件事：
/// 主键默认值是类型默认值（不是 null 保护后的替身）、相等性是引用语义（不是值语义）、
/// 以及 BasicId 可被子类重写（多处业务 DTO 依赖重写来改名或加校验）。
/// </remarks>
public class DtoBaseTests
{
    /// <summary>
    /// 值类型主键的默认值是 0，而非抛错或哨兵值
    /// </summary>
    [Fact]
    public void BasicId_WithValueTypeKey_DefaultsToZero()
    {
        var dto = new DtoBaseLongKeyDto();

        Assert.Equal(0L, dto.BasicId);
    }

    /// <summary>
    /// 引用类型主键的默认值是 null
    /// </summary>
    /// <remarks>
    /// 源码用 <c>default!</c> 抑制了可空警告，运行期仍是 null；调用方不能指望拿到空串。
    /// </remarks>
    [Fact]
    public void BasicId_WithReferenceTypeKey_DefaultsToNull()
    {
        var dto = new DtoBaseStringKeyDto();

        Assert.Null(dto.BasicId);
    }

    /// <summary>
    /// 主键可正常写入与回读
    /// </summary>
    [Fact]
    public void BasicId_WhenAssigned_RoundTrips()
    {
        var dto = new DtoBaseLongKeyDto { BasicId = 9527L };

        Assert.Equal(9527L, dto.BasicId);
    }

    /// <summary>
    /// 主键相同的两个实例并不相等：DtoBase 是普通类，走引用相等
    /// </summary>
    /// <remarks>
    /// 特意锁死这一点：若将来改成 record 会静默变成值相等，
    /// 依赖字典/HashSet 去重的调用方行为会翻转。
    /// </remarks>
    [Fact]
    public void Equality_WithSameKey_IsReferenceBased()
    {
        var left = new DtoBaseLongKeyDto { BasicId = 1L };
        var right = new DtoBaseLongKeyDto { BasicId = 1L };

        Assert.NotSame(left, right);
        Assert.False(left.Equals(right));
        Assert.NotEqual(left, right);

        // 引用相等的直接后果：主键相同的两条也不会被集合去重
        var set = new HashSet<DtoBaseLongKeyDto> { left, right };

        Assert.Equal(2, set.Count);
    }

    /// <summary>
    /// BasicId 是 virtual，子类可以重写
    /// </summary>
    [Fact]
    public void BasicId_IsVirtual_AndOverridable()
    {
        var property = typeof(DtoBase<long>).GetProperty("BasicId");

        Assert.NotNull(property);
        Assert.True(property!.GetGetMethod()!.IsVirtual);
        Assert.True(property.GetSetMethod()!.IsVirtual);
        Assert.Equal(42L, new DtoBaseOverriddenKeyDto().BasicId);
    }

    /// <summary>
    /// 泛型基类继承自非泛型基类，两者都是抽象类
    /// </summary>
    [Fact]
    public void GenericBase_InheritsNonGenericBase()
    {
        Assert.True(typeof(DtoBase).IsAbstract);
        Assert.True(typeof(DtoBase<long>).IsAbstract);
        Assert.True(typeof(DtoBase).IsAssignableFrom(typeof(DtoBase<long>)));
        Assert.True(typeof(DtoBase<long>).IsAssignableFrom(typeof(DtoBaseLongKeyDto)));
    }

    /// <summary>
    /// 非泛型基类不引入任何字段，子类的序列化结果不会被基类污染
    /// </summary>
    [Fact]
    public void NonGenericBase_DeclaresNoProperties()
    {
        Assert.Empty(typeof(DtoBase).GetProperties());
    }

    /// <summary>
    /// 序列化后主键字段名为 BasicId
    /// </summary>
    [Fact]
    public void Serialize_UsesBasicIdFieldName()
    {
        var json = JsonSerializer.Serialize(new DtoBaseLongKeyDto { BasicId = 3L, Name = "曦寒" });

        Assert.Contains("\"BasicId\":3", json);

        var restored = JsonSerializer.Deserialize<DtoBaseLongKeyDto>(json);

        Assert.NotNull(restored);
        Assert.Equal(3L, restored!.BasicId);
        Assert.Equal("曦寒", restored.Name);
    }
}

/// <summary>
/// long 主键的具体 DTO
/// </summary>
internal sealed class DtoBaseLongKeyDto : DtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// string 主键的具体 DTO
/// </summary>
internal sealed class DtoBaseStringKeyDto : DtoBase<string>
{
}

/// <summary>
/// 重写了主键的具体 DTO
/// </summary>
internal sealed class DtoBaseOverriddenKeyDto : DtoBase<long>
{
    /// <summary>
    /// 主键（重写后自带初值）
    /// </summary>
    public override long BasicId { get; set; } = 42L;
}
