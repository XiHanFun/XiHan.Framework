// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 审计 DTO 基类测试
/// </summary>
/// <remarks>
/// 三个时间字段的类型是 <c>DateTimeOffset[]?</c> 而非单个 <c>DateTimeOffset?</c>，
/// 说明该基类同时充当「审计查询入参」——时间以区间数组传递。
/// 这个反直觉的形状是对外契约的一部分，必须锁死，否则前端会按单值传参。
/// </remarks>
public class FullAuditedDtoBaseTests
{
    /// <summary>
    /// 非泛型基类默认值：时间区间全空、未软删除
    /// </summary>
    [Fact]
    public void NonGenericBase_Defaults_AreEmpty()
    {
        var dto = new FullAuditedNonGenericTestDto();

        Assert.Null(dto.CreatedTime);
        Assert.Null(dto.ModifiedTime);
        Assert.Null(dto.DeletedTime);
        Assert.False(dto.IsDeleted);
    }

    /// <summary>
    /// 泛型基类默认值：主键与三个操作人标识都是类型默认值
    /// </summary>
    [Fact]
    public void GenericBase_Defaults_AreTypeDefaults()
    {
        var dto = new FullAuditedGenericTestDto();

        Assert.Null(dto.BasicId);
        Assert.Null(dto.CreatedId);
        Assert.Null(dto.CreatedBy);
        Assert.Null(dto.ModifiedId);
        Assert.Null(dto.ModifiedBy);
        Assert.Null(dto.DeletedId);
        Assert.Null(dto.DeletedBy);
        Assert.False(dto.IsDeleted);
    }

    /// <summary>
    /// 三个时间字段是数组，用于承载查询区间而非单点时刻
    /// </summary>
    [Fact]
    public void TimeFields_AreArrays_NotSingleInstants()
    {
        Assert.Equal(typeof(DateTimeOffset[]), typeof(FullAuditedDtoBase).GetProperty("CreatedTime")!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset[]), typeof(FullAuditedDtoBase).GetProperty("ModifiedTime")!.PropertyType);
        Assert.Equal(typeof(DateTimeOffset[]), typeof(FullAuditedDtoBase).GetProperty("DeletedTime")!.PropertyType);
    }

    /// <summary>
    /// 时间区间写入后原样回读
    /// </summary>
    [Fact]
    public void TimeRange_WhenAssigned_RoundTrips()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

        var dto = new FullAuditedNonGenericTestDto { CreatedTime = [start, end] };

        Assert.NotNull(dto.CreatedTime);
        Assert.Equal(2, dto.CreatedTime!.Length);
        Assert.Equal(start, dto.CreatedTime[0]);
        Assert.Equal(end, dto.CreatedTime[1]);
    }

    /// <summary>
    /// 所有审计成员都是 virtual，允许子类重写以改写默认值或加校验
    /// </summary>
    [Fact]
    public void AuditMembers_AreVirtual()
    {
        string[] memberNames =
        [
            nameof(FullAuditedDtoBase.CreatedTime),
            nameof(FullAuditedDtoBase.ModifiedTime),
            nameof(FullAuditedDtoBase.DeletedTime),
            nameof(FullAuditedDtoBase.IsDeleted)
        ];

        foreach (var memberName in memberNames)
        {
            var property = typeof(FullAuditedDtoBase).GetProperty(memberName);

            Assert.NotNull(property);
            Assert.True(property!.GetGetMethod()!.IsVirtual, memberName);
        }
    }

    /// <summary>
    /// 泛型基类继承自非泛型基类，泛型侧只补操作人相关成员
    /// </summary>
    [Fact]
    public void GenericBase_InheritsNonGenericBase()
    {
        Assert.True(typeof(FullAuditedDtoBase).IsAbstract);
        Assert.True(typeof(FullAuditedDtoBase<string>).IsAbstract);
        Assert.True(typeof(FullAuditedDtoBase).IsAssignableFrom(typeof(FullAuditedDtoBase<string>)));

        var declared = typeof(FullAuditedDtoBase<string>)
            .GetProperties()
            .Where(p => p.DeclaringType == typeof(FullAuditedDtoBase<string>))
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "BasicId", "CreatedBy", "CreatedId", "DeletedBy", "DeletedId", "ModifiedBy", "ModifiedId"
        ];

        Assert.Equal(expected, declared);
    }

    /// <summary>
    /// 审计字段名即对外契约字段名，且往返序列化保值
    /// </summary>
    [Fact]
    public void Serialize_PreservesAuditFieldNamesAndValues()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var dto = new FullAuditedGenericTestDto
        {
            BasicId = "u-1",
            CreatedId = "c-1",
            CreatedBy = "创建者",
            ModifiedId = "m-1",
            ModifiedBy = "修改者",
            DeletedId = "d-1",
            DeletedBy = "删除者",
            IsDeleted = true,
            CreatedTime = [start, end],
            DeletedTime = [end]
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.Contains("\"BasicId\":\"u-1\"", json);
        Assert.Contains("\"IsDeleted\":true", json);
        Assert.Contains("\"ModifiedTime\":null", json);

        var restored = JsonSerializer.Deserialize<FullAuditedGenericTestDto>(json);

        Assert.NotNull(restored);
        Assert.Equal("u-1", restored!.BasicId);
        Assert.Equal("c-1", restored.CreatedId);
        Assert.Equal("创建者", restored.CreatedBy);
        Assert.Equal("m-1", restored.ModifiedId);
        Assert.Equal("修改者", restored.ModifiedBy);
        Assert.Equal("d-1", restored.DeletedId);
        Assert.Equal("删除者", restored.DeletedBy);
        Assert.True(restored.IsDeleted);
        Assert.Null(restored.ModifiedTime);
        Assert.NotNull(restored.CreatedTime);
        Assert.Equal(2, restored.CreatedTime!.Length);
        Assert.Equal(start, restored.CreatedTime[0]);
        Assert.NotNull(restored.DeletedTime);
        Assert.Single(restored.DeletedTime!);
    }
}

/// <summary>
/// 非泛型审计 DTO 的具体实现
/// </summary>
internal sealed class FullAuditedNonGenericTestDto : FullAuditedDtoBase
{
}

/// <summary>
/// 字符串主键的审计 DTO 具体实现
/// </summary>
internal sealed class FullAuditedGenericTestDto : FullAuditedDtoBase<string>
{
}
