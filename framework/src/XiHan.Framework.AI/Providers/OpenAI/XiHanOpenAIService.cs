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

using System.Net.Http.Json;
using XiHan.Framework.AI.Options.Processing;
using XiHan.Framework.AI.Results;
using XiHan.Framework.Http.Polly;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.AI.Providers.OpenAI;

/// <summary>
/// 基于远程 OpenAI 的曦寒 AI 服务
/// </summary>
public class XiHanOpenAIService : IXiHanAIService, IXiHanAIRemoteService
{
    private readonly IHttpPollyService _httpPollyService;

    /// <summary>
    /// 网络请求组别
    /// </summary>
    public HttpGroupEnum HttpGroup { get; set; } = HttpGroupEnum.Remote;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpPollyService"></param>
    public XiHanOpenAIService(IHttpPollyService httpPollyService)
    {
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
        var response = await _httpPollyService.PostAsync(HttpGroup, $"/functions/{functionName}", new StringContent(parameters), null, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        var functionResult = resultContent.DeserializeTo<FunctionResult>();
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
        var response = await _httpPollyService.GetAsync($"/functions/results/{functionId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var resultContent = await response.Content.ReadAsStringAsync();
        return resultContent.DeserializeTo<FunctionResult>();
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
        var response = await _httpPollyService.PostAsJsonAsync("/annotate", requestContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        var annotationResult = resultContent.DeserializeTo<AnnotationResult>();
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
            { new StringContent(options.SerializeTo()), "options" }
        };
        var response = await _httpPollyService.PostAsync("/audio", content, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        var audioResult = resultContent.DeserializeTo<AudioResult>();
        return audioResult ?? throw new InvalidOperationException("Failed to deserialize audio result.");
    }

    /// <inheritdoc/>
    public async Task<Stream> ProcessBinaryAsync(Stream binaryStream, BinaryProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(binaryStream), "file", "binary.bin" },
            { new StringContent(JsonConvert.SerializeObject(options)), "options" }
        };
        var response = await _httpPollyService.PostAsync("/binary", content, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    /// <inheritdoc/>
    public async Task<FileResult> ProcessFileAsync(Stream fileStream, FileProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(fileStream), "file", "file.dat" },
            { new StringContent(JsonConvert.SerializeObject(options)), "options" }
        };
        var response = await _httpPollyService.PostAsync("/file", content, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<FileResult>(resultContent);
    }

    /// <inheritdoc/>
    public async Task<ImageResult> ProcessImageAsync(Stream imageStream, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(imageStream), "file", "image.png" },
            { new StringContent(JsonConvert.SerializeObject(options)), "options" }
        };
        var response = await _httpPollyService.PostAsync("/image", content, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<ImageResult>(resultContent);
    }

    /// <inheritdoc/>
    public async Task<TextResult> ProcessTextAsync(string input, TextProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestContent = new { Input = input, Options = options };
        var response = await _httpPollyService.PostAsJsonAsync("/text", requestContent, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        return resultContent.DeserializeTo<TextResult>();
    }

    /// <inheritdoc/>
    public async Task<VideoResult> ProcessVideoAsync(Stream videoStream, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent
        {
            { new StreamContent(videoStream), "file", "video.mp4" },
            { new StringContent(JsonConvert.SerializeObject(options)), "options" }
        };
        var response = await _httpPollyService.PostAsync("/video", content, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        var resultContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<VideoResult>(resultContent);
    }
}
