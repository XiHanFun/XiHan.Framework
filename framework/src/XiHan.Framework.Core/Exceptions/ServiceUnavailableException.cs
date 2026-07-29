// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Exceptions;

/// <summary>
/// 依赖服务不可用异常（映射 HTTP 503）
/// </summary>
/// <remarks>
/// <para>
/// 用于把「外部基础设施连不上」这类故障翻译成可读、可分类的失败：向量库、消息中间件、
/// 第三方接口等依赖不可达时抛出，携带可操作的排查线索，原始异常保留在 <see cref="Exception.InnerException"/> 供日志。
/// </para>
/// <para>
/// 与 <see cref="UserFriendlyException"/> 的分工：后者表示<b>调用方输入或业务状态</b>有问题（400），
/// 重试同样的请求仍会失败；本异常表示<b>服务端依赖</b>暂时不可用（503），依赖恢复后同样的请求即可成功。
/// 监控与告警据此区分「用户操作错误」和「基础设施故障」，因此不要用前者代替后者。
/// </para>
/// <para>翻译不等于吞掉：本异常仍然中断流程、仍然被记录，只是不再把裸驱动堆栈抛给调用方。</para>
/// </remarks>
public class ServiceUnavailableException : BusinessException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">面向运维的可读消息，应指明是哪个依赖不可用以及去哪里检查</param>
    /// <param name="code">业务码</param>
    /// <param name="details">明细</param>
    /// <param name="innerException">原始基础设施异常，保留供日志排查</param>
    /// <param name="logLevel">日志级别；依赖不可用属于需要关注的故障，默认 Error</param>
    public ServiceUnavailableException(
        string message,
        string? code = null,
        string? details = null,
        Exception? innerException = null,
        LogLevel logLevel = LogLevel.Error)
        : base(code, message, details, innerException, logLevel)
    {
        Details = details;
    }
}
