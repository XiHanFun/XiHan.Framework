// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 知识库流水线共用的错误构造与嵌入调用包装。
/// </summary>
internal static class KnowledgeErrors
{
    /// <summary>
    /// 没有 <c>ai.knowledge</c> Provider。
    /// </summary>
    public static Error NoStore()
        => Error.Unavailable(AIErrors.KnowledgeUnavailable, AIMessages.KnowledgeUnavailable, MessageArguments.Of(("reason", "no provider for 'ai.knowledge' is configured")));

    /// <summary>
    /// 维度不一致。
    /// </summary>
    public static Error Dimensions(int actual, int expected)
        => Error.Validation(AIErrors.DimensionsMismatch, AIMessages.KnowledgeDimensions, MessageArguments.Of(("actual", actual), ("expected", expected)));

    /// <summary>
    /// 生成嵌入，把传输异常映射成 <see cref="AIErrors.ModelUnavailable"/> / <see cref="AIErrors.ModelTimeout"/>。
    /// </summary>
    public static async ValueTask<Result<GeneratedEmbeddings<Embedding<float>>>> EmbedAsync(IEmbeddingGenerator<string, Embedding<float>> generator, IReadOnlyList<string> inputs, string provider, CancellationToken cancellationToken)
    {
        try
        {
            return await generator.GenerateAsync(inputs, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Error(AIErrors.ModelTimeout, AIMessages.ModelTimeout, MessageArguments.Of(("provider", provider)));
        }
        catch (TimeoutException)
        {
            return new Error(AIErrors.ModelTimeout, AIMessages.ModelTimeout, MessageArguments.Of(("provider", provider)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error.Unavailable(AIErrors.ModelUnavailable, AIMessages.ModelUnavailable, MessageArguments.Of(("provider", provider), ("reason", exception.Message)));
        }
    }
}
