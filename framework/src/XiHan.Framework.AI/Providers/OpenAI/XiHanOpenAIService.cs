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
using XiHan.Framework.Http.Polly;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.AI.Providers.OpenAI;

/// <summary>
/// 基于远程 OpenAI 的曦寒 AI 服务
/// </summary>
public class XiHanOpenAIService : IXiHanAIService
{
    private readonly HttpGroupEnum _remoteHttpGroup;
    private readonly IHttpPollyService _httpPollyService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpPollyService"></param>
    public XiHanOpenAIService(IHttpPollyService httpPollyService)
    {
        _remoteHttpGroup = HttpGroupEnum.Remote;
        _httpPollyService = httpPollyService;
    }

    /// <summary>
    /// 函数调用任务
    /// </summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="parameters">函数参数（JSON 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>函数执行结果</returns>
    public async Task<FunctionResult> CallFunctionAsync(string functionName, string parameters, CancellationToken cancellationToken = default)
    {
        var functionResult = await _httpPollyService.PostAsync<FunctionResult, StringContent>(_remoteHttpGroup, $"/functions/{functionName}", new StringContent(parameters), null, cancellationToken);
        return functionResult ?? throw new InvalidOperationException("Failed to deserialize function result.");
    }

    /// <summary>
    /// 获取函数结果
    /// </summary>
    /// <param name="functionId">函数执行 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>函数返回结果</returns>
    public async Task<FunctionResult?> GetFunctionResultAsync(string functionId, CancellationToken cancellationToken = default)
    {
        var functionResult = await _httpPollyService.GetAsync<FunctionResult>(_remoteHttpGroup, $"/functions/results/{functionId}", null, cancellationToken);
        return functionResult;
    }

    /// <summary>
    /// 注释处理任务
    /// </summary>
    /// <param name="input">需要注释的内容</param>
    /// <param name="options">注释处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>注释处理结果</returns>
    public async Task<AnnotationResult> ProcessAnnotateAsync(string input, AnnotationProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestContent = new { Input = input, Options = options };
        var annotationResult = await _httpPollyService.PostAsync<AnnotationResult, object>(_remoteHttpGroup, "/annotate", requestContent, null, cancellationToken);
        return annotationResult ?? throw new InvalidOperationException("Failed to deserialize annotation result.");
    }

    /// <summary>
    /// 音频处理任务
    /// </summary>
    /// <param name="audioStream">音频数据流</param>
    /// <param name="options">音频处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频处理结果</returns>
    public async Task<AudioResult> ProcessAudioAsync(Stream audioStream, AudioProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(audioStream), "file", "audio.wav" },
            { new StringContent(options?.SerializeTo() ?? string.Empty), "options" }
        };
        var audioResult = await _httpPollyService.PostAsync<AudioResult, object>(_remoteHttpGroup, "/audio", content, null, cancellationToken);
        return audioResult ?? throw new InvalidOperationException("Failed to deserialize audio result.");
    }

    /// <summary>
    /// 二进制流处理任务
    /// </summary>
    /// <param name="binaryStream">二进制数据流</param>
    /// <param name="options">二进制处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理后的二进制流</returns>
    public async Task<Stream> ProcessBinaryAsync(Stream binaryStream, BinaryProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(binaryStream), "file", "binary.bin" },
            { new StringContent(options?.SerializeTo() ?? string.Empty), "options" }
        };
        var streamResult = await _httpPollyService.PostAsync<Stream, MultipartFormDataContent>(_remoteHttpGroup, "/binary", content, null, cancellationToken);
        return streamResult ?? throw new InvalidOperationException("Failed to deserialize binary stream.");
    }

    /// <summary>
    /// 文件处理任务
    /// </summary>
    /// <param name="fileStream">文件数据流</param>
    /// <param name="options">文件处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件处理结果</returns>
    public async Task<FileResult> ProcessFileAsync(Stream fileStream, FileProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(fileStream), "file", "file.dat" },
            { new StringContent(options?.SerializeTo() ?? string.Empty), "options" }
        };
        var fileResult = await _httpPollyService.PostAsync<FileResult, MultipartFormDataContent>(_remoteHttpGroup, "/file", content, null, cancellationToken);
        return fileResult ?? throw new InvalidOperationException("Failed to deserialize file result.");
    }

    /// <summary>
    /// 图片处理任务
    /// </summary>
    /// <param name="imageStream">图片数据流</param>
    /// <param name="options">图片处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图片处理结果</returns>
    public async Task<ImageResult> ProcessImageAsync(Stream imageStream, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(imageStream), "file", "image.png" },
            { new StringContent(options?.SerializeTo() ?? string.Empty), "options" }
        };
        var imageResult = await _httpPollyService.PostAsync<ImageResult, MultipartFormDataContent>(_remoteHttpGroup, "/image", content, null, cancellationToken);
        return imageResult ?? throw new InvalidOperationException("Failed to deserialize image result.");
    }

    /// <summary>
    /// 文本处理任务
    /// </summary>
    /// <param name="input">输入文本</param>
    /// <param name="options">文本处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果文本</returns>
    public async Task<TextResult> ProcessTextAsync(string input, TextProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestContent = new { Input = input, Options = options };
        var textResult = await _httpPollyService.PostAsync<TextResult, object>(_remoteHttpGroup, "/text", requestContent, null, cancellationToken);
        return textResult ?? throw new InvalidOperationException("Failed to deserialize text result.");
    }

    /// <summary>
    /// 视频处理任务
    /// </summary>
    /// <param name="videoStream">视频数据流</param>
    /// <param name="options">视频处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>视频处理结果</returns>
    public async Task<VideoResult> ProcessVideoAsync(Stream videoStream, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(videoStream), "file", "video.mp4" },
            { new StringContent(options?.SerializeTo() ?? string.Empty), "options" }
        };
        var videoResult = await _httpPollyService.PostAsync<VideoResult, MultipartFormDataContent>(_remoteHttpGroup, "/video", content, null, cancellationToken);
        return videoResult ?? throw new InvalidOperationException("Failed to deserialize video result.");
    }
}
