#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ImageSharpProcessingService
// Guid:f2a3b4c5-d6e7-4f8a-b9c0-d1e2f3a4b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.VirtualFileSystem.Processing.Models;

namespace XiHan.Framework.VirtualFileSystem.Processing;

/// <summary>
/// 基于 SixLabors.ImageSharp 的图片处理服务。
/// 当前版本不强依赖 ImageSharp 包，默认返回失败结果而不是抛出异常。
/// </summary>
public class ImageSharpProcessingService : IImageProcessingService
{
    private const string NotSupportedMessage = "ImageSharp 处理能力未启用，请安装 SixLabors.ImageSharp 并接入具体实现。";

    public Task<ImageProcessingResult> GenerateThumbnailAsync(ThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(GenerateThumbnailAsync)));
    }

    public Task<ImageProcessingResult> CompressAsync(ImageCompressionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(CompressAsync)));
    }

    public Task<ImageProcessingResult> AddWatermarkAsync(WatermarkRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(AddWatermarkAsync)));
    }

    public Task<ImageProcessingResult> CropAsync(ImageCropRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(CropAsync)));
    }

    public Task<ImageProcessingResult> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(ResizeAsync)));
    }

    public Task<ImageProcessingResult> RotateAsync(ImageRotateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateNotSupportedResult(nameof(RotateAsync)));
    }

    public Task<ImageInfo> GetImageInfoAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ImageInfo());
    }

    private static ImageProcessingResult CreateNotSupportedResult(string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        stopwatch.Stop();

        return new ImageProcessingResult
        {
            Success = false,
            ErrorMessage = $"{operationName}: {NotSupportedMessage}",
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
