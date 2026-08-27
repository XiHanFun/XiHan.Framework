// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 曦寒框架异常测试
/// </summary>
/// <remarks>
/// 这个基类只做一件事：把固定前缀无条件拼在消息最前面。前缀会直接进日志与接口错误体，属对外可见行为，逐字锁死。
/// 子类 <c>XiHanValidationException</c> 的前缀行为已在验证抽象包的测试里覆盖，这里只测基类自身的三个构造重载，
/// 以及「基类刻意不承载错误码／日志级别契约」这条边界。
/// </remarks>
public class XiHanExceptionTests
{
    /// <summary>
    /// 基类无条件附加的消息前缀
    /// </summary>
    private const string MessagePrefix = "曦寒框架异常。";

    /// <summary>
    /// 无参构造只得到框架默认消息，且没有内部异常
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesFrameworkMessageOnly()
    {
        var exception = new XiHanException();

        Assert.Equal(MessagePrefix, exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 传消息时拼在框架前缀之后
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_PrefixesFrameworkMessage()
    {
        var exception = new XiHanException("模块装配失败");

        Assert.Equal(MessagePrefix + "模块装配失败", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 消息为空时退化为只有前缀
    /// </summary>
    [Fact]
    public void Constructor_WithNullMessage_FallsBackToPrefixOnly()
    {
        var exception = new XiHanException(null);

        Assert.Equal(MessagePrefix, exception.Message);
    }

    /// <summary>
    /// 同时传消息与内部异常时两者都被保留
    /// </summary>
    [Fact]
    public void Constructor_WithMessageAndInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new XiHanException("模块装配失败", inner);

        Assert.Equal(MessagePrefix + "模块装配失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 内部异常可以为空
    /// </summary>
    [Fact]
    public void Constructor_WithNullInnerException_KeepsMessage()
    {
        var exception = new XiHanException("模块装配失败", null);

        Assert.Equal(MessagePrefix + "模块装配失败", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 基类只是普通异常，不承载错误码、错误详情、日志级别与业务异常契约
    /// </summary>
    /// <remarks>
    /// 这条边界决定了框架异常在统一异常处理管线里的落点：
    /// 没有 <see cref="IHasLogLevel"/> 就按 Error 记，没有 <see cref="IBusinessException"/> 就不会被当成可回显的业务错误。
    /// </remarks>
    [Fact]
    public void Type_CarriesNoErrorContract()
    {
        var exception = new XiHanException("模块装配失败");

        Assert.IsAssignableFrom<Exception>(exception);
        Assert.False(exception is IHasErrorCode);
        Assert.False(exception is IHasErrorDetails);
        Assert.False(exception is IHasLogLevel);
        Assert.False(exception is IBusinessException);
    }

    /// <summary>
    /// 没有日志级别契约时框架扩展方法按默认的错误级别处理
    /// </summary>
    [Fact]
    public void GetLogLevel_WithoutLogLevelContract_ReturnsError()
    {
        var exception = new XiHanException("模块装配失败");

        Assert.Equal(LogLevel.Error, exception.GetLogLevel());
    }

    /// <summary>
    /// 抛出后可按框架异常基类捕获并带上堆栈
    /// </summary>
    [Fact]
    public void Throw_IsCatchableAsXiHanException()
    {
        XiHanException? caught = null;

        try
        {
            throw new XiHanException("模块装配失败");
        }
        catch (XiHanException thrown)
        {
            caught = thrown;
        }

        Assert.NotNull(caught);
        Assert.NotNull(caught!.StackTrace);
    }

    /// <summary>
    /// 类型只公开三个构造函数
    /// </summary>
    /// <remarks>
    /// 缺少「仅内部异常」的重载是刻意的：框架异常必须带上人能读的说明，
    /// 这里把重载集合固定下来，防止后续无声增删。
    /// </remarks>
    [Fact]
    public void Type_ExposesThreePublicConstructors()
    {
        var signatures = typeof(XiHanException)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(constructor => string.Join(",", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["", "String", "String,Exception"];

        Assert.Equal(expected, signatures);
    }
}
