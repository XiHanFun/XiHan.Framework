// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 实体差异日志记录模型测试
/// </summary>
/// <remarks>
/// <c>AuditType</c> 的默认值 "EntityChange" 是落库后用于区分审计种类的判别字段，
/// 属被持久化依赖的常量语义，改动会导致历史数据检索不到，必须锁死。
/// </remarks>
public class EntityDiffLogRecordTests
{
    /// <summary>
    /// 新建记录时审计类型固定为 EntityChange，其余字段为空串或 null
    /// </summary>
    [Fact]
    public void Ctor_Default_AuditTypeIsEntityChange()
    {
        var record = new EntityDiffLogRecord();

        Assert.Equal("EntityChange", record.AuditType);
        Assert.Equal(string.Empty, record.OperationType);
        Assert.Equal(string.Empty, record.EntityType);

        Assert.Null(record.EntityId);
        Assert.Null(record.BeforeData);
        Assert.Null(record.AfterData);
        Assert.Null(record.ChangedFields);
        Assert.Null(record.RequestPath);
        Assert.Null(record.RequestMethod);
        Assert.Null(record.OperationIp);
        Assert.Null(record.RequestId);
        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
        Assert.Null(record.TenantId);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesAndPascalCasePropertyNames()
    {
        var original = new EntityDiffLogRecord
        {
            AuditType = "EntityChange",
            OperationType = "Update",
            EntityType = "SampleDomain.Entities.SampleOrder",
            EntityId = "1",
            BeforeData = "{\"Amount\":1}",
            AfterData = "{\"Amount\":2}",
            ChangedFields = "[\"Amount\"]",
            RequestPath = "/api/orders/1",
            RequestMethod = "PUT",
            OperationIp = "10.0.0.6",
            RequestId = "req-1",
            UserId = 5,
            UserName = "tom",
            TenantId = 100
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<EntityDiffLogRecord>(json);

        Assert.Contains("\"AuditType\":", json);
        Assert.Contains("\"ChangedFields\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.AuditType, restored!.AuditType);
        Assert.Equal(original.OperationType, restored.OperationType);
        Assert.Equal(original.EntityType, restored.EntityType);
        Assert.Equal(original.EntityId, restored.EntityId);
        Assert.Equal(original.BeforeData, restored.BeforeData);
        Assert.Equal(original.AfterData, restored.AfterData);
        Assert.Equal(original.ChangedFields, restored.ChangedFields);
        Assert.Equal(original.RequestPath, restored.RequestPath);
        Assert.Equal(original.RequestMethod, restored.RequestMethod);
        Assert.Equal(original.OperationIp, restored.OperationIp);
        Assert.Equal(original.RequestId, restored.RequestId);
        Assert.Equal(original.UserId, restored.UserId);
        Assert.Equal(original.UserName, restored.UserName);
        Assert.Equal(original.TenantId, restored.TenantId);
    }
}
