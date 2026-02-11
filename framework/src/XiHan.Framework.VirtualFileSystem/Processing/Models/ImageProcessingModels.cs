#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ImageProcessingModels
// Guid:e1f2a3b4-c5d6-4f7e-a8b9-c0d1e2f3a4b5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:31:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Processing.Models;

/// <summary>
/// 缩略图请求
/// </summary>
public class ThumbnailRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; } = 150;

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; } = 150;

    /// <summary>
    /// 缩略图模式
    /// </summary>
    public ThumbnailMode Mode { get; set; } = ThumbnailMode.Fit;

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Original;

    /// <summary>
    /// 质量（1-100）
    /// </summary>
    public int Quality { get; set; } = 85;
}

/// <summary>
/// 图片压缩请求
/// </summary>
public class ImageCompressionRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 目标质量（1-100）
    /// </summary>
    public int Quality { get; set; } = 75;

    /// <summary>
    /// 目标文件大小（KB，0表示不限制）
    /// </summary>
    public int TargetSizeKb { get; set; } = 0;

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Original;

    /// <summary>
    /// 是否保留元数据
    /// </summary>
    public bool PreserveMetadata { get; set; } = false;
}

/// <summary>
/// 水印请求
/// </summary>
public class WatermarkRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 水印类型
    /// </summary>
    public WatermarkType WatermarkType { get; set; } = WatermarkType.Text;

    /// <summary>
    /// 水印文本（文本水印时使用）
    /// </summary>
    public string? WatermarkText { get; set; }

    /// <summary>
    /// 水印图片流（图片水印时使用）
    /// </summary>
    public Stream? WatermarkImageStream { get; set; }

    /// <summary>
    /// 水印位置
    /// </summary>
    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    /// <summary>
    /// 透明度（0-100）
    /// </summary>
    public int Opacity { get; set; } = 50;

    /// <summary>
    /// 字体大小（文本水印）
    /// </summary>
    public int FontSize { get; set; } = 20;

    /// <summary>
    /// 字体颜色（文本水印，格式：#RRGGBB）
    /// </summary>
    public string FontColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// 边距
    /// </summary>
    public int Margin { get; set; } = 10;
}

/// <summary>
/// 图片裁剪请求
/// </summary>
public class ImageCropRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 起始X坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 起始Y坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Original;
}

/// <summary>
/// 图片调整大小请求
/// </summary>
public class ImageResizeRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 目标宽度（0表示自动）
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 目标高度（0表示自动）
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 调整模式
    /// </summary>
    public ResizeMode Mode { get; set; } = ResizeMode.Fit;

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Original;

    /// <summary>
    /// 质量
    /// </summary>
    public int Quality { get; set; } = 85;
}

/// <summary>
/// 图片旋转请求
/// </summary>
public class ImageRotateRequest
{
    /// <summary>
    /// 源图片流
    /// </summary>
    public Stream SourceStream { get; set; } = Stream.Null;

    /// <summary>
    /// 旋转角度（0, 90, 180, 270）
    /// </summary>
    public int Angle { get; set; }

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Original;
}

/// <summary>
/// 图片处理结果
/// </summary>
public class ImageProcessingResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 输出流
    /// </summary>
    public Stream? OutputStream { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 格式
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 处理耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 图片信息
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 格式
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// DPI
    /// </summary>
    public double? Dpi { get; set; }

    /// <summary>
    /// 颜色深度
    /// </summary>
    public int? ColorDepth { get; set; }

    /// <summary>
    /// 是否有透明通道
    /// </summary>
    public bool HasAlpha { get; set; }
}

/// <summary>
/// 缩略图模式
/// </summary>
public enum ThumbnailMode
{
    /// <summary>
    /// 适应（保持比例，可能有留白）
    /// </summary>
    Fit,

    /// <summary>
    /// 填充（保持比例，裁剪多余部分）
    /// </summary>
    Fill,

    /// <summary>
    /// 拉伸（不保持比例）
    /// </summary>
    Stretch
}

/// <summary>
/// 图片格式
/// </summary>
public enum ImageFormat
{
    /// <summary>
    /// 原始格式
    /// </summary>
    Original,

    /// <summary>
    /// JPEG
    /// </summary>
    Jpeg,

    /// <summary>
    /// PNG
    /// </summary>
    Png,

    /// <summary>
    /// WebP
    /// </summary>
    WebP,

    /// <summary>
    /// GIF
    /// </summary>
    Gif
}

/// <summary>
/// 水印类型
/// </summary>
public enum WatermarkType
{
    /// <summary>
    /// 文本水印
    /// </summary>
    Text,

    /// <summary>
    /// 图片水印
    /// </summary>
    Image
}

/// <summary>
/// 水印位置
/// </summary>
public enum WatermarkPosition
{
    /// <summary>
    /// 左上角
    /// </summary>
    TopLeft,

    /// <summary>
    /// 顶部居中
    /// </summary>
    TopCenter,

    /// <summary>
    /// 右上角
    /// </summary>
    TopRight,

    /// <summary>
    /// 左侧居中
    /// </summary>
    MiddleLeft,

    /// <summary>
    /// 正中央
    /// </summary>
    Center,

    /// <summary>
    /// 右侧居中
    /// </summary>
    MiddleRight,

    /// <summary>
    /// 左下角
    /// </summary>
    BottomLeft,

    /// <summary>
    /// 底部居中
    /// </summary>
    BottomCenter,

    /// <summary>
    /// 右下角
    /// </summary>
    BottomRight
}

/// <summary>
/// 调整大小模式
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// 适应（保持比例）
    /// </summary>
    Fit,

    /// <summary>
    /// 填充（裁剪）
    /// </summary>
    Fill,

    /// <summary>
    /// 拉伸
    /// </summary>
    Stretch
}
