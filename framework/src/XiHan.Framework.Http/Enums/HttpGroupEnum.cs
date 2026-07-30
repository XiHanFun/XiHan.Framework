// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Http.Enums;

/// <summary>
/// 网络请求组别
/// </summary>
public enum HttpGroupEnum
{
    /// <summary>
    /// 远程
    /// </summary>
    [Description("远程")]
    Remote,

    /// <summary>
    /// 本地
    /// </summary>
    [Description("本地")]
    Local
}
