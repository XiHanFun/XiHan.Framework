#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParseOptions
// Guid:f7ed73e4-af64-4709-89c3-8fc52c78d93e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:09:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Arguments;

/// <summary>
/// 参数解析配置
/// </summary>
public class ParseOptions
{
    /// <summary>
    /// 是否允许未知选项
    /// </summary>
    public bool AllowUnknownOptions { get; set; } = false;

    /// <summary>
    /// 是否大小写敏感
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// 是否启用POSIX样式（单个-后面可以跟多个短选项）
    /// </summary>
    public bool EnablePosixStyle { get; set; } = true;

    /// <summary>
    /// 是否自动生成帮助选项
    /// </summary>
    public bool AutoGenerateHelp { get; set; } = true;

    /// <summary>
    /// 是否自动生成版本选项
    /// </summary>
    public bool AutoGenerateVersion { get; set; } = true;

    /// <summary>
    /// 帮助选项名称
    /// </summary>
    public string[] HelpOptions { get; set; } = ["help", "h"];

    /// <summary>
    /// 版本选项名称
    /// </summary>
    public string[] VersionOptions { get; set; } = ["version", "v"];

    /// <summary>
    /// 值分隔符
    /// </summary>
    public char[] ValueSeparators { get; set; } = ['=', ':'];

    /// <summary>
    /// 停止解析标记（通常是 --）
    /// </summary>
    public string StopParsingMarker { get; set; } = "--";
}
