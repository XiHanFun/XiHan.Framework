// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 更新 DTO 基类测试
/// </summary>
/// <remarks>
/// 更新入参必须自带主键（与创建入参相反），否则服务端无从定位被更新对象。
/// 这里锁定主键成员的存在性、默认值与序列化字段名。
/// </remarks>
public class UpdateDtoBaseTests
{
    /// <summary>
    /// 两个基类都是抽象类
    /// </summary>
    [Fact]
    public void Bases_AreAbstract()
    {
        Assert.True(typeof(UpdateDtoBase).IsAbstract);
        Assert.True(typeof(UpdateDtoBase<long>).IsAbstract);
    }

    /// <summary>
    /// 泛型基类继承自非泛型基类
    /// </summary>
    [Fact]
    public void GenericBase_InheritsNonGenericBase()
    {
        Assert.True(typeof(UpdateDtoBase).IsAssignableFrom(typeof(UpdateDtoBase<long>)));
        Assert.True(typeof(UpdateDtoBase<long>).IsAssignableFrom(typeof(UpdateDtoBaseTestDto)));
    }

    /// <summary>
    /// 非泛型基类不带主键，主键只在泛型基类上
    /// </summary>
    [Fact]
    public void KeyProperty_OnlyExistsOnGenericBase()
    {
        Assert.Empty(typeof(UpdateDtoBase).GetProperties());
        Assert.NotNull(typeof(UpdateDtoBase<long>).GetProperty("BasicId"));
    }

    /// <summary>
    /// 主键默认值为类型默认值
    /// </summary>
    [Fact]
    public void BasicId_Default_IsTypeDefault()
    {
        Assert.Equal(0L, new UpdateDtoBaseTestDto().BasicId);
        Assert.Null(new UpdateDtoBaseStringKeyDto().BasicId);
    }

    /// <summary>
    /// 主键是 virtual，子类可重写
    /// </summary>
    [Fact]
    public void BasicId_IsVirtual()
    {
        var property = typeof(UpdateDtoBase<long>).GetProperty("BasicId");

        Assert.NotNull(property);
        Assert.True(property!.GetGetMethod()!.IsVirtual);
        Assert.True(property.GetSetMethod()!.IsVirtual);
    }

    /// <summary>
    /// 更新 DTO 序列化后必须带出 BasicId，否则服务端定位不到对象
    /// </summary>
    [Fact]
    public void Serialize_CarriesBasicId()
    {
        var json = JsonSerializer.Serialize(new UpdateDtoBaseTestDto { BasicId = 7L, Name = "改名" });

        Assert.Contains("\"BasicId\":7", json);

        var restored = JsonSerializer.Deserialize<UpdateDtoBaseTestDto>(json);

        Assert.NotNull(restored);
        Assert.Equal(7L, restored!.BasicId);
        Assert.Equal("改名", restored.Name);
    }
}

/// <summary>
/// 具体更新 DTO
/// </summary>
internal sealed class UpdateDtoBaseTestDto : UpdateDtoBase<long>
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 字符串主键的具体更新 DTO
/// </summary>
internal sealed class UpdateDtoBaseStringKeyDto : UpdateDtoBase<string>
{
}
