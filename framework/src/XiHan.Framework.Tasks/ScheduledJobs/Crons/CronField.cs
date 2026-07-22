// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
