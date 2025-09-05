#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateCache
// Guid:e3f82d17-9c8a-4b15-a6e4-7d8f2c9d5b1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 01:49:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text;

namespace XiHan.Framework.Templating;

/// <summary>
/// 模板缓存
/// </summary>
public static class TemplateCache
{
    private static readonly ConcurrentDictionary<string, string> Templates = new();

    /// <summary>
    /// 获取缓存中的模板
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <returns>模板内容，如果不存在则返回null</returns>
    public static string? GetTemplate(string key)
    {
        Templates.TryGetValue(key, out var template);
        return template;
    }

    /// <summary>
    /// 添加或更新缓存中的模板
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="template">模板内容</param>
    public static void SetTemplate(string key, string template)
    {
        Templates.AddOrUpdate(key, template, (_, _) => template);
    }

    /// <summary>
    /// 从缓存中移除模板
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <returns>是否成功移除</returns>
    public static bool RemoveTemplate(string key)
    {
        return Templates.TryRemove(key, out _);
    }

    /// <summary>
    /// 清空模板缓存
    /// </summary>
    public static void ClearTemplates()
    {
        Templates.Clear();
    }

    /// <summary>
    /// 获取或加载模板，优先从缓存获取，不存在则从文件加载并缓存
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>模板内容</returns>
    public static string GetOrLoadTemplate(string key, string templateFilePath, Encoding? encoding = null)
    {
        if (Templates.TryGetValue(key, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        SetTemplate(key, template);

        return template;
    }

    /// <summary>
    /// 使用缓存模板进行渲染
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="values">参数字典</param>
    /// <returns>渲染后的内容</returns>
    public static string RenderCachedTemplate(string key, IDictionary<string, object?> values)
    {
        var template = GetTemplate(key);
        return string.IsNullOrEmpty(template)
            ? throw new KeyNotFoundException($"模板缓存中不存在键名为 {key} 的模板")
            : TemplateEngine.Render(template, values);
    }

    /// <summary>
    /// 使用缓存模板进行高级渲染
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="values">参数字典</param>
    /// <returns>渲染后的内容</returns>
    public static string RenderAdvancedCachedTemplate(string key, IDictionary<string, object?> values)
    {
        var template = GetTemplate(key);
        return string.IsNullOrEmpty(template)
            ? throw new KeyNotFoundException($"模板缓存中不存在键名为 {key} 的模板")
            : TemplateEngine.RenderAdvanced(template, values);
    }

    /// <summary>
    /// 获取或加载模板并进行渲染
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的内容</returns>
    public static string GetOrLoadAndRender(string key, string templateFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        var template = GetOrLoadTemplate(key, templateFilePath, encoding);
        return TemplateEngine.Render(template, values);
    }

    /// <summary>
    /// 获取或加载模板并进行高级渲染
    /// </summary>
    /// <param name="key">模板键名</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的内容</returns>
    public static string GetOrLoadAndRenderAdvanced(string key, string templateFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        var template = GetOrLoadTemplate(key, templateFilePath, encoding);
        return TemplateEngine.RenderAdvanced(template, values);
    }
}
