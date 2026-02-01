#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompilerOptions
// Guid:f5142e02-1702-437b-81a4-98d85b64e014
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:09:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace XiHan.Framework.Script.Options;

/// <summary>
/// 编译器选项
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// 语言版本
    /// </summary>
    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Latest;

    /// <summary>
    /// 预处理器符号
    /// </summary>
    public List<string> PreprocessorSymbols { get; set; } = [];

    /// <summary>
    /// 警告等级
    /// </summary>
    public int WarningLevel { get; set; } = 4;

    /// <summary>
    /// 将警告视为错误
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; } = false;

    /// <summary>
    /// 特定警告视为错误
    /// </summary>
    public List<string> WarningsAsErrors { get; set; } = [];

    /// <summary>
    /// 忽略的警告
    /// </summary>
    public List<string> WarningsNotAsErrors { get; set; } = [];

    /// <summary>
    /// 禁用的警告
    /// </summary>
    public List<string> DisabledWarnings { get; set; } = [];

    /// <summary>
    /// 是否生成调试信息
    /// </summary>
    public bool GenerateDebugInfo { get; set; } = true;

    /// <summary>
    /// 调试信息格式
    /// </summary>
    public DebugInformationFormat DebugInformationFormat { get; set; } = DebugInformationFormat.PortablePdb;
}
