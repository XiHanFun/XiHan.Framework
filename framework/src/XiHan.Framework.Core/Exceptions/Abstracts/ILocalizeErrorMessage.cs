// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
