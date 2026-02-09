#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryKeyword
// Guid:a2f29b87-33fc-4fc9-81fe-af4ae70713c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/10 6:42:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 关键字搜索（关键字文本 + 参与搜索的字段）
/// </summary>
public sealed class QueryKeyword
{
    /// <summary>
    /// 搜索关键字（可选）
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 允许关键字搜索的字段
    /// </summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>
    /// 匹配模式（默认包含）
    /// </summary>
    public KeywordMatchMode MatchMode { get; set; } = KeywordMatchMode.Contains;

    /// <summary>
    /// 是否有效（有关键字且有有效的搜索字段）
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value) && 
                          Fields.Exists(f => !string.IsNullOrWhiteSpace(f));

    /// <summary>
    /// 是否为空
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value) || 
                          !Fields.Exists(f => !string.IsNullOrWhiteSpace(f));

    /// <summary>
    /// 添加搜索字段（自动过滤空值）
    /// </summary>
    public QueryKeyword AddField(string field)
    {
        if (!string.IsNullOrWhiteSpace(field) && !Fields.Contains(field, StringComparer.OrdinalIgnoreCase))
        {
            Fields.Add(field.Trim());
        }
        return this;
    }

    /// <summary>
    /// 清理空字段
    /// </summary>
    public QueryKeyword CleanEmptyFields()
    {
        Fields.RemoveAll(string.IsNullOrWhiteSpace);
        return this;
    }

    /// <summary>
    /// 批量添加搜索字段
    /// </summary>
    public QueryKeyword AddFields(IEnumerable<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var field in fields)
        {
            AddField(field);
        }
        return this;
    }

    /// <summary>
    /// 清空搜索字段
    /// </summary>
    public QueryKeyword ClearFields()
    {
        Fields.Clear();
        return this;
    }

    /// <summary>
    /// 克隆当前关键字搜索
    /// </summary>
    public QueryKeyword Clone()
    {
        return new QueryKeyword
        {
            Value = Value,
            Fields = [.. Fields],
            MatchMode = MatchMode
        };
    }
}
