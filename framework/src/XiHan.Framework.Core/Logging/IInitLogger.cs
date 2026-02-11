#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IInitLogger
// Guid:fda85c50-8a0a-4173-bcba-26a8d03b8639
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:17:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 初始化日志接口
/// </summary>
public interface IInitLogger<out T> : ILogger<T>
{
    /// <summary>
    /// 日志入口
    /// </summary>
    public List<XiHanInitLogEntry> Entries { get; }
}
