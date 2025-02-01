﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDeepSeekService
// Guid:a154756a-aebc-43bf-bea7-3945c888c3ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/2 2:00:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options.Processing;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.DeepSeek;

/// <summary>
/// 基于 DeepSeek 的曦寒 AI 服务
/// </summary>
public class XiHanDeepSeekService : IXiHanAIService, IXiHanAILocalModelService
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
