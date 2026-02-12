#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullAccessLogWriter
// Guid:f2b6b6cb-6ee4-4810-8ad8-ed94f18eb489
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 空访问日志写入器
/// </summary>
public class NullAccessLogWriter : IAccessLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
