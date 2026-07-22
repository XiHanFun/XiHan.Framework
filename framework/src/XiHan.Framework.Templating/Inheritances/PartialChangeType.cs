// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 片段变化类型
/// </summary>
public enum PartialChangeType
{
    /// <summary>
    /// 创建
    /// </summary>
    Created,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted,

    /// <summary>
    /// 重命名
    /// </summary>
    Renamed
}
