// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 带定长字符串列的实体（第二版：多一列，<c>InitTables</c> 加列）
/// </summary>
[SugarTable("text_record")]
public sealed class TextRecordV2
{
    /// <summary>
    /// 标识
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 名字
    /// </summary>
    [StringLength(10)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(10)]
    public string? Note { get; set; }
}
