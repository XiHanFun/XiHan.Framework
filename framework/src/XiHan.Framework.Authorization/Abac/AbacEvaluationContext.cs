// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// ABAC 评估上下文
/// </summary>
public sealed class AbacEvaluationContext
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 权限编码
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 策略编码
    /// </summary>
    public string PolicyCode { get; set; } = string.Empty;

    /// <summary>
    /// 原始资源对象
    /// </summary>
    public object? Resource { get; set; }

    /// <summary>
    /// 主体属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> SubjectAttributes { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 资源属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> ResourceAttributes { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 环境属性
    /// </summary>
    public IReadOnlyDictionary<string, object?> EnvironmentAttributes { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 评估时间
    /// </summary>
    public DateTimeOffset EvaluationTime { get; set; } = DateTimeOffset.UtcNow;
}
