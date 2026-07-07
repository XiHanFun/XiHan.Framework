#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobExecutionException
// Guid:ed6d2b70-187c-486a-b842-34619f09c83c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 后台作业执行异常（作为"业务失败、可退避重试"的信号，与致命错误区分）
/// </summary>
public class BackgroundJobExecutionException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public BackgroundJobExecutionException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="innerException">内部异常</param>
    public BackgroundJobExecutionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 作业名称
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 作业参数（序列化后）
    /// </summary>
    public string? JobArgs { get; set; }
}
