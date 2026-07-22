// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业持久化信息（存储边界的传输模型）
/// </summary>
public class BackgroundJobInfo
{
    /// <summary>
    /// 作业唯一标识
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 入队应用名称（多实例隔离；为空表示不区分）
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// 入队时的租户标识（执行时据此切换租户上下文；为空表示宿主/无租户）
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 作业名称（由参数类型解析出的稳定名）
    /// </summary>
    public string JobName { get; set; } = default!;

    /// <summary>
    /// 序列化后的作业参数（JSON）
    /// </summary>
    public string JobArgs { get; set; } = default!;

    /// <summary>
    /// 已尝试次数（失败自增，用于计算退避间隔）
    /// </summary>
    public short TryCount { get; set; }

    /// <summary>
    /// 创建时间（放弃超时判定的基准）
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 下次可执行时间（延迟入队 / 失败退避写此字段）
    /// </summary>
    public DateTime NextTryTime { get; set; }

    /// <summary>
    /// 上次尝试时间
    /// </summary>
    public DateTime? LastTryTime { get; set; }

    /// <summary>
    /// 是否已放弃（连续失败累计超时或遇致命错误）
    /// </summary>
    public bool IsAbandoned { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    public BackgroundJobPriority Priority { get; set; } = BackgroundJobPriority.Normal;
}
