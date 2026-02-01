#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplatePrecompiler
// Guid:65544629-3ebb-4eed-8bfb-a29dec051989
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:16:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板预编译器接口
/// </summary>
public interface ITemplatePrecompiler
{
    /// <summary>
    /// 预编译模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="templateName">模板名称</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果</returns>
    TemplatePrecompilationResult Precompile(string templateSource, string templateName, TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 异步预编译模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="templateName">模板名称</param>
    /// <param name="options">预编译选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预编译结果</returns>
    Task<TemplatePrecompilationResult> PrecompileAsync(string templateSource, string templateName, TemplatePrecompilationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量预编译模板
    /// </summary>
    /// <param name="templates">模板字典</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果集合</returns>
    Task<IDictionary<string, TemplatePrecompilationResult>> PrecompileBatchAsync(IDictionary<string, string> templates, TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 预编译目录中的所有模板
    /// </summary>
    /// <param name="directory">模板目录</param>
    /// <param name="pattern">文件模式</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果集合</returns>
    Task<IDictionary<string, TemplatePrecompilationResult>> PrecompileDirectoryAsync(string directory, string pattern = "*.html", TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 获取预编译结果
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <returns>预编译结果</returns>
    TemplatePrecompilationResult? GetPrecompiledTemplate(string templateName);

    /// <summary>
    /// 清空预编译缓存
    /// </summary>
    void ClearPrecompiledCache();
}
