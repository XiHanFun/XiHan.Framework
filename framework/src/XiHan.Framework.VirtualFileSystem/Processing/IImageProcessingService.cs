#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IImageProcessingService
// Guid:d0e1f2a3-b4c5-4f6d-e7f8-a9b0c1d2e3f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.VirtualFileSystem.Processing.Models;

namespace XiHan.Framework.VirtualFileSystem.Processing;

/// <summary>
/// 图片处理服务接口
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// 生成缩略图
    /// </summary>
    /// <param name="request">缩略图请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> GenerateThumbnailAsync(ThumbnailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 压缩图片
    /// </summary>
    /// <param name="request">压缩请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> CompressAsync(ImageCompressionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加水印
    /// </summary>
    /// <param name="request">水印请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> AddWatermarkAsync(WatermarkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 裁剪图片
    /// </summary>
    /// <param name="request">裁剪请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> CropAsync(ImageCropRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 调整图片大小
    /// </summary>
    /// <param name="request">调整请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 旋转图片
    /// </summary>
    /// <param name="request">旋转请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessingResult> RotateAsync(ImageRotateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取图片信息
    /// </summary>
    /// <param name="imageStream">图片流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图片信息</returns>
    Task<ImageInfo> GetImageInfoAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
