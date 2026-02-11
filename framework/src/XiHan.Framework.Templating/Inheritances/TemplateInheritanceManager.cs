#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateInheritanceManager
// Guid:514b1626-1e2c-4da3-a4be-6412e2089d32
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using XiHan.Framework.Templating.Compilers;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 模板继承管理器实现
/// </summary>
public class TemplateInheritanceManager : ITemplateInheritanceManager
{
    private readonly ConcurrentDictionary<string, string> _layouts = new();
    private readonly ITemplateEngineRegistry _engineRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="engineRegistry">模板引擎注册表</param>
    public TemplateInheritanceManager(ITemplateEngineRegistry engineRegistry)
    {
        _engineRegistry = engineRegistry;
    }

    /// <summary>
    /// 注册布局模板
    /// </summary>
    /// <param name="name">布局名称</param>
    /// <param name="template">布局模板</param>
    public void RegisterLayout(string name, string template)
    {
        _layouts[name] = template;
    }

    /// <summary>
    /// 获取布局模板
    /// </summary>
    /// <param name="name">布局名称</param>
    /// <returns>布局模板</returns>
    public string? GetLayout(string name)
    {
        return _layouts.TryGetValue(name, out var layout) ? layout : null;
    }

    /// <summary>
    /// 解析模板继承关系
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>继承信息</returns>
    public TemplateInheritanceInfo ParseInheritance(string templateSource)
    {
        var result = new TemplateInheritanceInfo();

        // 解析 extends 指令
        var extendsPattern = @"{{-?\s*extends\s+[""']([^""']+)[""']\s*-?}}";
        var extendsMatch = Regex.Match(templateSource, extendsPattern, RegexOptions.IgnoreCase);

        if (extendsMatch.Success)
        {
            result = result with
            {
                HasInheritance = true,
                ParentLayout = extendsMatch.Groups[1].Value
            };
        }

        // 解析块定义
        var blocks = ParseBlocks(templateSource);
        result = result with { Blocks = blocks };

        return result;
    }

    /// <summary>
    /// 合并模板和布局
    /// </summary>
    /// <param name="template">子模板</param>
    /// <param name="layout">布局模板</param>
    /// <param name="blocks">块定义</param>
    /// <returns>合并后的模板</returns>
    public string MergeTemplate(string template, string layout, IDictionary<string, string> blocks)
    {
        var result = layout;

        // 替换所有块占位符
        foreach (var (blockName, blockContent) in blocks)
        {
            var blockPlaceholder = $"{{{{- block {blockName} -}}}}";
            var blockEndPlaceholder = $"{{{{- endblock -}}}}";

            // 寻找块的开始和结束位置
            var startIndex = result.IndexOf(blockPlaceholder, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                var endIndex = result.IndexOf(blockEndPlaceholder, startIndex, StringComparison.OrdinalIgnoreCase);
                if (endIndex >= 0)
                {
                    // 替换整个块
                    var beforeBlock = result[..startIndex];
                    var afterBlock = result[(endIndex + blockEndPlaceholder.Length)..];
                    result = beforeBlock + blockContent + afterBlock;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 渲染继承模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderInheritedTemplateAsync(string templateSource, ITemplateContext context)
    {
        var inheritanceInfo = ParseInheritance(templateSource);

        if (!inheritanceInfo.HasInheritance || string.IsNullOrEmpty(inheritanceInfo.ParentLayout))
        {
            // 没有继承，直接渲染
            var engine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
            return await engine.RenderAsync(templateSource, context);
        }

        // 获取父布局
        var parentLayout = GetLayout(inheritanceInfo.ParentLayout) ?? throw new InvalidOperationException($"找不到布局模板: {inheritanceInfo.ParentLayout}");

        // 提取块内容
        var blockContents = new Dictionary<string, string>();
        foreach (var (blockName, block) in inheritanceInfo.Blocks)
        {
            blockContents[blockName] = block.Content;
        }

        // 合并模板
        var mergedTemplate = MergeTemplate(templateSource, parentLayout, blockContents);

        // 渲染合并后的模板
        var mergedEngine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return await mergedEngine.RenderAsync(mergedTemplate, context);
    }

    /// <summary>
    /// 解析模板块
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>块字典</returns>
    private static Dictionary<string, TemplateBlock> ParseBlocks(string templateSource)
    {
        var blocks = new Dictionary<string, TemplateBlock>();

        // 解析块定义: {{ block blockname }}...{{ endblock }}
        var blockPattern = @"{{-?\s*block\s+([^}\s]+)\s*-?}}(.*?){{-?\s*endblock\s*-?}}";
        var matches = Regex.Matches(templateSource, blockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var blockName = match.Groups[1].Value.Trim();
            var blockContent = match.Groups[2].Value;

            var block = new TemplateBlock
            {
                Name = blockName,
                Content = blockContent,
                IsOverridable = true,
                Location = new TemplateSourceLocation
                {
                    Line = templateSource[..match.Index].Count(c => c == '\n') + 1,
                    Column = match.Index - templateSource.LastIndexOf('\n', match.Index)
                }
            };

            blocks[blockName] = block;
        }

        return blocks;
    }
}
