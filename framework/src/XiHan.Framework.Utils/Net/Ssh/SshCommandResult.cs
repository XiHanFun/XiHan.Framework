#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SshCommandResult
// Guid:f2f6f25a-6cf0-401b-9769-51f7e9eb8dda
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 15:32:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Net.Ssh;

/// <summary>
/// SSH命令执行结果
/// </summary>
public class SshCommandResult
{
    /// <summary>
    /// 执行的命令
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// 执行结果
    /// </summary>
    public string Result { get; set; } = "";

    /// <summary>
    /// 错误信息
    /// </summary>
    public string Error { get; set; } = "";

    /// <summary>
    /// 退出状态码
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool IsSuccess => ExitCode == 0;
}
