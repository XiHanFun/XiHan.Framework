#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileTemplateHelper
// Guid:a9e52c38-f6d1-4c8b-b9e2-03d7f8a1c6e4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 01:54:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Templating.Simple;

/// <summary>
/// 文件模板帮助类
/// </summary>
public static class FileTemplateHelper
{
    /// <summary>
    /// 从文件加载模板并渲染
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的文本</returns>
    public static string RenderFile(string templateFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return TemplateEngine.Render(template, values);
    }

    /// <summary>
    /// 从文件加载模板并渲染
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="model">对象模型</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的文本</returns>
    public static string RenderFile(string templateFilePath, object model, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return TemplateEngine.Render(template, model);
    }

    /// <summary>
    /// 从文件加载高级模板并渲染
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的文本</returns>
    public static string RenderAdvancedFile(string templateFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return TemplateEngine.RenderAdvanced(template, values);
    }

    /// <summary>
    /// 渲染模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    public static void RenderToFile(string templateFilePath, string outputFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = RenderFile(templateFilePath, values, encoding);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputFilePath, renderedContent, encoding);
    }

    /// <summary>
    /// 渲染模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="model">对象模型</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    public static void RenderToFile(string templateFilePath, string outputFilePath, object model, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = RenderFile(templateFilePath, model, encoding);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputFilePath, renderedContent, encoding);
    }

    /// <summary>
    /// 渲染高级模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    public static void RenderAdvancedToFile(string templateFilePath, string outputFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = RenderAdvancedFile(templateFilePath, values, encoding);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputFilePath, renderedContent, encoding);
    }

    /// <summary>
    /// 异步从文件加载模板并渲染
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染后的文本</returns>
    public static async Task<string> RenderFileAsync(string templateFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = await File.ReadAllTextAsync(templateFilePath, encoding);
        return TemplateEngine.Render(template, values);
    }

    /// <summary>
    /// 异步渲染模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="values">参数字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    public static async Task RenderToFileAsync(string templateFilePath, string outputFilePath, IDictionary<string, object?> values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = await RenderFileAsync(templateFilePath, values, encoding);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(outputFilePath, renderedContent, encoding);
    }
}
