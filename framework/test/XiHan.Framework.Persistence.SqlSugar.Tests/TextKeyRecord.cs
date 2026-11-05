// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 字符串主键的实体
/// </summary>
/// <remarks>
/// 主键与名字都放反斜杠、单引号与换行：SqlSugar 按主键删除、<c>In</c> 查询与多行写入把它们内联成字面量
/// </remarks>
[SugarTable("text_key_record")]
public sealed class TextKeyRecord : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 名字
    /// </summary>
    [StringLength(64)]
    public string Name { get; set; } = string.Empty;
}
