#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobTimeoutAttribute.cs
// Guid:afbfac42-54a6-45b3-b46c-ab23e59d1f59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:19:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务超时特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobTimeoutAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
    public JobTimeoutAttribute(int timeoutMilliseconds)
    {
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMilliseconds { get; }
}
