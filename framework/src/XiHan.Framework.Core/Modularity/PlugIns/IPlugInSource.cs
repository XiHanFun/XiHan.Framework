#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPlugInSource
// Guid:c55b0c7f-f693-4bcd-8ee2-a59d62b43fd5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:24:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
