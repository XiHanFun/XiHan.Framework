#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBackgroundJobNameProvider
// Guid:2d062932-dc71-4c4c-bf0f-a146d3e36d45
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
