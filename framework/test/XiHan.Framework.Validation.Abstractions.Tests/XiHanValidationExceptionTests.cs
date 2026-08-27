// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Validation.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Validation.Abstractions.Tests;

/// <summary>
/// 曦寒验证异常测试
/// </summary>
/// <remarks>
/// 这个类型同时兑现四份契约：异常本身（消息拼装、内部异常）、<see cref="IHasValidationErrors"/>（错误明细集合）、
/// <see cref="IHasLogLevel"/>（可改写的日志级别）、<see cref="IExceptionWithSelfLogging"/>（自述日志）。
/// 断言按这四份契约组织，而不是按方法逐个凑。
/// 消息前缀 <c>曦寒框架异常。</c> 由基类 <see cref="XiHanException"/> 无条件拼在最前面，
/// 属于对外可见行为（会直接进日志和 API 错误体），因此逐字锁死。
/// </remarks>
public class XiHanValidationExceptionTests
{
    /// <summary>
    /// 基类无条件附加的消息前缀
    /// </summary>
    private const string FrameworkMessagePrefix = "曦寒框架异常。";

    /// <summary>
    /// 无参构造：错误明细为空集合，日志级别默认为警告
    /// </summary>
    [Fact]
    public void Constructor_Default_HasEmptyErrorsAndWarningLevel()
    {
        var exception = new XiHanValidationException();

        Assert.NotNull(exception.ValidationErrors);
        Assert.Empty(exception.ValidationErrors);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
        Assert.Null(exception.InnerException);
        Assert.Equal(FrameworkMessagePrefix, exception.Message);
    }

