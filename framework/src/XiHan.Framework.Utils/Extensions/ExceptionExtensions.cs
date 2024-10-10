#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionExtensions
// Guid:25adaec8-dba5-4b9d-a48f-c508b0810a3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-13 上午 04:00:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 异常拓展类
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// 抛出异常并打印信息
    /// </summary>
    public static void ThrowAndConsoleError(this Exception ex, string info)
    {
        var errorInfo = info + Environment.NewLine + ex.Message;
        errorInfo.WriteLineError();
        throw new Exception(errorInfo, ex);
    }
}