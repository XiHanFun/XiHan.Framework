// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
