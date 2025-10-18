#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConnectionStrings
// Guid:aa3d3050-b041-4a43-8705-e0d1a129770f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 6:49:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Data;

/// <summary>
/// 连接字符串集合
/// </summary>
[Serializable]
public class ConnectionStrings : Dictionary<string, string?>
{
    /// <summary>
    /// 默认连接字符串名称
    /// </summary>
    public const string DefaultConnectionStringName = "Default";

    /// <summary>
    /// 默认连接字符串
    /// </summary>
    public string? Default
    {
        get => this.GetOrDefault(DefaultConnectionStringName);
        set => this[DefaultConnectionStringName] = value;
    }
}
