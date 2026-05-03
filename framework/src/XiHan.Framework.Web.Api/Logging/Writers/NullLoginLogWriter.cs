#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullLoginLogWriter
// Guid:c9e4f73a-5f2g-4d8c-bg07-3h5d8c0e4f6g
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/03 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Writers;

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
