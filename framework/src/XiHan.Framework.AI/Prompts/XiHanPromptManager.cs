#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanPromptManager
// Guid:aa38cb1d-88cd-4d77-9668-5a5adbb81cad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace XiHan.Framework.AI.Prompts;

/// <summary>
/// 提示词管理器实现
/// </summary>
public class XiHanPromptManager : IXiHanAIPromptManager
{
    private readonly ConcurrentDictionary<string, string> _templates = new();
    private readonly string _templatesDirectory;
    private readonly ILogger<XiHanPromptManager> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public XiHanPromptManager(ILogger<XiHanPromptManager> logger)
    {
        _logger = logger;

        // 获取程序集所在目录
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

        // 模板目录
        _templatesDirectory = Path.Combine(assemblyDirectory!, "Prompts", "Templates");

        // 确保目录存在
        Directory.CreateDirectory(_templatesDirectory);

        // 加载所有模板
        LoadTemplates();
    }

    /// <summary>
    /// 获取提示词模板
    /// </summary>
    public Task<string> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        if (_templates.TryGetValue(templateName, out var template))
        {
            return Task.FromResult(template);
        }

        // 尝试从文件加载
        var filePath = Path.Combine(_templatesDirectory, $"{templateName}.prompt");
        if (File.Exists(filePath))
        {
            var templateContent = File.ReadAllText(filePath, Encoding.UTF8);
            _templates[templateName] = templateContent;
            return Task.FromResult(templateContent);
        }

        _logger.LogWarning("未找到提示词模板: {TemplateName}", templateName);
        return Task.FromResult(string.Empty);
    }

    /// <summary>
    /// 渲染提示词模板
    /// </summary>
    public async Task<string> RenderTemplateAsync(string templateName, object variables, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateName, cancellationToken);
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        // 简单的变量替换
        var result = template;

        // 遍历对象的所有属性
        foreach (var prop in variables.GetType().GetProperties())
        {
            var value = prop.GetValue(variables)?.ToString() ?? string.Empty;
            result = result.Replace($"{{{{{prop.Name}}}}}", value);
        }

        return result;
    }

    /// <summary>
    /// 添加或更新提示词模板
    /// </summary>
    public Task<bool> SaveTemplateAsync(string templateName, string templateContent, CancellationToken cancellationToken = default)
    {
        try
        {
            // 更新内存中的模板
            _templates[templateName] = templateContent;

            // 保存到文件
            var filePath = Path.Combine(_templatesDirectory, $"{templateName}.prompt");
            File.WriteAllText(filePath, templateContent, Encoding.UTF8);

            _logger.LogInformation("已保存提示词模板: {TemplateName}", templateName);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存提示词模板时出错: {TemplateName}", templateName);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 加载所有模板
    /// </summary>
    private void LoadTemplates()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_templatesDirectory, "*.prompt"))
            {
                var templateName = Path.GetFileNameWithoutExtension(file);
                var templateContent = File.ReadAllText(file, Encoding.UTF8);
                _templates[templateName] = templateContent;
            }

            _logger.LogInformation("已加载{Count}个提示词模板", _templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载提示词模板时出错");
        }
    }
}
