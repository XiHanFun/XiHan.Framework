#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PrecompilationTargetFormat
// Guid:b6e6209a-0c42-427e-b3de-afbdb56cc956
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:17:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 预编译目标格式
/// </summary>
public enum PrecompilationTargetFormat
{
    /// <summary>
    /// 源代码格式
    /// </summary>
    Source,

    /// <summary>
    /// 二进制格式
    /// </summary>
    Binary,

    /// <summary>
    /// 字节码格式
    /// </summary>
    Bytecode,

    /// <summary>
    /// 程序集格式
    /// </summary>
    Assembly
}
