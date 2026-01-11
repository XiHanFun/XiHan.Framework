#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VideoProcessingModels
// Guid:b4c5d6e7-f8a9-4f0b-c1d2-e3f4a5b6c7d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:41:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Processing.Models;

/// <summary>
/// 视频转码请求
/// </summary>
public class VideoTranscodeRequest
{
    /// <summary>
    /// 源视频路径
    /// </summary>
    public string SourcePath { get; set; }

    /// <summary>
    /// 输出路径
    /// </summary>
    public string OutputPath { get; set; }

    /// <summary>
    /// 目标格式
    /// </summary>
    public VideoFormat TargetFormat { get; set; } = VideoFormat.Mp4;

    /// <summary>
    /// 视频编码器
    /// </summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>
    /// 音频编码器
    /// </summary>
    public string AudioCodec { get; set; } = "aac";

    /// <summary>
    /// 视频比特率（如：2000k）
    /// </summary>
    public string? VideoBitrate { get; set; }

    /// <summary>
    /// 音频比特率（如：128k）
    /// </summary>
    public string? AudioBitrate { get; set; }

    /// <summary>
    /// 分辨率（如：1920x1080）
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// 帧率
    /// </summary>
    public int? FrameRate { get; set; }

    /// <summary>
    /// 进度回调
    /// </summary>
    public Action<double>? ProgressCallback { get; set; }
}

/// <summary>
/// 视频封面提取请求
/// </summary>
public class VideoThumbnailRequest
{
    /// <summary>
    /// 源视频路径
    /// </summary>
    public string SourcePath { get; set; }

    /// <summary>
    /// 输出路径
    /// </summary>
    public string OutputPath { get; set; }

    /// <summary>
    /// 提取时间点（秒）
    /// </summary>
    public double TimePosition { get; set; } = 1.0;

    /// <summary>
    /// 输出宽度
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// 输出高度
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// 输出格式
    /// </summary>
    public ImageFormat OutputFormat { get; set; } = ImageFormat.Jpeg;
}

/// <summary>
/// HLS生成请求
/// </summary>
public class HlsGenerationRequest
{
    /// <summary>
    /// 源视频路径
    /// </summary>
    public string SourcePath { get; set; }

    /// <summary>
    /// 输出目录
    /// </summary>
    public string OutputDirectory { get; set; }

    /// <summary>
    /// 切片时长（秒）
    /// </summary>
    public int SegmentDuration { get; set; } = 10;

    /// <summary>
    /// 输出文件名前缀
    /// </summary>
    public string FilePrefix { get; set; } = "segment";

    /// <summary>
    /// 是否生成多码率
    /// </summary>
    public bool GenerateMultipleBitrates { get; set; } = false;

    /// <summary>
    /// 码率配置列表
    /// </summary>
    public List<BitrateConfig>? BitrateConfigs { get; set; }
}

/// <summary>
/// 码率配置
/// </summary>
public class BitrateConfig
{
    /// <summary>
    /// 分辨率
    /// </summary>
    public string Resolution { get; set; } = "1920x1080";

    /// <summary>
    /// 视频比特率
    /// </summary>
    public string VideoBitrate { get; set; } = "2000k";

    /// <summary>
    /// 音频比特率
    /// </summary>
    public string AudioBitrate { get; set; } = "128k";
}

/// <summary>
/// 视频处理结果
/// </summary>
public class VideoProcessingResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 输出路径
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 时长（秒）
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 处理耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 额外信息
    /// </summary>
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>
/// 视频信息
/// </summary>
public class VideoInfo
{
    /// <summary>
    /// 时长（秒）
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 帧率
    /// </summary>
    public double FrameRate { get; set; }

    /// <summary>
    /// 视频编码
    /// </summary>
    public string? VideoCodec { get; set; }

    /// <summary>
    /// 音频编码
    /// </summary>
    public string? AudioCodec { get; set; }

    /// <summary>
    /// 比特率
    /// </summary>
    public long Bitrate { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 格式
    /// </summary>
    public string? Format { get; set; }
}

/// <summary>
/// 视频格式
/// </summary>
public enum VideoFormat
{
    /// <summary>
    /// MP4
    /// </summary>
    Mp4,

    /// <summary>
    /// AVI
    /// </summary>
    Avi,

    /// <summary>
    /// MOV
    /// </summary>
    Mov,

    /// <summary>
    /// MKV
    /// </summary>
    Mkv,

    /// <summary>
    /// WebM
    /// </summary>
    WebM,

    /// <summary>
    /// FLV
    /// </summary>
    Flv
}
