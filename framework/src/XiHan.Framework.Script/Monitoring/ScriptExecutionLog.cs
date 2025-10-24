#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptExecutionLog
// Guid:09baa14c-f27f-40dc-9094-ef5a16ea1c6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/1 11:06:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本执行日志
/// </summary>
public class ScriptExecutionLog
{
    /// <summary>
    /// 日志唯一标识
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 执行时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 执行时间(毫秒)
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 编译时间(毫秒)
    /// </summary>
    public long CompilationTimeMs { get; set; }

    /// <summary>
    /// 内存使用量(字节)
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 脚本代码
    /// </summary>
    public string? ScriptCode { get; set; }

    /// <summary>
    /// 脚本路径
    /// </summary>
    public string? ScriptPath { get; set; }

    /// <summary>
    /// 是否来自缓存
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; set; }
}
