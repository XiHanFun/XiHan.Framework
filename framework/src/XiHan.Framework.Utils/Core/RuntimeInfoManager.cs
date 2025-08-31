#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RuntimeInfoManager
// Guid:36df3b7c-eaa6-46b7-b7de-9623ea769fad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/9 6:54:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Runtime;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 运行时信息管理器
/// </summary>
public static class RuntimeInfoManager
{
    /// <summary>
    /// 获取当前运行时信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    /// <returns>运行时信息</returns>
    public static SystemRuntimeInfo GetSystemRuntimeInfo()
    {
        return new SystemRuntimeInfo
        {
            RuntimeInfo = OsPlatformHelper.RuntimeInfos,
            RunningTime = RunningTimeHelper.RunningTime
        };
    }
}

/// <summary>
/// 系统运行时信息
/// </summary>
public class SystemRuntimeInfo
{
    /// <summary>
    /// 运行时信息
    /// </summary>
    public RuntimeInfo RuntimeInfo { get; set; } = new();

    /// <summary>
    /// 运行时间
    /// </summary>
    public string RunningTime { get; set; } = string.Empty;
}