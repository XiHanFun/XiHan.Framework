// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 演进前的表结构：只有主键与名字。
/// </summary>
[SugarTable("evolving_record")]
public sealed class EvolvingRecordV1
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 名字。
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
