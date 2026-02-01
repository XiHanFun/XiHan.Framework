#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DirectoryExistsAttribute
// Guid:b23016ea-6097-4909-a8ab-2908b1e8de9f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:03:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Validators;

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 目录存在验证属性
/// </summary>
public class DirectoryExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建目录存在验证属性
    /// </summary>
    public DirectoryExistsAttribute() : base(typeof(DirectoryExistsValidator))
    {
    }
}
