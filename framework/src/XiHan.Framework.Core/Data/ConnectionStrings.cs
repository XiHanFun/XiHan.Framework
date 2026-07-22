// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
