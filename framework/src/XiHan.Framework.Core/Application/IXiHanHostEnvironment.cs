// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 曦寒宿主环境接口
/// </summary>
public interface IXiHanHostEnvironment
{
    /// <summary>
    /// 环境名称
    /// </summary>
    string? EnvironmentName { get; set; }
}
