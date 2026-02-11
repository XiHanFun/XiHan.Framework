#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CronField
// Guid:39417368-36b3-417d-9044-745e5800a8c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/06 22:16:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Crons;

/// <summary>
/// Cron 字段对象
/// </summary>
public class CronField
{
    /// <summary>
    /// 是否为通配符（* 或 ?）
    /// </summary>
    public bool IsWildcard { get; set; }

    /// <summary>
    /// 具体的值列表
    /// </summary>
    public List<int> Values { get; set; } = [];

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的字段值</returns>
    public override string ToString()
    {
        if (IsWildcard)
        {
            return "*";
        }

        return string.Join(",", Values);
    }
}
