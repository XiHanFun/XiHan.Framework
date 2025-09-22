#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMemoryPartialProvider
// Guid:2f3b8461-d5c0-4251-9c4d-2e4823799af3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:14:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 内存片段提供者
/// </summary>
public interface IMemoryPartialProvider : IPartialTemplateProvider
{
    /// <summary>
    /// 添加片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void AddPartial(string name, string template);

    /// <summary>
    /// 更新片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void UpdatePartial(string name, string template);

    /// <summary>
    /// 移除片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    bool RemovePartial(string name);

    /// <summary>
    /// 清空片段
    /// </summary>
    void ClearPartials();
}
