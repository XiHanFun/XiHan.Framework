// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
