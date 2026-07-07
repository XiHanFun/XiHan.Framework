#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullLoginLogWriter
// Guid:85930709-c392-4dad-b064-c7f0d36ec636
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空登录日志写入器
/// </summary>
public class NullLoginLogWriter : ILoginLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
