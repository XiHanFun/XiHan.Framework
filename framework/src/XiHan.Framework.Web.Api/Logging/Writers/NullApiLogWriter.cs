#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullApiLogWriter
// Guid:e6f70819-0123-4567-8901-bcdef1234567
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Writers;

/// <summary>
/// 空接口日志写入器
/// </summary>
public class NullApiLogWriter : IApiLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
