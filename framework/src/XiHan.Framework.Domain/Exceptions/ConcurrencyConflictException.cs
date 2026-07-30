// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Exceptions;

/// <summary>
/// 乐观并发冲突异常
/// </summary>
/// <remarks>
/// 当基于行版本（Row_Version）的乐观锁校验失败——即待更新实体持有的版本与数据库当前版本不一致
/// （其他请求已抢先修改该行）——时抛出。调用方应重新加载最新数据后再重试。
/// 由仓储层把 SqlSugar 的 <c>VersionExceptions</c> 翻译为本异常，避免领域/应用层耦合 ORM 具体异常类型。
/// </remarks>
public class ConcurrencyConflictException : DomainException
{
    /// <summary>
    /// 默认提示消息
    /// </summary>
    private const string DefaultMessage = "数据已被其他操作修改，请刷新后重试。";

    /// <summary>
    /// 构造函数
    /// </summary>
    public ConcurrencyConflictException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
