#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeScript
// Guid:6b8b6802-25e1-4c59-9e64-9b6c9078a43b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:24:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Models;

/// <summary>
/// 升级脚本信息
/// </summary>
/// <param name="Version">脚本版本</param>
/// <param name="ScriptName">脚本名称</param>
/// <param name="ScriptPath">脚本路径</param>
public sealed record UpgradeScript(string Version, string ScriptName, string ScriptPath);
