#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileExistsAttribute
// Guid:1d3eff2b-610b-4bb1-bd5a-75b1868ad683
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:03:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine.Validators;

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 文件存在验证属性
/// </summary>
public class FileExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建文件存在验证属性
    /// </summary>
    public FileExistsAttribute() : base(typeof(FileExistsValidator))
    {
    }
}
