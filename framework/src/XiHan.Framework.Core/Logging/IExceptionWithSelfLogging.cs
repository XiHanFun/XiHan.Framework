#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExceptionWithSelfLogging
// Guid:97620453-9cb6-442c-82f4-7e441d9d8446
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:09:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 自身日志接口
/// </summary>
public interface IExceptionWithSelfLogging
{
    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="logger"></param>
    void Log(ILogger logger);
}