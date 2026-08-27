// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 删除 DTO 基类测试
/// </summary>
/// <remarks>
/// 删除入参与更新入参同构：只带主键。这里额外锁定「删除基类不带软删除标记」——
/// 是否软删除是服务端策略（见 BatchDeleteRequest.SoftDelete），不由单条删除入参决定。
/// </remarks>
public class DeletionDtoBaseTests
{
    /// <summary>
    /// 两个基类都是抽象类
    /// </summary>
    [Fact]
    public void Bases_AreAbstract()
    {
        Assert.True(typeof(DeletionDtoBase).IsAbstract);
        Assert.True(typeof(DeletionDtoBase<long>).IsAbstract);
    }

    /// <summary>
    /// 泛型基类继承自非泛型基类
    /// </summary>
    [Fact]
    public void GenericBase_InheritsNonGenericBase()
    {
        Assert.True(typeof(DeletionDtoBase).IsAssignableFrom(typeof(DeletionDtoBase<long>)));
        Assert.True(typeof(DeletionDtoBase<long>).IsAssignableFrom(typeof(DeletionDtoBaseTestDto)));
    }

    /// <summary>
    /// 泛型基类只声明主键这一个成员
    /// </summary>
    [Fact]
    public void GenericBase_DeclaresOnlyKeyProperty()
    {
        var names = typeof(DeletionDtoBase<long>).GetProperties().Select(p => p.Name).ToArray();

        Assert.Single(names);
        Assert.Equal("BasicId", names[0]);
        Assert.Empty(typeof(DeletionDtoBase).GetProperties());
    }

    /// <summary>
    /// 主键默认值为类型默认值
    /// </summary>
    [Fact]
    public void BasicId_Default_IsTypeDefault()
    {
        Assert.Equal(0L, new DeletionDtoBaseTestDto().BasicId);
        Assert.Equal(Guid.Empty, new DeletionDtoBaseGuidKeyDto().BasicId);
    }

    /// <summary>
    /// 主键是 virtual，子类可重写
    /// </summary>
    [Fact]
    public void BasicId_IsVirtual()
    {
        var property = typeof(DeletionDtoBase<long>).GetProperty("BasicId");

        Assert.NotNull(property);
        Assert.True(property!.GetGetMethod()!.IsVirtual);
        Assert.True(property.GetSetMethod()!.IsVirtual);
    }

    /// <summary>
    /// Guid 主键的删除 DTO 往返序列化保持主键
    /// </summary>
    [Fact]
    public void Serialize_WithGuidKey_RoundTrips()
    {
        var id = Guid.NewGuid();

        var json = JsonSerializer.Serialize(new DeletionDtoBaseGuidKeyDto { BasicId = id });
        var restored = JsonSerializer.Deserialize<DeletionDtoBaseGuidKeyDto>(json);

        Assert.Contains("\"BasicId\":", json);
        Assert.NotNull(restored);
        Assert.Equal(id, restored!.BasicId);
    }
}

/// <summary>
/// long 主键的具体删除 DTO
/// </summary>
internal sealed class DeletionDtoBaseTestDto : DeletionDtoBase<long>
{
}

/// <summary>
/// Guid 主键的具体删除 DTO
/// </summary>
internal sealed class DeletionDtoBaseGuidKeyDto : DeletionDtoBase<Guid>
{
}
