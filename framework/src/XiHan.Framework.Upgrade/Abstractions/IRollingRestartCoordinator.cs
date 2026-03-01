#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRollingRestartCoordinator
// Guid:0c14bbd2-38cb-4b47-9c3f-08b4b8a5e4bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:26:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 滚动重启协调器接口
/// </summary>
public interface IRollingRestartCoordinator
{
    /// <summary>
    /// 执行滚动重启
    /// </summary>
    Task RestartAsync(CancellationToken cancellationToken = default);
}
