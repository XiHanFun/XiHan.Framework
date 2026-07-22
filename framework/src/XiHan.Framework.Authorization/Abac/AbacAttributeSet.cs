// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// ABAC 属性快照
/// </summary>
public sealed class AbacAttributeSet
{
    /// <summary>
    /// 主体属性
    /// </summary>
    public Dictionary<string, object?> SubjectAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 资源属性
    /// </summary>
    public Dictionary<string, object?> ResourceAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 环境属性
    /// </summary>
    public Dictionary<string, object?> EnvironmentAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
