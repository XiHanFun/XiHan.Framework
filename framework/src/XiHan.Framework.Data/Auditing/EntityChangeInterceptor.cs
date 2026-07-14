#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EntityChangeInterceptor
// Guid:b8e7d6c5-a4b3-42f1-9e0d-c8b7a6f5e4d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XiHan.Framework.Auditing;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Data.Auditing;

/// <summary>
/// 实体变更拦截器
/// </summary>
/// <remarks>
/// 基于 SqlSugar 命令级 AOP 事件（OnLogExecuting / OnLogExecuted）自动捕获实体变更快照并生成差异日志。
/// 与 <see cref="SqlSugar.Auditing.SqlSugarDiffLogAop"/>（需要仓储主动调用 EnableDiffLogEvent）互补，
/// 本拦截器无需仓储显式介入，对所有 INSERT / UPDATE / DELETE 命令自动生效。
/// <list type="bullet">
///   <item><c>OnDataExecuting</c>：在执行前解析 SQL，对 UPDATE / DELETE 提取实体标识与旧值快照；</item>
///   <item><c>OnDataExecuted</c>：执行后组装 <see cref="EntityDiffLogRecord"/> 并 fire-and-forget 写入；</item>
///   <item>通过 <see cref="SqlSugar.Extensions.SqlSugarAuditHookExtensions.UseEntityChangeInterceptor"/> 挂载。</item>
/// </list>
/// </remarks>
public sealed class EntityChangeInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;

    // 以 SQL 参数指纹为键暂存执行前捕获的"前值" JSON
    private static readonly ConcurrentDictionary<string, BeforeSnapshot> BeforeStore = new();

    // JSON 序列化选项：紧凑输出、忽略循环引用
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    // 过期快照清理阈值（避免内存泄漏）
    private const int MaxStoreSize = 10_000;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    public EntityChangeInterceptor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 数据执行前回调：捕获"变更前"快照
    /// </summary>
    /// <param name="sql">执行的 SQL 文本</param>
    /// <param name="pars">SQL 参数集合</param>
    public void OnDataExecuting(object sql, SugarParameter[] pars)
    {
        if (sql is not string sqlText || string.IsNullOrWhiteSpace(sqlText))
        {
            return;
        }

        var operationType = ResolveOperationType(sqlText);
        if (operationType is null)
        {
            return;
        }

        // 仅 UPDATE / DELETE 需要捕获"前值"（INSERT 无前值）
        if (operationType != "Update" && operationType != "Delete")
        {
            return;
        }

        var tableName = ExtractTableName(sqlText, operationType);
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contextProvider = scope.ServiceProvider.GetService<IEntityAuditContextProvider>();
            if (contextProvider is null || !contextProvider.ShouldAuditByName(tableName))
            {
                return;
            }

            // 从参数中提取实体主键值，用于查询"前值"
            var entityId = ExtractEntityIdFromParameters(pars);
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return;
            }

            // 查询数据库获取当前（变更前）状态
            var beforeDataJson = QueryBeforeState(scope.ServiceProvider, tableName, entityId);
            if (beforeDataJson is null)
            {
                return;
            }

            // 以 SQL 指纹为键暂存快照
            var fingerprint = BuildFingerprint(sqlText, pars);
            BeforeStore[fingerprint] = new BeforeSnapshot
            {
                OperationType = operationType,
                TableName = tableName,
                EntityId = entityId,
                BeforeData = beforeDataJson,
                Timestamp = DateTimeOffset.UtcNow
            };

            TrimStoreIfNeeded();
        }
        catch (Exception ex)
        {
            // 审计快照失败不影响主业务
            LogHelper.Warn($"实体变更拦截器捕获前值快照失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 数据执行后回调：计算差异并生成审计日志
    /// </summary>
    /// <param name="sql">执行的 SQL 文本</param>
    /// <param name="pars">SQL 参数集合</param>
    public void OnDataExecuted(object sql, SugarParameter[] pars)
    {
        if (sql is not string sqlText || string.IsNullOrWhiteSpace(sqlText))
        {
            return;
        }

        var operationType = ResolveOperationType(sqlText);
        if (operationType is null)
        {
            return;
        }

        var tableName = ExtractTableName(sqlText, operationType);
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        var fingerprint = BuildFingerprint(sqlText, pars);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contextProvider = scope.ServiceProvider.GetService<IEntityAuditContextProvider>();
            var writer = scope.ServiceProvider.GetService<IEntityDiffLogWriter>();
            if (contextProvider is null || writer is null)
            {
                return;
            }

            if (!contextProvider.ShouldAuditByName(tableName))
            {
                BeforeStore.TryRemove(fingerprint, out _);
                return;
            }

            var record = contextProvider.CreateBaseRecord();
            record.OperationType = operationType;
            record.EntityType = tableName;

            string? beforeData = null;
            string? afterData = null;
            string? changedFields = null;
            string? entityId = null;

            if (operationType == "Insert")
            {
                entityId = ExtractEntityIdFromParameters(pars);
                afterData = SerializeParameters(pars);
            }
            else if (operationType == "Update")
            {
                if (BeforeStore.TryRemove(fingerprint, out var snapshot))
                {
                    beforeData = snapshot.BeforeData;
                    entityId = snapshot.EntityId;

                    // 变更后查询数据库获取"后值"
                    afterData = QueryAfterState(scope.ServiceProvider, tableName, entityId);
                    changedFields = ComputeChangedFields(beforeData, afterData);

                    if (string.IsNullOrWhiteSpace(changedFields))
                    {
                        // 无实际字段变更，不落审计
                        return;
                    }
                }
                else
                {
                    // 前值快照丢失，仅记录后值与 SET 字段
                    entityId = ExtractEntityIdFromParameters(pars);
                    afterData = SerializeSetClauseParameters(sqlText, pars);
                    changedFields = ExtractChangedFieldNames(sqlText);
                }
            }
            else if (operationType == "Delete")
            {
                if (BeforeStore.TryRemove(fingerprint, out var snapshot))
                {
                    beforeData = snapshot.BeforeData;
                    entityId = snapshot.EntityId;
                }
                else
                {
                    entityId = ExtractEntityIdFromParameters(pars);
                }
            }

            record.EntityId = entityId;
            // 字段级脱敏只在"落库前"这一步做：前面的快照/差异比对必须拿原值，否则敏感字段前后都成掩码、
            // 会被判定为"未变更"而丢失记录（改密码将不留任何痕迹）。此处掩码后，值不再明文进审计表。
            record.BeforeData = LogSanitizer.MaskJsonFields(beforeData);
            record.AfterData = LogSanitizer.MaskJsonFields(afterData);
            record.ChangedFields = changedFields;

            // Fire-and-forget：审计写入不阻塞主业务流程
            _ = writer.WriteAsync(record, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 审计失败绝不影响主业务
            LogHelper.Error(ex, $"实体变更拦截器写入差异日志失败：表={tableName}，操作={operationType}");
            BeforeStore.TryRemove(fingerprint, out _);
        }
    }

    #region 私有辅助方法

    /// <summary>
    /// 解析 SQL 操作类型
    /// </summary>
    private static string? ResolveOperationType(string sqlText)
    {
        var trimmed = sqlText.AsSpan().TrimStart();
        if (trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return "Create";
        }

        if (trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return "Update";
        }

        if (trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return "Delete";
        }

        return null;
    }

    /// <summary>
    /// 从 SQL 文本提取表名
    /// </summary>
    private static string? ExtractTableName(string sqlText, string operationType)
    {
        try
        {
            return operationType switch
            {
                "Create" => MatchPattern(sqlText, @"INSERT\s+INTO\s+[\[`""]?(\w+)[\]`""]?", RegexOptions.IgnoreCase),
                "Update" => MatchPattern(sqlText, @"UPDATE\s+[\[`""]?(\w+)[\]`""]?", RegexOptions.IgnoreCase),
                "Delete" => MatchPattern(sqlText, @"DELETE\s+FROM\s+[\[`""]?(\w+)[\]`""]?", RegexOptions.IgnoreCase),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 正则匹配提取组
    /// </summary>
    private static string? MatchPattern(string input, string pattern, RegexOptions options)
    {
        var match = Regex.Match(input, pattern, options);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 从参数集合中提取实体主键值（查找名称含 Id 的参数）
    /// </summary>
    private static string? ExtractEntityIdFromParameters(SugarParameter[] pars)
    {
        if (pars is null || pars.Length == 0)
        {
            return null;
        }

        // 优先查找名称以 "Id" 结尾的参数
        foreach (var param in pars)
        {
            var name = param.ParameterName?.TrimStart('@', ':');
            if (name is not null && name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                return param.Value?.ToString();
            }
        }

        // 回退：取第一个参数
        return pars[0].Value?.ToString();
    }

    /// <summary>
    /// 查询变更前数据库中的实体状态
    /// </summary>
    private static string? QueryBeforeState(IServiceProvider serviceProvider, string tableName, string entityId)
    {
        try
        {
            var client = serviceProvider.GetService<ISqlSugarClient>();
            if (client is null)
            {
                return null;
            }

            // 推断主键类型：long 优先，否则按字符串处理（Guid 等场景）
            var idValue = CoerceEntityId(entityId);
            var result = client.Ado.GetDataTable(
                $"SELECT * FROM [{tableName}] WHERE Id = @Id",
                new SugarParameter[] { new SugarParameter("@Id", idValue) });
            if (result?.Rows.Count > 0)
            {
                var row = result.Rows[0];
                var dict = new Dictionary<string, object?>();
                foreach (DataColumn col in result.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                return JsonSerializer.Serialize(dict, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"查询实体前值状态失败（表={tableName}，Id={entityId}）：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 查询变更后数据库中的实体状态
    /// </summary>
    private static string? QueryAfterState(IServiceProvider serviceProvider, string tableName, string? entityId)
    {
        try
        {
            var client = serviceProvider.GetService<ISqlSugarClient>();
            if (client is null || string.IsNullOrWhiteSpace(entityId))
            {
                return null;
            }

            // 推断主键类型：long 优先，否则按字符串处理（Guid 等场景）
            var idValue = CoerceEntityId(entityId);
            var result = client.Ado.GetDataTable(
                $"SELECT * FROM [{tableName}] WHERE Id = @Id",
                new SugarParameter[] { new SugarParameter("@Id", idValue) });
            if (result?.Rows.Count > 0)
            {
                var row = result.Rows[0];
                var dict = new Dictionary<string, object?>();
                foreach (DataColumn col in result.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                return JsonSerializer.Serialize(dict, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"查询实体后值状态失败（表={tableName}，Id={entityId}）：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 将实体 ID 字符串转换为正确的数据库主键类型
    /// </summary>
    /// <remarks>
    /// 尝试转换为 long（项目的雪花 ID 约定）；若失败则保留为 string（Guid 等场景）。
    /// </remarks>
    private static object CoerceEntityId(string entityId)
    {
        if (long.TryParse(entityId, out var longId))
        {
            return longId;
        }

        return entityId;
    }

    /// <summary>
    /// 将参数序列化为 JSON
    /// </summary>
    private static string? SerializeParameters(SugarParameter[] pars)
    {
        if (pars is null || pars.Length == 0)
        {
            return null;
        }

        try
        {
            var dict = pars.ToDictionary(
                p => p.ParameterName?.TrimStart('@', ':') ?? "unknown",
                p => p.Value);
            return JsonSerializer.Serialize(dict, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从 UPDATE 语句的 SET 子句中提取参数值序列化
    /// </summary>
    private static string? SerializeSetClauseParameters(string sqlText, SugarParameter[] pars)
    {
        if (pars is null || pars.Length == 0)
        {
            return null;
        }

        try
        {
            var setMatch = Regex.Match(sqlText, @"SET\s+(.+?)\s*WHERE", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!setMatch.Success)
            {
                return SerializeParameters(pars);
            }

            var setClause = setMatch.Groups[1].Value;
            var setParams = new Dictionary<string, object?>();

            foreach (var param in pars)
            {
                var paramName = param.ParameterName ?? string.Empty;
                if (setClause.Contains(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    var cleanName = paramName.TrimStart('@', ':');
                    setParams[cleanName] = param.Value;
                }
            }

            return setParams.Count > 0 ? JsonSerializer.Serialize(setParams, JsonOptions) : SerializeParameters(pars);
        }
        catch
        {
            return SerializeParameters(pars);
        }
    }

    /// <summary>
    /// 从 UPDATE 语句提取变更字段名列表
    /// </summary>
    private static string? ExtractChangedFieldNames(string sqlText)
    {
        try
        {
            var setMatch = Regex.Match(sqlText, @"SET\s+(.+?)\s*WHERE", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!setMatch.Success)
            {
                return null;
            }

            var setClause = setMatch.Groups[1].Value;
            var fieldMatches = Regex.Matches(setClause, @"[\[`""]?(\w+)[\]`""]?\s*=");
            var fields = new List<string>();
            foreach (Match m in fieldMatches)
            {
                fields.Add(m.Groups[1].Value);
            }

            if (fields.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(fields.Select(f => new { Field = f }), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 比对前后 JSON 快照，计算实际发生变更的字段
    /// </summary>
    private static string? ComputeChangedFields(string? beforeJson, string? afterJson)
    {
        if (string.IsNullOrWhiteSpace(beforeJson) || string.IsNullOrWhiteSpace(afterJson))
        {
            return null;
        }

        try
        {
            var before = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(beforeJson, JsonOptions);
            var after = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(afterJson, JsonOptions);
            if (before is null || after is null)
            {
                return null;
            }

            var changed = new List<object>();
            foreach (var kvp in after)
            {
                before.TryGetValue(kvp.Key, out var beforeValue);
                var beforeStr = beforeValue.ValueKind == JsonValueKind.Undefined ? null : beforeValue.GetRawText();
                var afterStr = kvp.Value.GetRawText();

                if (beforeStr != afterStr)
                {
                    // 敏感字段：保留"这个字段变过"的事实（审计价值），但不留新旧值
                    var isSensitive = LogSanitizer.IsSensitiveName(kvp.Key);
                    changed.Add(new
                    {
                        Field = kvp.Key,
                        Before = isSensitive ? LogSanitizer.Mask : beforeStr,
                        After = isSensitive ? LogSanitizer.Mask : afterStr
                    });
                }
            }

            return changed.Count > 0 ? JsonSerializer.Serialize(changed, JsonOptions) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 基于 SQL 文本和参数生成指纹（用于关联前后回调）
    /// </summary>
    private static string BuildFingerprint(string sql, SugarParameter[] pars)
    {
        // 使用 SQL 哈希 + 关键参数值生成指纹
        var paramFingerprint = pars is not null && pars.Length > 0
            ? string.Join("|", pars.Select(p => $"{p.ParameterName}={p.Value}"))
            : string.Empty;
        var raw = $"{sql}|{paramFingerprint}";
        return raw.GetHashCode().ToString("X8");
    }

    /// <summary>
    /// 定期清理过期快照，避免内存泄漏
    /// </summary>
    private static void TrimStoreIfNeeded()
    {
        if (BeforeStore.Count <= MaxStoreSize)
        {
            return;
        }

        var threshold = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiredKeys = BeforeStore
            .Where(kvp => kvp.Value.Timestamp < threshold)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            BeforeStore.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 执行前快照内部结构
    /// </summary>
    private sealed class BeforeSnapshot
    {
        public string OperationType { get; init; } = string.Empty;
        public string TableName { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public string BeforeData { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; }
    }

    #endregion
}
