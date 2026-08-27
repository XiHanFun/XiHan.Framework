// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 初始化异常测试
/// </summary>
/// <remarks>
/// 应用装配期的所有失败都被包装成这个类型再抛给宿主，消息前缀是排查时的第一眼线索，逐字锁死。
/// 它直接继承 <see cref="Exception"/> 而不是 <see cref="XiHanException"/>——
/// 这意味着 <c>catch (XiHanException)</c> 抓不到装配失败，属对外可见的继承契约，单独立用例。
/// </remarks>
public class InitializationExceptionTests
{
    /// <summary>
    /// 无条件附加的消息前缀
    /// </summary>
    private const string MessagePrefix = "程序初始化异常。";

    /// <summary>
    /// 无参构造只得到默认消息
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDefaultMessageOnly()
    {
        var exception = new InitializationException();

        Assert.Equal(MessagePrefix, exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 传消息时拼在前缀之后
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_PrefixesDefaultMessage()
    {
        var exception = new InitializationException("模块 A 装配失败");

        Assert.Equal(MessagePrefix + "模块 A 装配失败", exception.Message);
    }

    /// <summary>
    /// 消息为空时退化为只有前缀
    /// </summary>
    [Fact]
    public void Constructor_WithNullMessage_FallsBackToPrefixOnly()
    {
        var exception = new InitializationException(null);

        Assert.Equal(MessagePrefix, exception.Message);
    }

    /// <summary>
    /// 同时传消息与内部异常时两者都被保留
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new InitializationException("模块 A 装配失败", inner);

        Assert.Equal(MessagePrefix + "模块 A 装配失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 内部异常为空时也不抛错
    /// </summary>
    /// <remarks>
    /// 构造函数在记录日志时会读内部异常的堆栈，参数可空意味着必须容忍空值，
    /// 这条用例把「传 null 也要能构造出来」固定下来。
    /// </remarks>
    [Fact]
    public void Constructor_WithNullInnerException_DoesNotThrow()
    {
        var exception = new InitializationException("模块 A 装配失败", null);

        Assert.Null(exception.InnerException);
        Assert.Equal(MessagePrefix + "模块 A 装配失败", exception.Message);
    }

    /// <summary>
    /// 类型直接继承自异常基类，不落在框架异常继承线上
    /// </summary>
    [Fact]
    public void Type_ExtendsExceptionDirectlyNotXiHanException()
    {
        var exception = new InitializationException();

        Assert.IsAssignableFrom<Exception>(exception);
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(InitializationException)));
        Assert.Equal(typeof(Exception), typeof(InitializationException).BaseType);
    }

    /// <summary>
    /// 不承载错误码、日志级别与业务异常契约
    /// </summary>
    [Fact]
    public void Type_CarriesNoErrorContract()
    {
        var exception = new InitializationException();

        Assert.False(exception is IHasErrorCode);
        Assert.False(exception is IHasLogLevel);
        Assert.False(exception is IBusinessException);
        Assert.Equal(LogLevel.Error, exception.GetLogLevel());
    }

    /// <summary>
    /// 类型只公开三个构造函数
    /// </summary>
    [Fact]
    public void Type_ExposesThreePublicConstructors()
    {
        var signatures = typeof(InitializationException)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(constructor => string.Join(",", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["", "String", "String,Exception"];

        Assert.Equal(expected, signatures);
    }
}
