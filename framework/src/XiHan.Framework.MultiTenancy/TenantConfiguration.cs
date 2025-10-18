#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantConfiguration
// Guid:a7fa1aac-dd4f-4e01-aac1-f9a105638a57
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:48:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Data;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 租户配置
/// </summary>
[Serializable]
public class TenantConfiguration
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public TenantConfiguration()
    {
        IsActive = true;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    public TenantConfiguration(Guid id, [NotNull] string name)
        : this()
    {
        Guard.NotNull(name, nameof(name));

        Id = id;
        Name = name;

        ConnectionStrings = [];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <param name="normalizedName">租户规范化名称</param>
    /// <param name="editionId">租户版本唯一标识</param>
    public TenantConfiguration(Guid id, [NotNull] string name, [NotNull] string normalizedName, Guid? editionId = null)
        : this(id, name)
    {
        Guard.NotNull(normalizedName, nameof(normalizedName));

        NormalizedName = normalizedName;
        EditionId = editionId;
    }

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 租户规范化名称
    /// </summary>
    public string NormalizedName { get; set; } = default!;

    /// <summary>
    /// 租户连接字符串
    /// </summary>
    public ConnectionStrings? ConnectionStrings { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 租户版本唯一标识
    /// </summary>
    public Guid? EditionId { get; set; }
}
