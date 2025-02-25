#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanStringLocalizer
// Guid:f8d72c53-6b1a-4e19-b7c8-a9e86f4e5d2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:16:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 曦寒字符串本地化器接口
/// 扩展微软标准IStringLocalizer，提供额外功能
/// </summary>
public interface IXiHanStringLocalizer : IStringLocalizer
{
    /// <summary>
    /// 获取指定语言的本地化字符串
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="culture">特定的语言文化</param>
    /// <returns>本地化后的字符串</returns>
    LocalizedString GetWithCulture(string name, string culture);

    /// <summary>
    /// 获取指定语言的本地化字符串，支持参数插值
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="culture">特定的语言文化</param>
    /// <param name="arguments">格式化参数</param>
    /// <returns>本地化后的字符串</returns>
    LocalizedString GetWithCulture(string name, string culture, params object[] arguments);

    /// <summary>
    /// 获取当前资源可用的所有文化
    /// </summary>
    /// <returns>可用的文化列表</returns>
    IReadOnlyList<string> GetSupportedCultures();

    /// <summary>
    /// 获取根资源路径
    /// </summary>
    /// <returns>资源路径</returns>
    string GetResourceBasePath();
}
