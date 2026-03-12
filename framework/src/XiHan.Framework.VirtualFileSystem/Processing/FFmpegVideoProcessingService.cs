#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FFmpegVideoProcessingService
// Guid:c5d6e7f8-a9b0-4f1c-d2e3-f4a5b6c7d8e9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.VirtualFileSystem.Processing.Models;

namespace XiHan.Framework.VirtualFileSystem.Processing;

/// <summary>
/// 基于 FFmpeg 的视频处理服务。
/// 当前版本不强依赖 FFmpeg 环境，默认返回失败结果而不是抛出异常。
/// </summary>
public class FFmpegVideoProcessingService : IVideoProcessingService
{
    private readonly string _ffmpegPath;

    public FFmpegVideoProcessingService(string? ffmpegPath = null)
    {
        _ffmpegPath = ffmpegPath ?? "ffmpeg";
    }

    public Task<VideoProcessingResult> TranscodeAsync(VideoTranscodeRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(TranscodeAsync)));
    }

    public Task<VideoProcessingResult> ExtractThumbnailAsync(VideoThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(ExtractThumbnailAsync)));
    }

    public Task<VideoProcessingResult> GenerateHlsAsync(HlsGenerationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(GenerateHlsAsync)));
    }

    public Task<VideoInfo> GetVideoInfoAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new VideoInfo());
    }

    private VideoProcessingResult CreateNotSupportedResult(string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        stopwatch.Stop();

        return new VideoProcessingResult
        {
            Success = false,
            ErrorMessage = $"{operationName}: FFmpeg 处理能力未启用。请安装 FFmpeg 并接入 {_ffmpegPath}。",
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
