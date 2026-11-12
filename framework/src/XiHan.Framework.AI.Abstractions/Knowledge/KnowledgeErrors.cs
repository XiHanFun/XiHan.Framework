// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 知识库流水线共用的错误构造与嵌入调用包装
/// </summary>
internal static class KnowledgeErrors
{
    /// <summary>
    /// 一次摄入或检索用的集合
    /// </summary>
    /// <remarks>
    /// 请求指明了就用它；没有指明时按当前调用的租户取 <see cref="KnowledgeOptions.DefaultCollectionFor"/>，派生不出合法的名字（租户 ID 太长）时以校验错误结束（ADR-0210）
    /// </remarks>
    public static Result<string> CollectionFor(string? requested, KnowledgeOptions options, IInvocationAccessor? invocation)
    {
        if (requested is not null)
        {
            return requested;
        }

        // 不在调用内（后台任务直接用）按不带租户处理
        var tenant = invocation is not null && invocation.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant?.Id : null;
        return options.DefaultCollectionFor(tenant).TryGetValue(out var collection)
            ? collection
            : Error.Validation(AIErrors.RequestInvalid, AIMessages.RequestInvalid, MessageArguments.Of(("reason", $"the default collection for tenant '{tenant}' would exceed 64 characters; name the collection explicitly")));
    }

    /// <summary>
    /// 没有 <c>ai.knowledge</c> Provider
    /// </summary>
    public static Error NoStore()
        => Error.Unavailable(AIErrors.KnowledgeUnavailable, AIMessages.KnowledgeUnavailable, MessageArguments.Of(("reason", "no provider for 'ai.knowledge' is configured")));

    /// <summary>
    /// 维度不一致
    /// </summary>
    public static Error Dimensions(int actual, int expected)
        => Error.Validation(AIErrors.DimensionsMismatch, AIMessages.KnowledgeDimensions, MessageArguments.Of(("actual", actual), ("expected", expected)));

    /// <summary>
    /// 解析嵌入 Provider 并生成嵌入，把传输异常映射成 <see cref="AIErrors.ModelUnavailable"/> / <see cref="AIErrors.ModelTimeout"/>
    /// </summary>
    /// <remarks>
    /// 一次嵌入批调用一个 Provider 超时令牌（<see cref="AIProviderOptions.Timeout"/>，按注入的时间源计时），超时与取消按令牌归因（RFC-0022「超时与取消」）：
    /// 超时令牌已触发即 <see cref="AIErrors.ModelTimeout"/>，不论调用方是否也取消、生成器是抛出还是正常返回；只有调用方取消时抛出 <see cref="OperationCanceledException"/>
    /// </remarks>
    /// <param name="providers">Provider 目录</param>
    /// <param name="provider">Provider 名（调用方已按请求、知识库配置与目录的默认名取定）</param>
    /// <param name="inputs">输入文本</param>
    /// <param name="timeProvider">时间源</param>
    /// <param name="logger">日志（调用失败时记下完整异常）</param>
    /// <param name="cancellationToken">调用方令牌</param>
    public static async ValueTask<Result<GeneratedEmbeddings<Embedding<float>>>> EmbedAsync(IAIProviderCatalog providers, string provider, IReadOnlyList<string> inputs, TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
    {
        var generator = providers.ResolveEmbeddingGenerator(provider);
        if (!generator.TryGetValue(out var embeddings))
        {
            return generator.Error!;
        }

        // 时限取同一个 Provider 的配置：目录解析得出生成器就查得到它，查不到是目录前后不一致，按未配置报告而不是换个缺省时限
        if (!providers.Find(provider).TryGetValue(out var settings))
        {
            return AIProviderCatalog.NotFound(provider);
        }

        using var timeout = new ProviderTimeout(settings.Timeout, timeProvider, cancellationToken);
        GeneratedEmbeddings<Embedding<float>> generated;
        try
        {
            generated = await embeddings.GenerateAsync(inputs, cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (timeout.IsTimeout(exception))
        {
            return Timeout(provider);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ModelErrors.Unavailable(logger, provider, exception);
        }

        // 生成器接住取消、正常返回的结果可能条数不全：超时令牌已触发即超时，只有调用方取消时抛出取消
        return timeout.ReturnedAfterTimeout() ? Timeout(provider) : generated;
    }

    /// <summary>
    /// 嵌入调用超时
    /// </summary>
    private static Error Timeout(string provider)
        => new(AIErrors.ModelTimeout, AIMessages.ModelTimeout, MessageArguments.Of(("provider", provider)));
}
