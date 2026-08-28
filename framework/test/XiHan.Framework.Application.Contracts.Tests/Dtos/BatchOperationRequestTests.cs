// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 批量操作请求测试
/// </summary>
/// <remarks>
/// 三个批量请求的默认值决定了「调用方什么都不填时」的服务端行为：
/// 默认整批事务、遇错即停、删除走软删。这三条默认值是安全侧的选择，
/// 一旦被改成相反值，未显式赋值的历史调用方会静默变成「遇错继续 + 硬删」。
/// </remarks>
public class BatchOperationRequestTests
{
    /// <summary>
    /// 批量操作请求默认：空集合、遇错即停、启用事务
    /// </summary>
    [Fact]
    public void BatchOperationRequest_Defaults_AreTransactionalAndFailFast()
    {
        var request = new BatchOperationRequest<string>();

        Assert.NotNull(request.Items);
        Assert.Empty(request.Items);
        Assert.False(request.ContinueOnError);
        Assert.True(request.UseTransaction);
    }

    /// <summary>
    /// 批量删除请求默认：空集合、遇错即停、启用事务、走软删除
    /// </summary>
    [Fact]
    public void BatchDeleteRequest_Defaults_UseSoftDelete()
    {
        var request = new BatchDeleteRequest<long>();

        Assert.NotNull(request.Ids);
        Assert.Empty(request.Ids);
        Assert.False(request.ContinueOnError);
        Assert.True(request.UseTransaction);
        Assert.True(request.SoftDelete);
    }

    /// <summary>
    /// 批量更新请求默认：空集合、遇错即停、启用事务
    /// </summary>
    [Fact]
    public void BatchUpdateRequest_Defaults_AreTransactionalAndFailFast()
    {
        var request = new BatchUpdateRequest<UpdateDtoBaseTestDto>();

        Assert.NotNull(request.Items);
        Assert.Empty(request.Items);
        Assert.False(request.ContinueOnError);
        Assert.True(request.UseTransaction);
    }

    /// <summary>
    /// 批量更新项默认不带数据
    /// </summary>
    /// <remarks>
    /// <c>Data</c> 用 <c>default!</c> 声明，引用类型场景下运行期就是 null，
    /// 服务端不能默认它非空。
    /// </remarks>
    [Fact]
    public void BatchUpdateItem_Default_DataIsNull()
    {
        var item = new BatchUpdateItem<UpdateDtoBaseTestDto>();

        Assert.Null(item.Data);
    }

    /// <summary>
    /// 批量更新项只承载数据本身，不额外携带主键
    /// </summary>
    /// <remarks>
    /// 主键必须由 TUpdate 自身（UpdateDtoBase&lt;TKey&gt;.BasicId）提供，
    /// 因此批量更新的泛型参数不能是不带主键的创建 DTO。
    /// </remarks>
    [Fact]
    public void BatchUpdateItem_DeclaresOnlyDataProperty()
    {
        var names = typeof(BatchUpdateItem<UpdateDtoBaseTestDto>).GetProperties().Select(p => p.Name).ToArray();

        Assert.Single(names);
        Assert.Equal("Data", names[0]);
    }

    /// <summary>
    /// 集合属性可写，允许调用方整体替换而不是只能逐个 Add
    /// </summary>
    [Fact]
    public void CollectionProperties_AreSettable()
    {
        var request = new BatchOperationRequest<string> { Items = ["a", "b"] };
        var deleteRequest = new BatchDeleteRequest<long> { Ids = [1L, 2L, 3L] };

        Assert.Equal(2, request.Items.Count);
        Assert.Equal(3, deleteRequest.Ids.Count);
    }

    /// <summary>
    /// 批量操作请求的序列化字段名锁定，并往返保值
    /// </summary>
    [Fact]
    public void BatchOperationRequest_RoundTrip_PreservesFieldNames()
    {
        var request = new BatchOperationRequest<string>
        {
            Items = ["a", "b"],
            ContinueOnError = true,
            UseTransaction = false
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"Items\":[\"a\",\"b\"]", json);
        Assert.Contains("\"ContinueOnError\":true", json);
        Assert.Contains("\"UseTransaction\":false", json);

        var restored = JsonSerializer.Deserialize<BatchOperationRequest<string>>(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Items.Count);
        Assert.True(restored.ContinueOnError);
        Assert.False(restored.UseTransaction);
    }

    /// <summary>
    /// 批量删除请求的序列化字段名锁定，并往返保值
    /// </summary>
    [Fact]
    public void BatchDeleteRequest_RoundTrip_PreservesFieldNames()
    {
        var request = new BatchDeleteRequest<long>
        {
            Ids = [10L, 20L],
            SoftDelete = false
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"Ids\":[10,20]", json);
        Assert.Contains("\"SoftDelete\":false", json);

        var restored = JsonSerializer.Deserialize<BatchDeleteRequest<long>>(json);

        Assert.NotNull(restored);
        Assert.Equal([10L, 20L], restored!.Ids);
        Assert.False(restored.SoftDelete);
    }
}
