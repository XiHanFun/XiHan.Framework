// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 记录写入调用的访问日志写入器替身
/// </summary>
public sealed class RecordingAccessLogWriter : IAccessLogWriter
{
    /// <summary>
    /// 收到的记录（按写入顺序）
    /// </summary>
    public List<AccessLogRecord> Records { get; } = [];

    /// <summary>
    /// 每次写入收到的取消令牌
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>
    /// 写入访问日志
    /// </summary>
    /// <param name="record">访问日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入任务</returns>
    public Task WriteAsync(AccessLogRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录写入调用的操作日志写入器替身
/// </summary>
public sealed class RecordingOperationLogWriter : IOperationLogWriter
{
    /// <summary>
    /// 收到的记录（按写入顺序）
    /// </summary>
    public List<OperationLogRecord> Records { get; } = [];

    /// <summary>
    /// 每次写入收到的取消令牌
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>
    /// 写入操作日志
    /// </summary>
    /// <param name="record">操作日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入任务</returns>
    public Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录写入调用的异常日志写入器替身
/// </summary>
public sealed class RecordingExceptionLogWriter : IExceptionLogWriter
{
    /// <summary>
    /// 收到的记录（按写入顺序）
    /// </summary>
    public List<ExceptionLogRecord> Records { get; } = [];

    /// <summary>
    /// 每次写入收到的取消令牌
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>
    /// 写入异常日志
    /// </summary>
    /// <param name="record">异常日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入任务</returns>
    public Task WriteAsync(ExceptionLogRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录写入调用的接口日志写入器替身
/// </summary>
public sealed class RecordingApiLogWriter : IApiLogWriter
{
    /// <summary>
    /// 收到的记录（按写入顺序）
    /// </summary>
    public List<ApiLogRecord> Records { get; } = [];

    /// <summary>
    /// 每次写入收到的取消令牌
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>
    /// 写入接口日志
    /// </summary>
    /// <param name="record">接口日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入任务</returns>
    public Task WriteAsync(ApiLogRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录写入调用的登录日志写入器替身
/// </summary>
public sealed class RecordingLoginLogWriter : ILoginLogWriter
{
    /// <summary>
    /// 收到的记录（按写入顺序）
    /// </summary>
    public List<LoginLogRecord> Records { get; } = [];

    /// <summary>
    /// 每次写入收到的取消令牌
    /// </summary>
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>
    /// 写入登录日志
    /// </summary>
    /// <param name="record">登录日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入任务</returns>
    public Task WriteAsync(LoginLogRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}
