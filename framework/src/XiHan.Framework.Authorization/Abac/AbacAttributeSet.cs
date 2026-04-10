#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AbacAttributeSet
// Guid:6ac91f13-3e5d-4b0e-9f92-bf8f7720a3f1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
