#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILocalizableString
// Guid:801991c2-a3c0-4711-ba49-55bc87c24361
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:57:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 可本地化字符串接口
/// 定义支持多语言本地化的字符串对象的基本契约
/// </summary>
public interface ILocalizableString
{
    /// <summary>
    /// 使用指定的字符串本地化工厂对字符串进行本地化处理
    /// </summary>
    /// <param name="stringLocalizerFactory">字符串本地化工厂实例</param>
    /// <returns>本地化后的字符串结果</returns>
    /// <exception cref="ArgumentNullException">当 stringLocalizerFactory 为 null 时</exception>
    LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory);
}
