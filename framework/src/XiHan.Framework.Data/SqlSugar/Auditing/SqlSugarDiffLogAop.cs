#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDiffLogAop
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
using XiHan.Framework.Auditing;
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
///   <item>不会自我递归：审计写入器走裸 <c>Insertable</c>、不挂 <c>EnableDiffLogEvent</c>，SqlSugar 便不会为它再触发 Diff 事件；
///         审计表自身也在 <see cref="IEntityAuditContextProvider.ShouldAudit"/> 的排除名单里，双重保险。</item>
///   <item>审计写入<b>与业务同事务</b>（写入器用当前 UoW 连接）：业务回滚时审计行随之回滚。
///         对「数据变更日志」而言这是正确语义——变更没落库，就不该留下"改过"的记录。</item>
/// </list>
/// 仓储层与审计彻底解耦：仓储只"开启"审计开关，序列化、落库在此完成。
/// </remarks>
internal static class SqlSugarDiffLogAop
{
    // 快照 JSON 的整体上限；超出则产出合法的截断标记（绝不中途切断）
    private const int MaxSerializedLength = 8000;

    // 单列值上限，先行裁剪，避免单个大文本列顶爆整条快照
    private const int MaxColumnValueLength = 1000;

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
        // 审计落库应使用 CopyNew 连接，由 IEntityDiffLogWriter 实现方保证
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
    /// <remarks>
    /// 更新操作会进一步检测软删除标记翻转：IsDeleted false→true 视为删除（软删除）、true→false 视为恢复，
    /// 使数据变更日志能区分 新增 / 修改 / 删除 / 恢复 四种业务语义。
    /// </remarks>
    private static (string Operation, List<DiffLogTableInfo>? Before, List<DiffLogTableInfo>? After) ExtractChange(DiffLogModel diffModel)
    {
        return diffModel.DiffType switch
        {
            DiffType.insert => ("Create", null, diffModel.AfterData),
            DiffType.update => (ResolveUpdateOperation(diffModel), diffModel.BeforeData, diffModel.AfterData),
            DiffType.delete => ("Delete", diffModel.BeforeData, null),
            _ => (diffModel.DiffType.ToString(), diffModel.BeforeData, diffModel.AfterData)
        };
    }

    /// <summary>
    /// 解析更新操作语义：软删除标记翻转映射为 Delete / Restore，常规更新保持 Update
    /// </summary>
    private static string ResolveUpdateOperation(DiffLogModel diffModel)
    {
        var (before, after) = ExtractSoftDeleteFlags(diffModel);
        return (before, after) switch
        {
            (false, true) => "Delete",
            (true, false) => "Restore",
            _ => "Update"
        };
    }

    /// <summary>
    /// 提取前后镜像中的软删除标记（IsDeleted 列，不区分大小写；列缺失时视为未翻转）
    /// </summary>
    private static (bool? Before, bool? After) ExtractSoftDeleteFlags(DiffLogModel diffModel)
    {
        return (ReadSoftDeleteFlag(diffModel.BeforeData), ReadSoftDeleteFlag(diffModel.AfterData));
    }

    private static bool? ReadSoftDeleteFlag(List<DiffLogTableInfo>? tables)
    {
        // DiffLog 里的 ColumnName 是**数据库列名**（Is_Deleted），不是实体属性名（IsDeleted）。
        // 直接比 "IsDeleted" 永远匹配不上（下划线），会让软删/恢复全部退化成 Update——必须归一化后比对。
        var column = tables?.FirstOrDefault()?.Columns?
            .FirstOrDefault(c => IsSoftDeleteColumn(c.ColumnName));
        if (column?.Value is null)
        {
            return null;
        }

        return column.Value switch
        {
            bool flag => flag,
            _ => bool.TryParse(column.Value.ToString(), out var parsed) ? parsed : null
        };
    }

    /// <summary>
    /// 序列化 DiffLog 表行为 JSON（敏感列掩码 + 超长处理，且保证产出的始终是<b>合法 JSON</b>）
    /// </summary>
    private static string? SerializeTables(List<DiffLogTableInfo>? tables)
    {
        if (tables is null || tables.Count == 0)
        {
            return null;
        }

        try
        {
            // 仅保留列名/值，丢弃表元信息以减小体积；敏感列（密码/密钥/令牌/连接串等）按列名整体掩码，绝不明文进审计。
            // 单列超长值先行裁剪，避免一个大文本列把整条快照顶爆。
            var simplified = tables.Select(t => new
            {
                t.TableName,
                Columns = t.Columns?.Select(c => new
                {
                    c.ColumnName,
                    Value = TruncateValue(LogSanitizer.MaskFieldValue(c.ColumnName, c.Value))
                })
            });

            var json = JsonSerializer.Serialize(simplified, JsonOptions);
            if (json.Length <= MaxSerializedLength)
            {
                return json;
            }

            // 裁剪后仍超长：绝不从中间切断（那会产出非法 JSON、前端解析直接炸），改为产出一个合法的截断标记
            return JsonSerializer.Serialize(
                new { Truncated = true, OriginalLength = json.Length, Tables = tables.Select(t => t.TableName) },
                JsonOptions);
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"序列化审计 Diff 表失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 裁剪超长的单列值（仅对字符串生效）
    /// </summary>
    private static object? TruncateValue(object? value)
    {
        return value is string text && text.Length > MaxColumnValueLength
            ? text[..MaxColumnValueLength] + "…"
            : value;
    }

    /// <summary>
    /// 归一化后判断是否软删除列（去分隔符转小写后等于 isdeleted）
    /// </summary>
    private static bool IsSoftDeleteColumn(string? columnName)
    {
        return !string.IsNullOrWhiteSpace(columnName)
            && string.Concat(columnName.Where(char.IsLetterOrDigit))
                .Equals("isdeleted", StringComparison.OrdinalIgnoreCase);
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
            // 变更判定用原值（若先掩码，敏感字段前后都成 ***，会被误判为未变更、改密码将不留痕迹）
            if (Equals(beforeValue, afterCol.Value))
            {
                continue;
            }

            // 敏感字段：保留"这个字段变过"的事实，但不留新旧值
            changed.Add(new
            {
                Field = afterCol.ColumnName,
                Before = LogSanitizer.MaskFieldValue(afterCol.ColumnName, beforeValue),
                After = LogSanitizer.MaskFieldValue(afterCol.ColumnName, afterCol.Value)
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
