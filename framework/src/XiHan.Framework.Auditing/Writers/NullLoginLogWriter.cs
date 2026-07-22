// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
