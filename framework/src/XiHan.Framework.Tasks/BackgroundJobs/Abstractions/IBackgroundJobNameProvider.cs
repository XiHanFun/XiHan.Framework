// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业名称提供器（用于给作业一个稳定的持久化名字，重命名类型不影响已入库作业）
/// </summary>
public interface IBackgroundJobNameProvider
{
    /// <summary>
    /// 作业名称
    /// </summary>
    string Name { get; }
}
