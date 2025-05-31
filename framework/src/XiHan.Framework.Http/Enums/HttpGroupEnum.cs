#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MulanPSL2 License. See LICENSE in the project root for license information.
// FileName:HttpGroupEnum
// Guid:f06f7e72-341f-43bf-80b0-375eecb05957
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-10-08 下午 10:28:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Http.Enums;

/// <summary>
/// 网络请求组别
/// </summary>
public enum HttpGroupEnum
{
    /// <summary>
    /// 远程
    /// </summary>
    [Description("远程")]
    Remote,

    /// <summary>
    /// 本地
    /// </summary>
    [Description("本地")]
    Local
}
