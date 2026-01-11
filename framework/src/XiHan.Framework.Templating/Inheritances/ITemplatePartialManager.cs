#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplatePartialManager
// Guid:8f3h7h2d-6g9i-7e2f-3h8h-7d2g9i6f3h8h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplatePartialManager
// Guid:8f3h7h2d-6g9i-7e2f-3h8h-7d2g9i6f3h8h
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板片段管理器接口
/// </summary>
public interface ITemplatePartialManager
{
    /// <summary>
    /// 注册模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="template">片段模板</param>
    void RegisterPartial(string name, string template);

    /// <summary>
    /// 获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    string? GetPartial(string name);

    /// <summary>
    /// 异步获取模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>片段模板</returns>
    Task<string?> GetPartialAsync(string name);

    /// <summary>
    /// 移除模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <returns>是否成功移除</returns>
    bool RemovePartial(string name);

    /// <summary>
    /// 获取所有片段名称
    /// </summary>
    /// <returns>片段名称集合</returns>
    IEnumerable<string> GetPartialNames();

    /// <summary>
    /// 渲染模板片段
    /// </summary>
    /// <param name="name">片段名称</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderPartialAsync(string name, ITemplateContext context);

    /// <summary>
    /// 预编译所有片段
    /// </summary>
    /// <returns>预编译任务</returns>
    Task PrecompileAllPartialsAsync();

    /// <summary>
    /// 清空片段缓存
    /// </summary>
    void ClearPartialCache();
}
