#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVideoProcessingService
// Guid:a3b4c5d6-e7f8-4f9a-b0c1-d2e3f4a5b6c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.VirtualFileSystem.Processing.Models;

namespace XiHan.Framework.VirtualFileSystem.Processing;

/// <summary>
/// 视频处理服务接口
/// </summary>
public interface IVideoProcessingService
{
    /// <summary>
    /// 视频转码
    /// </summary>
    /// <param name="request">转码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<VideoProcessingResult> TranscodeAsync(VideoTranscodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 提取视频封面
    /// </summary>
    /// <param name="request">封面提取请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<VideoProcessingResult> ExtractThumbnailAsync(VideoThumbnailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成HLS切片
    /// </summary>
    /// <param name="request">HLS请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<VideoProcessingResult> GenerateHlsAsync(HlsGenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取视频信息
    /// </summary>
    /// <param name="videoPath">视频路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>视频信息</returns>
    Task<VideoInfo> GetVideoInfoAsync(string videoPath, CancellationToken cancellationToken = default);
}
