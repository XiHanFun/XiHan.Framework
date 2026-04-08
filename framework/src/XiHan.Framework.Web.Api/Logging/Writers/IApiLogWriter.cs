#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IApiLogWriter
// Guid:d5e6f708-9012-3456-7890-abcdef123456
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Writers;

/// <summary>
/// 接口日志写入器
/// </summary>
public interface IApiLogWriter
{
    /// <summary>
    /// 写入接口日志
    /// </summary>
    /// <param name="record">接口日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default);
}
