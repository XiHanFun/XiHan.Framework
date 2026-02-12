#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullExceptionLogWriter
// Guid:6e3df022-a315-4302-bf8a-9b973de8fe43
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:25:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 空异常日志写入器
/// </summary>
public class NullExceptionLogWriter : IExceptionLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
