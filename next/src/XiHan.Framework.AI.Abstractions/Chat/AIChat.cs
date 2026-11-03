// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Chat;

/// <summary>
/// <see cref="IAIChat"/> 的实现
/// </summary>
/// <remarks>
/// 工具绑定到当前调用（没有时为本作用域新建的上下文），护栏按登记顺序检查全部消息
/// 调用 Provider 时把它的 <see cref="AIProviderOptions.Timeout"/> 按注入的 <see cref="TimeProvider"/> 落成超时令牌、与调用方令牌链接后交给它，
/// 超时与取消按令牌归因（RFC-0022「超时与取消」）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIChat : IAIChat
{
    /// <summary>
    /// 选项
    /// </summary>
    private readonly AIOptions _options;

    /// <summary>
    /// Provider 目录
    /// </summary>
    private readonly IAIProviderCatalog _providers;

    /// <summary>
    /// 提示词目录
    /// </summary>
    private readonly IAIPromptCatalog _prompts;

    /// <summary>
    /// 工具目录
    /// </summary>
    private readonly IAIToolCatalog _tools;

    /// <summary>
    /// 护栏（登记顺序）
    /// </summary>
    private readonly IReadOnlyList<IAIGuardrail> _guardrails;

    /// <summary>
    /// 当前调用
    /// </summary>
    private readonly IInvocationAccessor _invocation;

    /// <summary>
    /// 本作用域的服务
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// Provider 的超时令牌按它计时，测试推进虚拟时间即触发
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 创建
    /// </summary>
    public AIChat(AIOptions options, IAIProviderCatalog providers, IAIPromptCatalog prompts, IAIToolCatalog tools, IEnumerable<IAIGuardrail> guardrails, IInvocationAccessor invocation, TimeProvider timeProvider, IServiceProvider services)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _guardrails = (guardrails ?? throw new ArgumentNullException(nameof(guardrails))).ToList();
        _invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public async ValueTask<Result<ChatResponse>> CompleteAsync(AIChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (!prepared.TryGetValue(out var call))
        {
            return prepared.Error!;
        }

        // 时限覆盖 Provider 的整次调用，含它自己的函数调用循环：工具拿到的也是这个链接令牌
        using var timeout = new ProviderTimeout(call.Timeout, _timeProvider, cancellationToken);
        ChatResponse response;
        try
        {
            response = await call.Client.GetResponseAsync(call.Messages, call.Options, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (timeout.IsTimeout(exception))
        {
            return Timeout(call.Provider);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(call.Provider, exception);
        }

        return timeout.ReturnedAfterTimeout() ? Timeout(call.Provider) : response;
    }

    /// <inheritdoc />
    public async ValueTask<Result<IAsyncEnumerable<ChatResponseUpdate>>> StreamAsync(AIChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        return prepared.TryGetValue(out var call) ? Result.Success(StreamCoreAsync(call, cancellationToken)) : prepared.Error!;
    }

    /// <summary>
    /// 流式调用
    /// </summary>
    /// <remarks>
    /// 时限是等每个更新（含第一个）的时长：只在等 Provider 时计时，调用方处理更新的时间不算，流的总时长不限
    /// 超时以 <see cref="TimeoutException"/> 抛出（流已交给调用方，不能再返回结果），调用方取消抛出 <see cref="OperationCanceledException"/>，其余异常原样抛出
    /// </remarks>
    private async IAsyncEnumerable<ChatResponseUpdate> StreamCoreAsync(PreparedCall call, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timeout = new ProviderTimeout(call.Timeout, _timeProvider, cancellationToken, armed: false);
        var updates = call.Client.GetStreamingResponseAsync(call.Messages, call.Options, timeout.Token).GetAsyncEnumerator(timeout.Token);
        await using (updates.ConfigureAwait(false))
        {
            while (true)
            {
                bool moved;
                timeout.Arm();
                try
                {
                    moved = await updates.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not TimeoutException && timeout.IsTimeout(exception))
                {
                    throw timeout.Exceeded(call.Provider, exception);
                }
                finally
                {
                    timeout.Disarm();
                }

                // 等这一个更新期间超时令牌已触发：Provider 交出的更新或结束可能是接住取消之后的（截断的流），与抛出同一个回答
                if (timeout.ReturnedAfterTimeout())
                {
                    throw timeout.Exceeded(call.Provider, null);
                }

                if (!moved)
                {
                    yield break;
                }

                yield return updates.Current;
            }
        }
    }

    /// <summary>
    /// 提示词 → 工具 → 护栏 → Provider
    /// </summary>
    private async ValueTask<Result<PreparedCall>> PrepareAsync(AIChatRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>(request.Messages.Count + 1);
        if (request.Prompt is not null)
        {
            if (!_prompts.Find(request.Prompt).TryGetValue(out var template))
            {
                return Error.NotFound(AIErrors.PromptNotFound, AIMessages.PromptNotFound, MessageArguments.Of(("name", request.Prompt)));
            }

            var rendered = template.Render(request.PromptArguments);
            if (!rendered.TryGetValue(out var system))
            {
                return rendered.Error!;
            }

            messages.Add(new ChatMessage(ChatRole.System, system));
        }

        messages.AddRange(request.Messages);

        var options = request.Options;
        if (request.Tools.Count > 0)
        {
            // 工具绑定到当前调用：没有环境调用时用本作用域新建一个，取消令牌为本次调用的令牌
            var caller = _invocation.Current.ValueOr(App.CreateContext(_services, cancellationToken));
            var bound = _tools.Bind(caller);
            var selected = new List<AITool>(request.Tools.Count);
            foreach (var name in request.Tools)
            {
                var tool = bound.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
                if (tool is null)
                {
                    return Error.NotFound(AIErrors.ToolNotFound, AIMessages.ToolNotFound, MessageArguments.Of(("name", name)));
                }

                selected.Add(tool);
            }

            options = options?.Clone() ?? new ChatOptions();
            options.Tools = selected;
        }

        if (!request.SkipGuardrails)
        {
            foreach (var guardrail in _guardrails)
            {
                Result<GuardrailVerdict> inspected;
                try
                {
                    inspected = await guardrail.InspectAsync(messages, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    return Error.Unexpected(AIErrors.Unexpected, AIMessages.GuardrailFailed, details: MessageArguments.Of(("guardrail", guardrail.Name), ("reason", exception.Message)));
                }

                if (!inspected.TryGetValue(out var verdict))
                {
                    return Error.Unexpected(AIErrors.Unexpected, AIMessages.GuardrailFailed, details: MessageArguments.Of(("guardrail", guardrail.Name), ("reason", inspected.Error!.Message)));
                }

                if (verdict.Blocked)
                {
                    return Error.Denied(AIErrors.GuardrailBlocked, AIMessages.GuardrailBlocked, MessageArguments.Of(("guardrail", guardrail.Name), ("reason", verdict.Reason)));
                }
            }
        }

        var provider = request.Provider ?? _options.ResolveDefaultProvider() ?? string.Empty;
        var client = _providers.ResolveChatClient(request.Provider);
        if (!client.TryGetValue(out var resolved))
        {
            return client.Error!;
        }

        // 时限取同一个 Provider 的配置：目录解析得出客户端就查得到它，查不到是目录前后不一致，按未配置报告而不是换个缺省时限
        return _providers.Find(provider).TryGetValue(out var settings)
            ? new PreparedCall(resolved, messages, options, provider, settings.Timeout)
            : AIProviderCatalog.NotFound(provider);
    }

    /// <summary>
    /// 超时
    /// </summary>
    private static Error Timeout(string provider)
        => new(AIErrors.ModelTimeout, AIMessages.ModelTimeout, MessageArguments.Of(("provider", provider)));

    /// <summary>
    /// 不可达或返回错误
    /// </summary>
    private static Error Unavailable(string provider, Exception exception)
        => Error.Unavailable(AIErrors.ModelUnavailable, AIMessages.ModelUnavailable, MessageArguments.Of(("provider", provider), ("reason", exception.Message)));

    /// <summary>
    /// 准备好的调用
    /// </summary>
    private sealed record PreparedCall(IChatClient Client, List<ChatMessage> Messages, ChatOptions? Options, string Provider, TimeSpan Timeout);
}