    /// <summary>
    /// 仅传消息时消息被拼在框架前缀之后，错误明细仍为空集合
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_PrefixesFrameworkMessageAndKeepsErrorsEmpty()
    {
        var exception = new XiHanValidationException("用户名不能为空");

        Assert.Equal(FrameworkMessagePrefix + "用户名不能为空", exception.Message);
        Assert.Empty(exception.ValidationErrors);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 仅传错误明细时直接持有传入集合本身，消息退化为框架默认消息
    /// </summary>
    [Fact]
    public void Constructor_WithValidationErrors_KeepsSameListInstanceAndDefaultMessage()
    {
        IList<ValidationResult> errors = new List<ValidationResult>
        {
            new ValidationResult("用户名不能为空", new[] { "UserName" })
        };

        var exception = new XiHanValidationException(errors);

        // 该重载没有把消息透传给基类，调用方只给明细时拿不到业务描述
        Assert.Equal(FrameworkMessagePrefix, exception.Message);
        Assert.Same(errors, exception.ValidationErrors);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 同时传消息与错误明细时两者都被保留
    /// </summary>
    [Fact]
    public void Constructor_WithMessageAndValidationErrors_KeepsBoth()
    {
        IList<ValidationResult> errors = new List<ValidationResult>
        {
            new ValidationResult("邮箱格式不正确", new[] { "Email" })
        };

        var exception = new XiHanValidationException("模型校验失败", errors);

        Assert.Equal(FrameworkMessagePrefix + "模型校验失败", exception.Message);
        Assert.Same(errors, exception.ValidationErrors);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 传内部异常时保留原始引用，错误明细仍为空集合
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsInnerAndEmptyErrors()
    {
        var inner = new InvalidOperationException("底层转换失败");

        var exception = new XiHanValidationException("模型校验失败", inner);

        Assert.Equal(FrameworkMessagePrefix + "模型校验失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Empty(exception.ValidationErrors);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 类型同时落在异常基类与三个框架接口上
    /// </summary>
    [Fact]
    public void Type_ImplementsFrameworkContracts()
    {
        var exception = new XiHanValidationException();

        Assert.IsAssignableFrom<Exception>(exception);
        Assert.IsAssignableFrom<XiHanException>(exception);
        Assert.IsAssignableFrom<IHasValidationErrors>(exception);
        Assert.IsAssignableFrom<IHasLogLevel>(exception);
        Assert.IsAssignableFrom<IExceptionWithSelfLogging>(exception);
    }

    /// <summary>
    /// 错误明细属性没有 setter，但集合内容可以在构造后继续追加
    /// </summary>
    [Fact]
    public void ValidationErrors_HasNoSetterButCollectionIsMutable()
    {
        var property = typeof(XiHanValidationException).GetProperty(nameof(XiHanValidationException.ValidationErrors));

        Assert.NotNull(property);
        Assert.Equal(typeof(IList<ValidationResult>), property!.PropertyType);
        Assert.Null(property.SetMethod);

        var exception = new XiHanValidationException();
        exception.ValidationErrors.Add(new ValidationResult("用户名不能为空", new[] { "UserName" }));

        Assert.Single(exception.ValidationErrors);
    }

    /// <summary>
    /// 日志级别可被调用方改写，并能通过接口与框架扩展方法读回
    /// </summary>
    [Fact]
    public void LogLevel_IsWritableAndReadableThroughContracts()
    {
        var exception = new XiHanValidationException("模型校验失败")
        {
            LogLevel = LogLevel.Critical
        };

        Assert.Equal(LogLevel.Critical, exception.LogLevel);
        Assert.Equal(LogLevel.Critical, ((IHasLogLevel)exception).LogLevel);

        // GetLogLevel 的默认值是 Error，这里能读到 Critical 说明 IHasLogLevel 被正确识别
        Assert.Equal(LogLevel.Critical, exception.GetLogLevel());
    }

    /// <summary>
    /// 未改写日志级别时框架扩展方法读到的是构造期写入的警告级别
    /// </summary>
    [Fact]
    public void GetLogLevel_WhenNotOverridden_ReturnsWarningInsteadOfDefaultError()
    {
        var exception = new XiHanValidationException("模型校验失败");

        Assert.Equal(LogLevel.Warning, exception.GetLogLevel());
    }

    /// <summary>
    /// 日志记录器为 null 时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void Log_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        var exception = new XiHanValidationException("模型校验失败", new List<ValidationResult>
        {
            new ValidationResult("用户名不能为空", new[] { "UserName" })
        });

        var thrown = Assert.Throws<ArgumentNullException>(() => exception.Log(null!));

        Assert.Equal("logger", thrown.ParamName);
    }

    /// <summary>
    /// 多条错误明细被汇总成一条日志，且逐条带出成员名
    /// </summary>
    [Fact]
    public void Log_WithMultipleErrors_WritesSingleAggregatedEntry()
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException("模型校验失败", new List<ValidationResult>
        {
            new ValidationResult("用户名不能为空", new[] { "UserName" }),
            new ValidationResult("邮箱格式不正确", new[] { "Email", "Contact" })
        });

        exception.Log(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("存在 2 个验证错误：", entry.Message);
        Assert.Contains("用户名不能为空 (UserName)", entry.Message);
        Assert.Contains("邮箱格式不正确 (Email, Contact)", entry.Message);
    }

    /// <summary>
    /// 错误明细没有成员名时不追加括号后缀
    /// </summary>
    [Fact]
    public void Log_WhenMemberNamesEmpty_OmitsParentheses()
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException(new List<ValidationResult>
        {
            new ValidationResult("整体校验未通过")
        });

        exception.Log(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("存在 1 个验证错误：", entry.Message);
        Assert.Contains("整体校验未通过", entry.Message);
        Assert.DoesNotContain("整体校验未通过 (", entry.Message);
    }

    /// <summary>
    /// 错误消息为 null 时仍然把成员名输出出去
    /// </summary>
    [Fact]
    public void Log_WhenErrorMessageIsNull_StillRendersMemberNames()
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException(new List<ValidationResult>
        {
            new ValidationResult(null, new[] { "UserName" })
        });

        exception.Log(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("(UserName)", entry.Message);
    }

    /// <summary>
    /// 自述日志按当前日志级别落到日志记录器对应的级别上
    /// </summary>
    /// <param name="configured">异常上配置的日志级别</param>
    /// <param name="expected">日志记录器实际收到的级别</param>
    /// <remarks>
    /// None 与 Debug 都会落到 Debug，这是框架 LogWithLevel 的兜底分支，属于对外可见行为。
    /// </remarks>
    [Theory]
    [InlineData(LogLevel.Critical, LogLevel.Critical)]
    [InlineData(LogLevel.Error, LogLevel.Error)]
    [InlineData(LogLevel.Warning, LogLevel.Warning)]
    [InlineData(LogLevel.Information, LogLevel.Information)]
    [InlineData(LogLevel.Trace, LogLevel.Trace)]
    [InlineData(LogLevel.Debug, LogLevel.Debug)]
    [InlineData(LogLevel.None, LogLevel.Debug)]
    public void Log_UsesConfiguredLogLevel(LogLevel configured, LogLevel expected)
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException("模型校验失败", new List<ValidationResult>
        {
            new ValidationResult("用户名不能为空", new[] { "UserName" })
        })
        {
            LogLevel = configured
        };

        exception.Log(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(expected, entry.Level);
    }

    /// <summary>
    /// 构造后补充的错误明细在记录日志时同样会被输出
    /// </summary>
    /// <remarks>
    /// 直接对构造时传入的集合追加元素也能生效，说明异常没有做防御性拷贝——
    /// 这是 <see cref="IHasValidationErrors"/> 允许集合可写的直接后果，属于契约的一部分。
    /// </remarks>
    [Fact]
    public void Log_ReflectsErrorsAddedAfterConstruction()
    {
        var logger = new RecordingLogger();
        IList<ValidationResult> errors = new List<ValidationResult>();
        var exception = new XiHanValidationException("模型校验失败", errors);

        errors.Add(new ValidationResult("用户名不能为空", new[] { "UserName" }));
        exception.ValidationErrors.Add(new ValidationResult("邮箱格式不正确", new[] { "Email" }));

        exception.Log(logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("存在 2 个验证错误：", entry.Message);
        Assert.Contains("用户名不能为空 (UserName)", entry.Message);
        Assert.Contains("邮箱格式不正确 (Email)", entry.Message);
    }

    /// <summary>
    /// 没有任何错误明细时不产生日志
    /// </summary>
    /// <remarks>
    /// 断言按 Log 方法自身声明的语义写：方法开头的空集合短路分支表明「无明细即不落日志」是设计意图。
    /// 这条用例最初是红的——Log 里的 IsNullOrEmpty 绑到了 GenericExtensions 的无约束泛型重载，
    /// 该重载当时对任何非 null 非字符串对象一律返回 false，空集合短路根本不生效，
    /// 于是输出一条「存在 0 个验证错误：」的空壳告警。修法是补齐泛型重载的集合分支
    /// （见 Utils/Extensions/GenericExtensions.cs），本用例即该缺陷的回归防线。
    /// </remarks>
    [Fact]
    public void Log_WhenNoValidationErrors_WritesNothing()
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException("模型校验失败");

        exception.Log(logger);

        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// 经框架统一异常日志入口分发时会额外输出自身的验证明细
    /// </summary>
    /// <remarks>
    /// 这条用例验证的是本异常与框架日志管线的对接：LogException 通过 IHasLogLevel 取到警告级别，
    /// 再通过 IExceptionWithSelfLogging 回调 Log 把明细补充出来。只断言「明细出现过」与「级别一致」，
    /// 不锁死 LogException 内部写了几条，避免把 Core 的实现细节固化进本项目的测试。
    /// </remarks>
    [Fact]
    public void LogException_DispatchesSelfLoggingWithValidationDetails()
    {
        var logger = new RecordingLogger();
        var exception = new XiHanValidationException("模型校验失败", new List<ValidationResult>
        {
            new ValidationResult("用户名不能为空", new[] { "UserName" })
        });

        logger.LogException(exception);

        Assert.NotEmpty(logger.Entries);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("存在 1 个验证错误：", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("用户名不能为空 (UserName)", StringComparison.Ordinal));
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
    }

    /// <summary>
    /// 抛出后可按框架异常基类捕获，且捕获到的实例仍带完整验证明细
    /// </summary>
    [Fact]
    public void Throw_IsCatchableAsXiHanExceptionAndKeepsErrors()
    {
        XiHanException? caught = null;

        try
        {
            throw new XiHanValidationException("模型校验失败", new List<ValidationResult>
            {
                new ValidationResult("用户名不能为空", new[] { "UserName" })
            });
        }
        catch (XiHanException thrown)
        {
            caught = thrown;
        }

        Assert.NotNull(caught);
        Assert.NotNull(caught!.StackTrace);

        var validationException = Assert.IsType<XiHanValidationException>(caught);
        var error = Assert.Single(validationException.ValidationErrors);
        Assert.Equal("用户名不能为空", error.ErrorMessage);
    }

    /// <summary>
    /// 类型只公开五个构造函数，不存在隐藏的其他入口
    /// </summary>
    /// <remarks>
    /// 五个重载覆盖「空 / 消息 / 明细 / 消息+明细 / 消息+内部异常」，
    /// 缺少「明细+内部异常」组合是刻意的取舍，这里把重载集合固定下来，防止后续无声增删。
    /// </remarks>
    [Fact]
    public void Type_ExposesFivePublicConstructors()
    {
        var constructors = typeof(XiHanValidationException)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(5, constructors.Length);

        var signatures = constructors
            .Select(constructor => string.Join(",", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "",
                "IList`1",
                "String",
                "String,Exception",
                "String,IList`1"
            },
            signatures);
    }
}
