// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Localization;
using XiHan.Framework.Core.Logging;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 异常体系继承关系与描述性契约测试
/// </summary>
/// <remarks>
/// 核心库的异常分成互不相交的两族：
/// 「业务族」（<see cref="BusinessException"/> 及其子类）带错误码／明细／日志级别，会被统一异常处理回显；
/// 「框架族」（<see cref="XiHanException"/>、<see cref="InitializationException"/>、<see cref="ShutdownException"/>）
/// 是纯异常，按默认错误级别记录、不回显。
/// 单个类型的行为在各自的用例文件里覆盖，这里只锁两族的边界，防止有人给框架族随手加上业务契约。
/// </remarks>
public class ExceptionHierarchyTests
{
    /// <summary>
    /// 业务族异常全部承载业务、错误码、明细与日志级别四份契约
    /// </summary>
    /// <param name="exceptionType">业务族异常类型</param>
    [Theory]
    [InlineData(typeof(BusinessException))]
    [InlineData(typeof(UserFriendlyException))]
    [InlineData(typeof(ServiceUnavailableException))]
    public void BusinessExceptionFamily_CarriesAllErrorContracts(Type exceptionType)
    {
        Assert.True(typeof(Exception).IsAssignableFrom(exceptionType));
        Assert.True(typeof(BusinessException).IsAssignableFrom(exceptionType));
        Assert.True(typeof(IBusinessException).IsAssignableFrom(exceptionType));
        Assert.True(typeof(IHasErrorCode).IsAssignableFrom(exceptionType));
        Assert.True(typeof(IHasErrorDetails).IsAssignableFrom(exceptionType));
        Assert.True(typeof(IHasLogLevel).IsAssignableFrom(exceptionType));
    }

    /// <summary>
    /// 框架族异常一份描述性契约都不承载
    /// </summary>
    /// <param name="exceptionType">框架族异常类型</param>
    [Theory]
    [InlineData(typeof(XiHanException))]
    [InlineData(typeof(InitializationException))]
    [InlineData(typeof(ShutdownException))]
    public void FrameworkExceptionFamily_CarriesNoErrorContract(Type exceptionType)
    {
        Assert.True(typeof(Exception).IsAssignableFrom(exceptionType));
        Assert.False(typeof(IBusinessException).IsAssignableFrom(exceptionType));
        Assert.False(typeof(IHasErrorCode).IsAssignableFrom(exceptionType));
        Assert.False(typeof(IHasErrorDetails).IsAssignableFrom(exceptionType));
        Assert.False(typeof(IHasLogLevel).IsAssignableFrom(exceptionType));
    }

    /// <summary>
    /// 两族互不相交：业务族不是框架异常，框架族也不是业务异常
    /// </summary>
    [Fact]
    public void TwoFamilies_DoNotIntersect()
    {
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(BusinessException)));
        Assert.False(typeof(BusinessException).IsAssignableFrom(typeof(XiHanException)));
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(InitializationException)));
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(ShutdownException)));
    }

    /// <summary>
    /// 核心库的异常都不承载网络状态码与自述日志契约
    /// </summary>
    /// <remarks>
    /// 这两份契约是留给上层的扩展点：状态码由 Web 层异常兑现，自述日志由验证等携带明细的异常兑现。
    /// 核心库自己实现了反而会把上层的映射策略提前写死，因此这条边界值得固定。
    /// </remarks>
    [Fact]
    public void CoreExceptions_DoNotImplementExtensionPointContracts()
    {
        Type[] coreExceptionTypes =
        [
            typeof(XiHanException),
            typeof(InitializationException),
            typeof(ShutdownException),
            typeof(BusinessException),
            typeof(UserFriendlyException),
            typeof(ServiceUnavailableException)
        ];

        Assert.All(coreExceptionTypes, type =>
        {
            Assert.False(typeof(IHasHttpStatusCode).IsAssignableFrom(type));
            Assert.False(typeof(IExceptionWithSelfLogging).IsAssignableFrom(type));
            Assert.False(typeof(ILocalizeErrorMessage).IsAssignableFrom(type));
        });
    }

    /// <summary>
    /// 四份描述性契约互相独立，任何一份都不继承另一份
    /// </summary>
    /// <remarks>
    /// 独立性决定了实现方可以只挑需要的那几份来兑现，
    /// 一旦其中一个接口继承了另一个，所有实现方都会被动多出一份必须实现的成员。
    /// </remarks>
    [Fact]
    public void ErrorContracts_AreIndependentOfEachOther()
    {
        Type[] contracts =
        [
            typeof(IHasErrorCode),
            typeof(IHasErrorDetails),
            typeof(IHasLogLevel),
            typeof(IHasHttpStatusCode)
        ];

        foreach (var contract in contracts)
        {
            Assert.Empty(contract.GetInterfaces());
        }
    }

    /// <summary>
    /// 错误码与明细只读，日志级别可读可写
    /// </summary>
    /// <remarks>
    /// 日志级别之所以必须可写，是因为统一异常处理允许按运行期策略把某类异常降级或升级；
    /// 错误码与明细则属于异常自身的事实，不允许被链路上的中间件改写。
    /// </remarks>
    [Fact]
    public void ErrorContracts_HaveExpectedAccessorShape()
    {
        var code = typeof(IHasErrorCode).GetProperty(nameof(IHasErrorCode.Code));
        Assert.NotNull(code);
        Assert.Equal(typeof(string), code!.PropertyType);
        Assert.Null(code.SetMethod);

        var details = typeof(IHasErrorDetails).GetProperty(nameof(IHasErrorDetails.Details));
        Assert.NotNull(details);
        Assert.Equal(typeof(string), details!.PropertyType);
        Assert.Null(details.SetMethod);

        var logLevel = typeof(IHasLogLevel).GetProperty(nameof(IHasLogLevel.LogLevel));
        Assert.NotNull(logLevel);
        Assert.Equal(typeof(LogLevel), logLevel!.PropertyType);
        Assert.NotNull(logLevel.GetMethod);
        Assert.NotNull(logLevel.SetMethod);

        var httpStatusCode = typeof(IHasHttpStatusCode).GetProperty(nameof(IHasHttpStatusCode.HttpStatusCode));
        Assert.NotNull(httpStatusCode);
        Assert.Equal(typeof(int), httpStatusCode!.PropertyType);
        Assert.Null(httpStatusCode.SetMethod);
    }

    /// <summary>
    /// 自述日志契约只暴露一个接收日志记录器的方法
    /// </summary>
    [Fact]
    public void SelfLoggingContract_ExposesSingleLogMethod()
    {
        var method = Assert.Single(typeof(IExceptionWithSelfLogging).GetMethods());

        Assert.Equal(nameof(IExceptionWithSelfLogging.Log), method.Name);
        Assert.Equal(typeof(void), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(ILogger), parameter.ParameterType);
    }

    /// <summary>
    /// 本地化报错信息契约只暴露一个接收本地化上下文并返回文本的方法
    /// </summary>
    [Fact]
    public void LocalizeErrorMessageContract_ExposesSingleLocalizeMethod()
    {
        var method = Assert.Single(typeof(ILocalizeErrorMessage).GetMethods());

        Assert.Equal(nameof(ILocalizeErrorMessage.LocalizeErrorMessage), method.Name);
        Assert.Equal(typeof(string), method.ReturnType);

        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(LocalizationContext), parameter.ParameterType);
    }
}
