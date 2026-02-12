#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAccessLogWriter
// Guid:ec8802d7-2cf8-46cb-aa66-9f6603004ebb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:22:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 访问日志写入器
/// </summary>
public interface IAccessLogWriter
{
    /// <summary>
    /// 写入访问日志
    /// </summary>
    /// <param name="record">访问日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default);
}
