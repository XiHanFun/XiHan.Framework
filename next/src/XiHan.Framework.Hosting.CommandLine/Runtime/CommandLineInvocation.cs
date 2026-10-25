// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Hosting.CommandLine.Runtime;

/// <summary>
/// 命令行选项与调用上下文之间的翻译（RFC-0003 的命令行载体映射）：<c>--tenant</c>、<c>--correlation</c>、<c>--locale</c> 进 Baggage，
/// <c>--timeout</c>（秒）换算为截止时间，<c>--authorization</c>（或环境变量 <c>XIHAN_AUTHORIZATION</c>）原样进入凭据由 Authenticate 阶段解释。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineInvocation
{
    /// <summary>
    /// 凭据的环境变量；<c>--authorization</c> 优先。
    /// </summary>
    public const string AuthorizationVariable = "XIHAN_AUTHORIZATION";

    /// <summary>
    /// 被这里消费、不属于操作载荷的选项名。
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedOptions = new HashSet<string>(StringComparer.Ordinal) { "json", "data", "authorization", "tenant", "correlation", "locale", "timeout", "help" };

    /// <summary>
    /// 从命令环境构造调用上下文；选项不合法返回 <see cref="CommandLineErrors.UsageInvalid"/>。
    /// </summary>
    public static Result<InvocationContext> Create(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Option("tenant").TryGetValue(out var tenant))
        {
            baggage[BaggageKeys.Tenant] = tenant;
        }

        if (context.Option("correlation").TryGetValue(out var correlation))
        {
            baggage[BaggageKeys.Correlation] = correlation;
        }

        if (context.Option("locale").TryGetValue(out var locale) && LocaleName.IsValid(locale))
        {
            baggage[BaggageKeys.Locale] = LocaleName.Normalize(locale);
        }

        DateTimeOffset? deadline = null;
        if (context.Option("timeout").TryGetValue(out var timeout))
        {
            if (!double.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0 || double.IsInfinity(seconds))
            {
                return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeTimeoutInvalid);
            }

            deadline = (context.Services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow() + TimeSpan.FromSeconds(seconds);
        }

        var invocation = App.CreateContext(context.Services, context.CancellationToken, deadline: deadline, baggage: baggage);

        // 凭据只做格式拆分，不在这里验证；解释交给安全子系统的 Authenticate 阶段
        var authorization = context.Option("authorization").ValueOr(Environment.GetEnvironmentVariable(AuthorizationVariable) ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            if (!AuthenticationCredential.TryParse(authorization, out var credential))
            {
                return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeAuthorizationInvalid);
            }

            invocation.SetCredential(credential);
        }

        return Result.Success(invocation);
    }
}
