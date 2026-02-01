#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxySelectionStrategy
// Guid:4749728a-d76c-480b-aecc-f249b9b70bc2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Http.Enums;

/// <summary>
/// 代理选择策略枚举
/// </summary>
public enum ProxySelectionStrategy
{
    /// <summary>
    /// 轮询策略
    /// </summary>
    RoundRobin = 0,

    /// <summary>
    /// 随机选择
    /// </summary>
    Random = 1,

    /// <summary>
    /// 最少使用
    /// </summary>
    LeastUsed = 2,

    /// <summary>
    /// 最快响应
    /// </summary>
    FastestResponse = 3,

    /// <summary>
    /// 优先级
    /// </summary>
    Priority = 4
}
