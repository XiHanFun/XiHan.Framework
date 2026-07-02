#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SmsMessageDataKeys
// Guid:e79364b6-841e-43a8-823f-471244c8b910
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Sms.Options;

/// <summary>
/// 短信消息 Data 键名常量
/// </summary>
public static class SmsMessageDataKeys
{
    /// <summary>
    /// 接收手机号（string 逗号分隔 或 IEnumerable&lt;string&gt;）
    /// </summary>
    public const string PhoneNumbers = "Sms.PhoneNumbers";

    /// <summary>
    /// 内部模板码（经配置 TemplateMap 映射为服务商模板码）
    /// </summary>
    public const string TemplateCode = "Sms.TemplateCode";

    /// <summary>
    /// 模板参数 JSON（键值对；键须与服务商模板变量名一致）
    /// </summary>
    public const string TemplateParams = "Sms.TemplateParams";
}
