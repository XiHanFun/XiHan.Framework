#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultLocalizationResource
// Guid:5f4a3e2d-1f0a-9b8c-7d6e-5f4a-3e2d-1f0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:35:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Resources;

/// <summary>
/// 默认本地化资源
/// </summary>
public class DefaultLocalizationResource : BaseLocalizationResource
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="defaultCulture">默认文化</param>
    public DefaultLocalizationResource(string resourceName, string defaultCulture = "en")
        : base(resourceName, defaultCulture)
    {
    }
}
