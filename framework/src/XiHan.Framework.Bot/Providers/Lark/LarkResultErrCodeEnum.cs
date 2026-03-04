#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkResultErrCodeEnum
// Guid:bd2ec69e-d4b5-4d85-91e3-cf9379b7e444
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/09/02 23:56:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Bot.Providers.Lark;

/// <summary>
/// 结果代码
/// </summary>
public enum LarkResultErrCodeEnum
{
    /// <summary>
    /// 请求体格式错误，请求体内容格式是否与各消息类型的示例代码一致，请求体大小不能超过20k
    /// </summary>
    [Description("请求体格式错误，请求体内容格式是否与各消息类型的示例代码一致，请求体大小不能超过20k")]
    BadRequest = 9499,

    /// <summary>
    /// 签名校验失败，请排查以下原因：
    /// 1、时间戳距发送时已超过 1 小时，签名已过期；
    /// 2、服务器时间与标准时间有比较大的偏差，导致签名过期。请注意检查、校准你的服务器时间；
    /// 3、签名不匹配，校验不通过。
    /// </summary>
    [Description("签名校验失败，请排查以下原因：1、时间戳距发送时已超过 1 小时，签名已过期；2、服务器时间与标准时间有比较大的偏差，导致签名过期。请注意检查、校准你的服务器时间；3、签名不匹配，校验不通过。")]
    SignMatchFail = 19021,

    /// <summary>
    /// IP校验失败
    /// </summary>
    [Description("IP校验失败")] IpNotAllowed = 19022,

    /// <summary>
    /// 关键词校验失败
    /// </summary>
    [Description("关键词校验失败")] KeyWordsNotFound = 19024
}
