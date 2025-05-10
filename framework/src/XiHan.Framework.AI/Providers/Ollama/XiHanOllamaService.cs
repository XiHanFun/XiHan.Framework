#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOllamaService
// Guid:d4584ab9-9936-492d-be25-222fe3931676
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/2 2:00:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.Ollama;

/// <summary>
/// 基于本地 Ollama 的曦寒 AI 服务
/// </summary>
public class XiHanOllamaService : IXiHanAIService
{
    public string ProviderName => throw new NotImplementedException();

    public Task<ChatResult> ChatAsync(string message, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatStreamingResult> ChatStreamingAsync(string message, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SwitchModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
