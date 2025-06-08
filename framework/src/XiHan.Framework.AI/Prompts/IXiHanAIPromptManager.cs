#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIPromptManager
// Guid:ef739c67-100d-41bc-9efd-2f6054190534
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Prompts;

/// <summary>
/// 提示词管理器接口
/// </summary>
public interface IXiHanAiPromptManager
{
    /// <summary>
    /// 获取提示词模板
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模板内容</returns>
    Task<string> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 渲染提示词模板
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="variables">变量字典</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染后的提示词</returns>
    Task<string> RenderTemplateAsync(string templateName, object variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加或更新提示词模板
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="templateContent">模板内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> SaveTemplateAsync(string templateName, string templateContent, CancellationToken cancellationToken = default);
}
