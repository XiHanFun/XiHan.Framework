#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIService
// Guid:84b11bb2-d82e-4a03-ad3e-7c524cd715c5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// 曦寒AI服务接口
/// </summary>
public interface IXiHanAiService
{
    /// <summary>
    /// 服务提供商名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 异步聊天接口
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="options">聊天选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天结果</returns>
    Task<XiHanChatResult> ChatAsync(string message, XiHanChatOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成文本嵌入向量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入向量</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 切换模型
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切换结果</returns>
    Task<bool> SwitchModelAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式聊天接口
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="options">聊天选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式聊天结果</returns>
    IAsyncEnumerable<XiHanChatStreamingResult> ChatStreamingAsync(string message, XiHanChatOptions? options = null, CancellationToken cancellationToken = default);
}
