// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 用户友好异常测试
/// </summary>
/// <remarks>
/// 这个类型的第二个构造重载首参是 <c>object</c>，与首参为 <c>string</c> 的第一个重载只差参数类型；
/// 传字符串永远命中字符串重载，只有传非字符串对象才会走本地化分支。用例刻意用一个自定义占位对象把这条重载边界钉死，
/// 否则「以为在传本地化消息、实际落到了普通消息」的误用不会被任何测试发现。
/// </remarks>
public class UserFriendlyExceptionTests
{
    /// <summary>
    /// 只传消息时消息原样保留，其余契约属性为空、日志级别为警告
    /// </summary>
    [Fact]
    public void Constructor_WithMessageOnly_KeepsMessageAndDefaults()
    {
        var exception = new UserFriendlyException("请先完成实名认证");

        Assert.Equal("请先完成实名认证", exception.Message);
        Assert.Null(exception.Code);
        Assert.Null(exception.Details);
        Assert.Null(exception.LocalizableMessage);
        Assert.Null(exception.InnerException);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 全参构造把每个参数落到对应属性上
    /// </summary>
    [Fact]
    public void Constructor_WithAllArguments_MapsEveryArgument()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new UserFriendlyException("请先完成实名认证", "XH-2001", "认证状态：未提交", inner, LogLevel.Error);

        Assert.Equal("请先完成实名认证", exception.Message);
        Assert.Equal("XH-2001", exception.Code);
        Assert.Equal("认证状态：未提交", exception.Details);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(LogLevel.Error, exception.LogLevel);
    }

    /// <summary>
    /// 传非字符串对象时命中本地化重载，回退消息落到消息上
    /// </summary>
    [Fact]
    public void Constructor_WithLocalizableMessage_KeepsObjectAndFallbackMessage()
    {
        LocalizableMessagePlaceholder localizable = new("Account:NotVerified");

        var exception = new UserFriendlyException(localizable, "请先完成实名认证", "XH-2001", "认证状态：未提交");

        Assert.Same(localizable, exception.LocalizableMessage);
        Assert.Equal("请先完成实名认证", exception.Message);
        Assert.Equal("XH-2001", exception.Code);
        Assert.Equal("认证状态：未提交", exception.Details);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 传字符串永远命中普通消息重载，不会被当成本地化消息
    /// </summary>
    [Fact]
    public void Constructor_WithStringFirstArgument_DoesNotBindToLocalizableOverload()
    {
        var exception = new UserFriendlyException("请先完成实名认证");

        Assert.Null(exception.LocalizableMessage);
    }

    /// <summary>
    /// 本地化重载允许不给回退消息，此时本地化对象仍被保留
    /// </summary>
    [Fact]
    public void Constructor_WithLocalizableMessageOnly_StillKeepsLocalizableObject()
    {
        LocalizableMessagePlaceholder localizable = new("Account:NotVerified");

        var exception = new UserFriendlyException(localizable);

        Assert.Same(localizable, exception.LocalizableMessage);
        Assert.NotNull(exception.Message);
    }

    /// <summary>
    /// 类型继承自业务异常并落在用户友好异常契约上
    /// </summary>
    [Fact]
    public void Type_ExtendsBusinessExceptionAndIsUserFriendly()
    {
        var exception = new UserFriendlyException("请先完成实名认证");

        Assert.IsAssignableFrom<BusinessException>(exception);
        Assert.IsAssignableFrom<IUserFriendlyException>(exception);
        Assert.IsAssignableFrom<IBusinessException>(exception);
    }

    /// <summary>
    /// 用户友好异常契约本身就是业务异常契约的细化
    /// </summary>
    [Fact]
    public void UserFriendlyContract_ExtendsBusinessContract()
    {
        Assert.True(typeof(IBusinessException).IsAssignableFrom(typeof(IUserFriendlyException)));
    }

    /// <summary>
    /// 框架扩展方法读到的是警告级别而不是默认的错误级别
    /// </summary>
    [Fact]
    public void GetLogLevel_DefaultsToWarning()
    {
        Assert.Equal(LogLevel.Warning, new UserFriendlyException("请先完成实名认证").GetLogLevel());
    }
}

/// <summary>
/// 本地化消息占位对象
/// </summary>
/// <remarks>
/// 框架把本地化消息以 <c>object</c> 弱类型存放，避免核心库反向依赖本地化抽象包，
/// 因此测试里用任意对象都能走通这条分支，这里用一个只带键名的占位类型表达意图。
/// </remarks>
/// <param name="Key">本地化键名</param>
public sealed record LocalizableMessagePlaceholder(string Key);
