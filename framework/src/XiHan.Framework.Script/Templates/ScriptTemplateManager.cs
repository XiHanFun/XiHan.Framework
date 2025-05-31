#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptTemplateManager
// Guid:f2g3h4i5-j6k7-l8m9-n0o1-p2q3r4s5t6u7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;

namespace XiHan.Framework.Script.Templates;

/// <summary>
/// 脚本模板管理器
/// </summary>
public class ScriptTemplateManager
{
    private readonly Dictionary<string, ScriptTemplate> _templates = new();
    private readonly string _templateDirectory;

    /// <summary>
    /// 初始化脚本模板管理器
    /// </summary>
    /// <param name="templateDirectory">模板目录</param>
    public ScriptTemplateManager(string? templateDirectory = null)
    {
        _templateDirectory = templateDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
        LoadSystemTemplates();
        LoadCustomTemplates();
    }

    /// <summary>
    /// 获取所有模板
    /// </summary>
    /// <returns>模板列表</returns>
    public IEnumerable<ScriptTemplate> GetAllTemplates()
    {
        return _templates.Values;
    }

    /// <summary>
    /// 根据名称获取模板
    /// </summary>
    /// <param name="name">模板名称</param>
    /// <returns>模板实例</returns>
    public ScriptTemplate? GetTemplate(string name)
    {
        return _templates.TryGetValue(name, out var template) ? template : null;
    }

