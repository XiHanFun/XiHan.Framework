// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 处理 Diff 事件，按行组装审计记录并落库（每个受影响的行一条记录）
    /// </summary>
    /// <remarks>
    /// SqlSugar 对批量写的 BeforeData/AfterData 是<b>每行一个</b> <see cref="DiffLogTableInfo"/>，
    /// 且前后两次快照 SELECT 均无 ORDER BY、行序不保证一致——必须按主键值配对，绝不能按下标或 FirstOrDefault：
    /// 历史实现只看首行，导致「首行无变更 → 整批 N 行审计被静默丢弃」「EntityId/ChangedFields 恒为首行」「行序漂移时跨行误报」。
    /// 每行一条记录使 EntityId 精确、ChangedFields 形状（{Field,Before,After}[]）与单行时代完全一致，消费端无需改动。
    /// </remarks>
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

        foreach (var (entityId, before, after) in PairRowsByPrimaryKey(diffModel.BeforeData, diffModel.AfterData))
        {
            string? changedFields = null;
            if (diffModel.DiffType == DiffType.update)
            {
                changedFields = SerializeChangedFieldsForRow(before, after);

                // 仅跳过「前后镜像都在且无字段变更」的行（不再因首行无变更丢弃整批）；
                // after 缺失（表达式更新使 WHERE 不再命中该行，重查不到后镜像）不跳过——
                // 退化为仅记 before 镜像，保留「改过」的事实
                if (changedFields is null && after is not null)
                {
                    continue;
                }
            }

            var record = contextProvider.CreateBaseRecord();
            record.OperationType = ResolveRowOperation(diffModel.DiffType, before, after);
            record.EntityType = entityTypeName;
            record.EntityId = entityId;
            record.BeforeData = SerializeTables(before is null ? null : [before]);
            record.AfterData = SerializeTables(after is null ? null : [after]);
            record.ChangedFields = changedFields;

            // SqlSugar AOP 回调为同步签名，这里走 .GetAwaiter().GetResult()。
            // 契约：写入器须经 ISqlSugarClientResolver.GetCurrentClient() 使用当前工作单元连接
            // （与业务同事务，业务回滚审计随之回滚，见类型 remarks）；禁止 CopyNew/独立连接，
            // 否则业务回滚后会留下「从未生效的变更」的幽灵审计记录。
            writer.WriteAsync(record, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 按主键值配对前后镜像行
    /// </summary>
    /// <remarks>
    /// update：前后按主键匹配成对；insert 只有后镜像、delete 只有前镜像，各自独立成行。
    /// 无主键列的行无法配对，前后各自独立成行（降级但不丢数据）。
    /// </remarks>
    private static List<(string? EntityId, DiffLogTableInfo? Before, DiffLogTableInfo? After)> PairRowsByPrimaryKey(
        List<DiffLogTableInfo>? beforeRows,
        List<DiffLogTableInfo>? afterRows)
    {
        var result = new List<(string?, DiffLogTableInfo?, DiffLogTableInfo?)>();
        var afterByKey = new Dictionary<string, DiffLogTableInfo>();
        var unkeyedAfterRows = new List<DiffLogTableInfo>();

        foreach (var afterRow in afterRows ?? [])
        {
            var key = ExtractRowKey(afterRow);
            if (key is null)
            {
                unkeyedAfterRows.Add(afterRow);
            }
            else
            {
                afterByKey[key] = afterRow;
            }
        }

        foreach (var beforeRow in beforeRows ?? [])
        {
            var key = ExtractRowKey(beforeRow);
            if (key is not null && afterByKey.Remove(key, out var matchedAfter))
            {
                result.Add((key, beforeRow, matchedAfter));
            }
            else
            {
                result.Add((key, beforeRow, null));
            }
        }

        foreach (var (key, afterRow) in afterByKey)
        {
            result.Add((key, null, afterRow));
        }

        foreach (var afterRow in unkeyedAfterRows)
        {
            result.Add((null, null, afterRow));
        }

        return result;
    }

    /// <summary>
    /// 提取单行的主键值（多主键列以 | 连接）
    /// </summary>
    private static string? ExtractRowKey(DiffLogTableInfo? rowInfo)
    {
        var primaryKeyColumns = rowInfo?.Columns?.Where(c => c.IsPrimaryKey).ToList();
        return primaryKeyColumns is null || primaryKeyColumns.Count == 0
            ? null
            : string.Join("|", primaryKeyColumns.Select(c => c.Value?.ToString()));
    }

    /// <summary>
    /// 按行解析操作语义：更新按该行的软删除标记翻转映射为 Delete/Restore，常规更新保持 Update
    /// </summary>
    private static string ResolveRowOperation(DiffType diffType, DiffLogTableInfo? before, DiffLogTableInfo? after)
    {
        return diffType switch
        {
            DiffType.insert => "Create",
            DiffType.delete => "Delete",
            DiffType.update => (ReadSoftDeleteFlag(before), ReadSoftDeleteFlag(after)) switch
            {
                (false, true) => "Delete",
                (true, false) => "Restore",
                _ => "Update"
            },
            _ => diffType.ToString()
        };
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
    /// 读取单行镜像中的软删除标记（IsDeleted 列，不区分大小写；列缺失时视为未翻转）
    /// </summary>
    private static bool? ReadSoftDeleteFlag(DiffLogTableInfo? rowInfo)
    {
        // DiffLog 里的 ColumnName 是**数据库列名**（Is_Deleted），不是实体属性名（IsDeleted）。
        // 直接比 "IsDeleted" 永远匹配不上（下划线），会让软删/恢复全部退化成 Update——必须归一化后比对。
        var column = rowInfo?.Columns?
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
    /// 基于单行前后镜像提取真正发生变更的字段
    /// </summary>
    private static string? SerializeChangedFieldsForRow(DiffLogTableInfo? before, DiffLogTableInfo? after)
    {
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
