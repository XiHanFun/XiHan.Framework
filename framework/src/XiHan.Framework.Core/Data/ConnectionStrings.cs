#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConnectionStrings
// Guid:1e26d6b0-34e3-4a08-b4b8-14c5be4cccf1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/19 06:49:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.Data;

/// <summary>
/// 连接字符串配置
/// </summary>
[Serializable]
public class ConnectionStrings : Dictionary<string, string>
{
    /// <summary>
    /// 默认连接字符串键名
    /// </summary>
    public const string DefaultConnectionStringName = "Default";

    /// <summary>
    /// 默认连接字符串
    /// </summary>
    public string? Default
    {
        get => this.GetOrDefault(DefaultConnectionStringName);
        set => this[DefaultConnectionStringName] = value ?? string.Empty;
    }
}
