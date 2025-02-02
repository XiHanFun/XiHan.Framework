#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOpenAIService
// Guid:9bb988db-1708-4ff5-8f21-9a8f26de4ec7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 5:57:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options.Processing;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.OpenAI;

/// <summary>
/// 基于远程 OpenAI 的曦寒 AI 服务
/// </summary>
public class XiHanOpenAIService : IXiHanAIService, IXiHanAIRemoteService
{
    /// <inheritdoc/>
    public Task<FunctionResult> CallFunctionAsync(string functionName, string parameters, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<FunctionResult?> GetFunctionResultAsync(string functionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<AnnotationResult> ProcessAnnotateAsync(string input, AnnotationProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<AudioResult> ProcessAudioAsync(Stream audioStream, AudioProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<Stream> ProcessBinaryAsync(Stream binaryStream, BinaryProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<FileResult> ProcessFileAsync(Stream fileStream, FileProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<ImageResult> ProcessImageAsync(Stream imageStream, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<string> ProcessTextAsync(string input, TextProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>

    public Task<VideoResult> ProcessVideoAsync(Stream videoStream, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
