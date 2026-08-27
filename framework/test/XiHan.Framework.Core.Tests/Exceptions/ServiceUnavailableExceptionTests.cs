// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 依赖服务不可用异常测试
/// </summary>
/// <remarks>
/// 与 <see cref="UserFriendlyException"/> 是一对：都继承业务异常，但默认日志级别刻意不同——
/// 用户友好异常默认 Warning（调用方的问题），本异常默认 Error（服务端依赖故障，需要告警）。
/// 这条差异是监控分流的依据，因此单独立用例对照锁死；同时确认它<b>不是</b>用户友好异常，
/// 免得被当成可直接回显给终端用户的文案。
/// </remarks>
public class ServiceUnavailableExceptionTests
{
    /// <summary>
    /// 只传消息时默认日志级别为错误，其余契约属性为空
    /// </summary>
    [Fact]
    public void Constructor_WithMessageOnly_DefaultsToErrorLevel()
    {
        var exception = new ServiceUnavailableException("向量库不可达，请检查 Qdrant 连接串");

        Assert.Equal("向量库不可达，请检查 Qdrant 连接串", exception.Message);
        Assert.Null(exception.Code);
        Assert.Null(exception.Details);
        Assert.Null(exception.InnerException);
        Assert.Equal(LogLevel.Error, exception.LogLevel);
    }

    /// <summary>
    /// 默认日志级别与用户友好异常刻意相反
    /// </summary>
    [Fact]
    public void DefaultLogLevel_DiffersFromUserFriendlyException()
    {
        Assert.Equal(LogLevel.Error, new ServiceUnavailableException("依赖不可用").GetLogLevel());
        Assert.Equal(LogLevel.Warning, new UserFriendlyException("参数不合法").GetLogLevel());
    }

    /// <summary>
    /// 全参构造把每个参数落到对应属性上，并保留原始基础设施异常
    /// </summary>
    [Fact]
    public void Constructor_WithAllArguments_KeepsOriginalInfrastructureException()
    {
        var inner = new TimeoutException("连接超时");

        var exception = new ServiceUnavailableException("向量库不可达", "XH-5030", "endpoint=127.0.0.1:6333", inner, LogLevel.Critical);

        Assert.Equal("XH-5030", exception.Code);
        Assert.Equal("endpoint=127.0.0.1:6333", exception.Details);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(LogLevel.Critical, exception.LogLevel);
    }

    /// <summary>
    /// 类型继承自业务异常，但不是用户友好异常
    /// </summary>
    [Fact]
    public void Type_ExtendsBusinessExceptionButIsNotUserFriendly()
    {
        var exception = new ServiceUnavailableException("向量库不可达");

        Assert.IsAssignableFrom<BusinessException>(exception);
        Assert.IsAssignableFrom<IBusinessException>(exception);
        Assert.False(exception is IUserFriendlyException);
    }

    /// <summary>
    /// 原始异常保留在内部异常里，格式化消息能把两层原因串起来
    /// </summary>
    /// <remarks>
    /// 「翻译不等于吞掉」：把裸驱动异常翻译成可读消息之后，原始堆栈仍要能顺着内部异常链拿到，
    /// 否则线上排查会丢掉真正的失败点。
    /// </remarks>
    [Fact]
    public void FormatMessage_ChainsTranslatedAndOriginalReason()
    {
        var inner = new TimeoutException("连接超时");
        var exception = new ServiceUnavailableException("向量库不可达", innerException: inner);

        var formatted = exception.FormatMessage();

        Assert.Contains("向量库不可达", formatted, StringComparison.Ordinal);
        Assert.Contains("连接超时", formatted, StringComparison.Ordinal);
    }
}
