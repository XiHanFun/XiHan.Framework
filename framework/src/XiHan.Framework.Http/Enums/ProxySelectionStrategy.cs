// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
