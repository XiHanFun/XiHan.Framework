// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ClientModel;
using System.Net.Sockets;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 嵌入模型调用的统一异常翻译。
/// </summary>
/// <remarks>
/// <para>
/// 嵌入走 OpenAI 兼容端点，SDK 失败时抛出的消息形如「Service request failed. Status: 404 (Not Found)」，
/// 既不说明是哪个环节，也不说明是哪个提供方与模型。RAG 链路上嵌入排在向量库之前，
/// 因此这类失败最容易被误判成向量库故障。
/// </para>
/// <para>
/// 这里把「提供方侧无法服务」翻译成 <see cref="ServiceUnavailableException"/>（映射 503），
/// 并在消息中带上提供方与模型名。请求本身的问题（如内容超长导致的 400）原样抛出，
/// 那类失败重试不会好，报成 503 会掩盖真正的原因。
/// </para>
/// </remarks>
public static class EmbeddingOperation
{
    /// <summary>
    /// 执行一次嵌入调用。
    /// </summary>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="operation">嵌入调用。</param>
    /// <param name="provider">提供方名称，用于定位配置。</param>
    /// <param name="model">嵌入模型名称。</param>
    /// <returns>调用结果。</returns>
    /// <exception cref="ServiceUnavailableException">嵌入提供方不可用或配置有误。</exception>
    public static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, string? provider, string? model)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (Describe(exception, provider, model) is { } message)
        {
            throw new ServiceUnavailableException(message, innerException: exception);
        }
    }

    /// <summary>
    /// 判断异常是否属于「提供方侧无法服务」，是则给出可操作的消息。
    /// </summary>
    /// <param name="exception">原始异常。</param>
    /// <param name="provider">提供方名称。</param>
    /// <param name="model">嵌入模型名称。</param>
    /// <returns>需要翻译时返回消息；应原样抛出时返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 按 HTTP 状态分类而不是匹配消息文本：SDK 版本与系统语言都会改变文案。
    /// 400 与 422 表示请求内容本身有问题（例如单片文本超出模型上下文），属于调用方缺陷，不翻译。
    /// </remarks>
    private static string? Describe(Exception exception, string? provider, string? model)
    {
        var target = $"提供方 [{provider ?? "默认"}]、模型 [{model ?? "未指定"}]";

        if (exception is ClientResultException clientResult)
        {
            return clientResult.Status switch
            {
                400 or 422 => null,
                401 or 403 => $"嵌入模型鉴权失败（{clientResult.Status}）：请检查 {target} 的 API Key。",
                404 => $"嵌入模型接口未找到（404）：请检查 {target} 的接口地址与模型名，"
                    + "OpenAI 兼容端点通常需要以 /v1 结尾。",
                408 or 429 => $"嵌入模型暂时不可用（{clientResult.Status}）：{target} 超时或触发限流，请稍后重试。",
                >= 500 => $"嵌入模型服务异常（{clientResult.Status}）：{target} 返回服务端错误。",
                _ => null
            };
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or HttpRequestException)
            {
                return $"嵌入模型不可达：请检查 {target} 的接口地址与网络连通性。";
            }
        }

        return null;
    }
}
