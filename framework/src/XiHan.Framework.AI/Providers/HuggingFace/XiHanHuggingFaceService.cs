#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHuggingFaceService
// Guid:6d8f51c9-f1c4-4606-b413-730739802414
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/4 18:29:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Options.Processing;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Providers.HuggingFace;

/// <summary>
/// 基于本地 HuggingFace 的曦寒 AI 服务
/// </summary>
public class XiHanHuggingFaceService : IXiHanAIService
{
    private readonly IChatCompletionService _huggingFaceChatService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanHuggingFaceService(
        IKeyedServiceProvider keyedServiceProvider,
        IOptions<XiHanHuggingFaceOptions> huggingFaceOptions)
    {
        _huggingFaceChatService = keyedServiceProvider.GetRequiredKeyedService<IChatCompletionService>(huggingFaceOptions.Value.ServiceId);
    }

    /// <summary>
    /// 函数调用任务
    /// </summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="parameters">函数参数（JSON 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>函数执行结果</returns>
    public Task<FunctionResult> CallFunctionAsync(string functionName, string parameters, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取函数结果
    /// </summary>
    /// <param name="functionId">函数执行 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>函数返回结果</returns>
    public Task<FunctionResult?> GetFunctionResultAsync(string functionId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 注释处理任务
    /// </summary>
    /// <param name="input">需要注释的内容</param>
    /// <param name="options">注释处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>注释处理结果</returns>
    public Task<AnnotationResult> ProcessAnnotateAsync(string input, AnnotationProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 音频处理任务
    /// </summary>
    /// <param name="audioStream">音频数据流</param>
    /// <param name="options">音频处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频处理结果</returns>
    public Task<AudioResult> ProcessAudioAsync(Stream audioStream, AudioProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 二进制流处理任务
    /// </summary>
    /// <param name="binaryStream">二进制数据流</param>
    /// <param name="options">二进制处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理后的二进制流</returns>
    public Task<Stream> ProcessBinaryAsync(Stream binaryStream, BinaryProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 文件处理任务
    /// </summary>
    /// <param name="fileStream">文件数据流</param>
    /// <param name="options">文件处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件处理结果</returns>
    public Task<FileResult> ProcessFileAsync(Stream fileStream, FileProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 图片处理任务
    /// </summary>
    /// <param name="imageStream">图片数据流</param>
    /// <param name="options">图片处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图片处理结果</returns>
    public Task<ImageResult> ProcessImageAsync(Stream imageStream, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 文本处理任务
    /// </summary>
    /// <param name="input">输入文本</param>
    /// <param name="options">文本处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果文本</returns>
    public Task<TextResult> ProcessTextAsync(string input, TextProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 视频处理任务
    /// </summary>
    /// <param name="videoStream">视频数据流</param>
    /// <param name="options">视频处理选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>视频处理结果</returns>
    public Task<VideoResult> ProcessVideoAsync(Stream videoStream, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
