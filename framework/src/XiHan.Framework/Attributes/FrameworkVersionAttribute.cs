#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkVersionAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架版本特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public class FrameworkVersionAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="version">版本号</param>
    /// <param name="isDeprecated">是否已弃用</param>
    /// <param name="deprecationMessage">弃用消息</param>
    public FrameworkVersionAttribute(string version, bool isDeprecated = false, string deprecationMessage = "")
    {
        Version = version;
        IsDeprecated = isDeprecated;
        DeprecationMessage = deprecationMessage;
    }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 是否已弃用
    /// </summary>
    public bool IsDeprecated { get; }

    /// <summary>
    /// 弃用消息
    /// </summary>
    public string DeprecationMessage { get; }
}
