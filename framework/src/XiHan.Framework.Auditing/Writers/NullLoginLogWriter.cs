// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Writers;

/// <summary>
/// 空登录日志写入器
/// </summary>
public class NullLoginLogWriter : ILoginLogWriter
{
    /// <summary>
    /// 写入登录日志，直接丢弃不做任何处理
    /// </summary>
    /// <param name="record">登录日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
