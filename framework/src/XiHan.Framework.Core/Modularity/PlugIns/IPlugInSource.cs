// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 插件源接口
/// </summary>
public interface IPlugInSource
{
    /// <summary>
    /// 获取模块类型
    /// </summary>
    /// <returns></returns>
    Type[] GetModules();
}
