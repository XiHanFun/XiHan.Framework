#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarAuditLogAop
// Guid:c7d2a2f0-6b9d-4f7a-9c83-2a6bb3e8f1b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 13:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Data.Auditing;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Data.SqlSugar.Auditing;

/// <summary>
/// SqlSugar 差异日志 AOP 处理器
/// </summary>
/// <remarks>
/// 基于 SqlSugar 原生 <c>Aop.OnDiffLogEvent</c> 机制：
/// <list type="bullet">
///   <item>仓储写操作显式调用 <c>.EnableDiffLogEvent(businessData)</c> 触发 Diff 收集；</item>
///   <item>SqlSugar 自动生成 <see cref="DiffLogModel"/>（含 BeforeData/AfterData/DiffType/SQL 等）；</item>
///   <item>本处理器负责把 Diff 事件转换为 <see cref="EntityDiffLogRecord"/> 并调用 <see cref="IEntityDiffLogWriter"/> 落库；</item>
///   <item>使用 <c>CopyNew()</c> 独立连接写审计，避免自我递归；审计表自身的变更会被过滤。</item>
/// </list>
/// 仓储层与审计彻底解耦：仓储只"开启"审计开关，序列化、落库在此完成。
/// </remarks>
internal static class SqlSugarDiffLogAop
{
    // 审计表自身写入不再触发 Diff 审计，需按表名/实体名过滤
    // 这里按实体类型名比对，具体业务审计表名由 IEntityAuditContextProvider 实现方控制
    private const string AuditSelfMarker = "AuditSelf";

    // JSON 序列化配置：紧凑、忽略循环引用
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    /// 挂载 OnDiffLogEvent 处理器到指定 SqlSugar 连接
    /// </summary>
    /// <param name="scopeFactory">根服务域工厂（审计写入需独立 Scope 解析 writer/provider）</param>
    /// <param name="dbProvider">SqlSugar 连接提供者</param>
    public static void Attach(IServiceScopeFactory scopeFactory, SqlSugarScopeProvider dbProvider)
    {
        dbProvider.Aop.OnDiffLogEvent = diffModel =>
        {
            try
            {
                HandleDiffLog(scopeFactory, diffModel);
            }
            catch (Exception ex)
            {
                // 审计失败绝不影响主业务，记日志兜底
                LogHelper.Error(ex, "写入实体差异日志失败");
            }
        };
    }

    /// <summary>
    /// 处理 Diff 事件，组装审计记录并落库
    /// </summary>
    private static void HandleDiffLog(IServiceScopeFactory scopeFactory, DiffLogModel diffModel)
    {
        if (diffModel is null)
        {
            return;
        }

        // 业务对象标记为审计自身则跳过，避免递归
        if (diffModel.BusinessData is string marker && marker == AuditSelfMarker)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<IEntityAuditContextProvider>();
        var writer = scope.ServiceProvider.GetService<IEntityDiffLogWriter>();
        if (contextProvider is null || writer is null)
        {
            return;
        }

        var entityTypeName = ResolveEntityTypeName(diffModel);
        if (string.IsNullOrWhiteSpace(entityTypeName))
        {
            return;
        }

        // 由 Provider 决定该实体类型是否参与审计（可按需开启/关闭）
        var businessType = diffModel.BusinessData as Type;
        if (businessType is not null && !contextProvider.ShouldAudit(businessType))
        {
            return;
        }

        var (operation, before, after) = ExtractChange(diffModel);
        var changedFields = diffModel.DiffType == DiffType.update
            ? SerializeChangedFields(diffModel)
            : null;

        if (diffModel.DiffType == DiffType.update && string.IsNullOrWhiteSpace(changedFields))
        {
            // 没有字段变更则无需落审计
            return;
        }

        var record = contextProvider.CreateBaseRecord();
        record.OperationType = operation;
        record.EntityType = entityTypeName;
        record.EntityId = ExtractEntityId(diffModel);
        record.BeforeData = SerializeTables(before);
        record.AfterData = SerializeTables(after);
        record.ChangedFields = changedFields;

        // SqlSugar AOP 回调为同步签名，这里走 .GetAwaiter().GetResult()
        // 审计落库应使用 CopyNew 连接，由 IEntityAuditLogWriter 实现方保证
        writer.WriteAsync(record, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 解析实体类型名：优先用 BusinessData（仓储传入的 Type），否则回落到表名
    /// </summary>
    private static string? ResolveEntityTypeName(DiffLogModel diffModel)
    {
        if (diffModel.BusinessData is Type type)
        {
            return type.Name;
        }

        var tableInfo = diffModel.AfterData?.FirstOrDefault() ?? diffModel.BeforeData?.FirstOrDefault();
        return tableInfo?.TableName;
    }

    /// <summary>
    /// 提取实体主键值（按列级 IsPrimaryKey 匹配首个主键）
    /// </summary>
    private static string? ExtractEntityId(DiffLogModel diffModel)
    {
        var tableInfo = diffModel.AfterData?.FirstOrDefault() ?? diffModel.BeforeData?.FirstOrDefault();
        var primaryKey = tableInfo?.Columns?.FirstOrDefault(c => c.IsPrimaryKey);
        return primaryKey?.Value?.ToString();
    }

    /// <summary>
    /// 将 SqlSugar 的 DiffType 映射为项目内的 OperationType 字符串
    /// </summary>
    private static (string Operation, List<DiffLogTableInfo>? Before, List<DiffLogTableInfo>? After) ExtractChange(DiffLogModel diffModel)
    {
        return diffModel.DiffType switch
        {
            DiffType.insert => ("Create", null, diffModel.AfterData),
            DiffType.update => ("Update", diffModel.BeforeData, diffModel.AfterData),
            DiffType.delete => ("Delete", diffModel.BeforeData, null),
            _ => (diffModel.DiffType.ToString(), diffModel.BeforeData, diffModel.AfterData)
        };
    }

    /// <summary>
    /// 序列化 DiffLog 表行为 JSON（超长截断避免撑爆审计表）
    /// </summary>
    private static string? SerializeTables(List<DiffLogTableInfo>? tables)
    {
        if (tables is null || tables.Count == 0)
        {
            return null;
        }

        try
        {
            // 仅保留列名/值，丢弃表元信息以减小体积
            var simplified = tables.Select(t => new
            {
                t.TableName,
                Columns = t.Columns?.Select(c => new { c.ColumnName, c.Value })
            });
            var json = JsonSerializer.Serialize(simplified, JsonOptions);
            const int maxLength = 8000;
            return json.Length > maxLength ? json[..maxLength] : json;
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"序列化审计 Diff 表失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 基于 Before/After 提取真正发生变更的字段
    /// </summary>
    private static string? SerializeChangedFields(DiffLogModel diffModel)
    {
        var before = diffModel.BeforeData?.FirstOrDefault();
        var after = diffModel.AfterData?.FirstOrDefault();
        if (before?.Columns is null || after?.Columns is null)
        {
            return null;
        }

        // 以列名为 key 对齐前后值
        var beforeMap = before.Columns.ToDictionary(c => c.ColumnName, c => c.Value);
        var changed = new List<object>();
        foreach (var afterCol in after.Columns)
        {
            beforeMap.TryGetValue(afterCol.ColumnName, out var beforeValue);
            if (Equals(beforeValue, afterCol.Value))
            {
                continue;
            }

            changed.Add(new
            {
                Field = afterCol.ColumnName,
                Before = beforeValue,
                After = afterCol.Value
            });
        }

        if (changed.Count == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(changed, JsonOptions);
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"序列化审计字段变更失败：{ex.Message}");
            return null;
        }
    }
}
