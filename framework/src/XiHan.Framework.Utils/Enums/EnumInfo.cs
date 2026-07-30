// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo
{
    /// <summary>
    /// 枚举类型
    /// </summary>
    public Type Type { get; set; } = null!;

    /// <summary>
    /// 底层类型
    /// </summary>
    public Type UnderlyingType { get; set; } = null!;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否为标志枚举
    /// </summary>
    public bool IsFlags { get; set; }

    /// <summary>
    /// 所有值
    /// </summary>
    public object[] Values { get; set; } = [];

    /// <summary>
    /// 所有名称
    /// </summary>
    public string[] Names { get; set; } = [];

    /// <summary>
    /// 默认本地化资源名
    /// </summary>
    public string? LocalizationResourceName { get; set; }

    /// <summary>
    /// 默认本地化键前缀
    /// </summary>
    public string? LocalizationKeyPrefix { get; set; }
}
