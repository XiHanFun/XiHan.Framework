// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 关闭过程异常测试
/// </summary>
/// <remarks>
/// 与 <see cref="InitializationException"/> 对称：模块关闭阶段的失败统一包成这个类型。
/// 前缀不同是刻意的，日志里靠前缀就能一眼区分「起不来」和「关不掉」，因此逐字锁死。
/// </remarks>
public class ShutdownExceptionTests
{
    /// <summary>
    /// 无条件附加的消息前缀
    /// </summary>
    private const string MessagePrefix = "程序关闭过程异常。";

    /// <summary>
    /// 无参构造只得到默认消息
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDefaultMessageOnly()
    {
        var exception = new ShutdownException();

        Assert.Equal(MessagePrefix, exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 传消息时拼在前缀之后
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_PrefixesDefaultMessage()
    {
        var exception = new ShutdownException("模块 A 关闭失败");

        Assert.Equal(MessagePrefix + "模块 A 关闭失败", exception.Message);
    }

    /// <summary>
    /// 消息为空时退化为只有前缀
    /// </summary>
    [Fact]
    public void Constructor_WithNullMessage_FallsBackToPrefixOnly()
    {
        var exception = new ShutdownException(null);

        Assert.Equal(MessagePrefix, exception.Message);
    }

    /// <summary>
    /// 同时传消息与内部异常时两者都被保留
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new ShutdownException("模块 A 关闭失败", inner);

        Assert.Equal(MessagePrefix + "模块 A 关闭失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 前缀与初始化异常不同，日志里可据此区分启动失败与关闭失败
    /// </summary>
    [Fact]
    public void MessagePrefix_DiffersFromInitializationException()
    {
        Assert.NotEqual(new InitializationException().Message, new ShutdownException().Message);
    }

    /// <summary>
    /// 类型直接继承自异常基类，不落在框架异常继承线上，也不承载错误契约
    /// </summary>
    [Fact]
    public void Type_ExtendsExceptionDirectlyAndCarriesNoErrorContract()
    {
        var exception = new ShutdownException();

        Assert.Equal(typeof(Exception), typeof(ShutdownException).BaseType);
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(ShutdownException)));
        Assert.False(exception is IHasErrorCode);
        Assert.False(exception is IHasLogLevel);
        Assert.Equal(LogLevel.Error, exception.GetLogLevel());
    }

    /// <summary>
    /// 类型只公开三个构造函数
    /// </summary>
    [Fact]
    public void Type_ExposesThreePublicConstructors()
    {
        var signatures = typeof(ShutdownException)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(constructor => string.Join(",", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["", "String", "String,Exception"];

        Assert.Equal(expected, signatures);
    }
}
