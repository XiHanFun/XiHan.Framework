#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobExecutionContext
// Guid:4dfd048a-5602-4043-9156-856b1dcc8b37
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业执行上下文
/// </summary>
public class BackgroundJobExecutionContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">当前作用域的服务提供器</param>
    /// <param name="jobType">作业处理器类型</param>
    /// <param name="jobArgs">已反序列化的作业参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public BackgroundJobExecutionContext(
        IServiceProvider serviceProvider,
        Type jobType,
        object jobArgs,
        CancellationToken cancellationToken = default)
    {
        ServiceProvider = serviceProvider;
        JobType = jobType;
        JobArgs = jobArgs;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 当前作用域的服务提供器
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 作业处理器类型
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// 已反序列化的作业参数
    /// </summary>
    public object JobArgs { get; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
