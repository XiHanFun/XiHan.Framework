// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Models;

/// <summary>
/// 升级脚本信息
/// </summary>
/// <param name="Version">脚本版本</param>
/// <param name="ScriptName">脚本名称</param>
/// <param name="ScriptPath">脚本路径</param>
public sealed record UpgradeScript(string Version, string ScriptName, string ScriptPath);
