#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILocalizeErrorMessage
// Guid:a618c88c-9431-43dd-ab00-7c1526423b7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/23 00:58:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Localization;

namespace XiHan.Framework.Core.Exceptions.Abstracts;

/// <summary>
/// 本地化报错信息接口
/// </summary>
public interface ILocalizeErrorMessage
{
    /// <summary>
    /// 本地化报错信息
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    string LocalizeErrorMessage(LocalizationContext context);
}