    /// <summary>
    /// 根据分类获取模板
    /// </summary>
    /// <param name="category">分类名称</param>
    /// <returns>模板列表</returns>
    public IEnumerable<ScriptTemplate> GetTemplatesByCategory(string category)
    {
        return _templates.Values.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 根据标签搜索模板
    /// </summary>
    /// <param name="tags">标签列表</param>
    /// <returns>模板列表</returns>
    public IEnumerable<ScriptTemplate> SearchTemplatesByTags(params string[] tags)
    {
        return _templates.Values.Where(t => 
            tags.Any(tag => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 搜索模板
    /// </summary>
    /// <param name="keyword">关键字</param>
    /// <returns>模板列表</returns>
    public IEnumerable<ScriptTemplate> SearchTemplates(string keyword)
    {
        return _templates.Values.Where(t =>
            t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            t.Tags.Any(tag => tag.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 注册模板
    /// </summary>
    /// <param name="template">模板实例</param>
    public void RegisterTemplate(ScriptTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new ArgumentException("模板名称不能为空", nameof(template));
        }

        _templates[template.Name] = template;
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    /// <param name="name">模板名称</param>
    /// <returns>是否删除成功</returns>
    public bool RemoveTemplate(string name)
    {
        var template = GetTemplate(name);
        return template?.IsSystem == true ? throw new InvalidOperationException("不能删除系统模板") : _templates.Remove(name);
    }

    /// <summary>
    /// 保存自定义模板到文件
    /// </summary>
    /// <param name="template">模板实例</param>
    public async Task SaveTemplateAsync(ScriptTemplate template)
    {
        if (template.IsSystem)
        {
            throw new InvalidOperationException("不能保存系统模板");
        }

        Directory.CreateDirectory(_templateDirectory);
        var filePath = Path.Combine(_templateDirectory, $"{template.Name}.json");
        
        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json);
        RegisterTemplate(template);
    }

    /// <summary>
    /// 从文件加载模板
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public async Task<ScriptTemplate?> LoadTemplateFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var template = JsonSerializer.Deserialize<ScriptTemplate>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (template != null)
            {
                RegisterTemplate(template);
            }

            return template;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 生成脚本代码
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="parameters">参数</param>
    /// <returns>生成的脚本代码</returns>
    public string GenerateScript(string templateName, Dictionary<string, object?> parameters)
    {
        var template = GetTemplate(templateName);
        if (template == null)
        {
            throw new ArgumentException($"未找到模板: {templateName}");
        }

        var validation = template.ValidateParameters(parameters);
        return !validation.IsValid
            ? throw new ArgumentException($"模板参数验证失败: {validation.FormatErrors()}")
            : template.GenerateCode(parameters);
    }

    /// <summary>
    /// 加载系统模板
    /// </summary>
    private void LoadSystemTemplates()
    {
        // Hello World 模板
        RegisterTemplate(new ScriptTemplate
        {
            Name = "HelloWorld",
            Description = "简单的 Hello World 示例",
            Category = "Basic",
            Author = "System",
            Code = """
                // Hello World 示例
                var message = "Hello, #{Name}!";
                Console.WriteLine(message);
                result = message;
                """,
            Parameters = [
                new TemplateParameter
                {
                    Name = "Name",
                    Type = TemplateParameterType.String,
                    Description = "要问候的名称",
                    DefaultValue = "World",
                    Required = false
                }
            ],
            RequiredNamespaces = ["System"],
            Tags = ["basic", "hello", "console"],
            IsSystem = true,
            Example = "Name: \"张三\" -> \"Hello, 张三!\""
        });

        // 数学计算模板
        RegisterTemplate(new ScriptTemplate
        {
            Name = "MathCalculator",
            Description = "数学计算器模板",
            Category = "Math",
            Author = "System",
            Code = """
                // 数学计算器
                double a = #{NumberA};
                double b = #{NumberB};
                string operation = "#{Operation}";
                
                double result = operation switch
                {
                    "add" => a + b,
                    "subtract" => a - b,
                    "multiply" => a * b,
                    "divide" => b != 0 ? a / b : throw new DivideByZeroException("除数不能为零"),
                    _ => throw new ArgumentException($"不支持的操作: {operation}")
                };
                
                Console.WriteLine($"{a} {operation} {b} = {result}");
                return result;
                """,
            Parameters = [
                new TemplateParameter
                {
                    Name = "NumberA",
                    Type = TemplateParameterType.Double,
                    Description = "第一个数字",
                    DefaultValue = "10",
                    Required = true
                },
                new TemplateParameter
                {
                    Name = "NumberB",
                    Type = TemplateParameterType.Double,
                    Description = "第二个数字",
                    DefaultValue = "5",
                    Required = true
                },
                new TemplateParameter
                {
                    Name = "Operation",
                    Type = TemplateParameterType.Enum,
                    Description = "数学操作",
                    DefaultValue = "add",
                    Required = true,
                    Options = ["add", "subtract", "multiply", "divide"]
                }
            ],
            RequiredNamespaces = ["System"],
            Tags = ["math", "calculator", "arithmetic"],
            IsSystem = true,
            Example = "NumberA: 10, NumberB: 5, Operation: \"add\" -> 15"
        });

        // 数据处理模板
        RegisterTemplate(new ScriptTemplate
        {
            Name = "DataProcessor",
            Description = "数据列表处理模板",
            Category = "Data",
            Author = "System",
            Code = """
                using System.Linq;
                
                // 数据处理示例
                var numbers = new[] { #{Numbers} };
                
                var sum = numbers.Sum();
                var average = numbers.Average();
                var max = numbers.Max();
                var min = numbers.Min();
                var count = numbers.Count();
                
                var summary = new {
                    Sum = sum,
                    Average = Math.Round(average, 2),
                    Max = max,
                    Min = min,
                    Count = count
                };
                
                Console.WriteLine($"数据统计: 总和={summary.Sum}, 平均值={summary.Average}, 最大值={summary.Max}, 最小值={summary.Min}, 数量={summary.Count}");
                result = summary;
                """,
            Parameters = [
                new TemplateParameter
                {
                    Name = "Numbers",
                    Type = TemplateParameterType.String,
                    Description = "数字列表（逗号分隔）",
                    DefaultValue = "1, 2, 3, 4, 5",
                    Required = true,
                    Pattern = @"^[\d\s,]+$"
                }
            ],
            RequiredNamespaces = ["System", "System.Linq"],
            Tags = ["data", "statistics", "linq"],
            IsSystem = true,
            Example = "Numbers: \"1,2,3,4,5\" -> 统计信息对象"
        });
    }

    /// <summary>
    /// 加载自定义模板
    /// </summary>
    private void LoadCustomTemplates()
    {
        if (!Directory.Exists(_templateDirectory))
        {
            return;
        }

        var templateFiles = Directory.GetFiles(_templateDirectory, "*.json");
        foreach (var file in templateFiles)
        {
            try
            {
                _ = LoadTemplateFromFileAsync(file).Result;
            }
            catch
            {
                // 忽略加载失败的模板文件
            }
        }
    }
} 
