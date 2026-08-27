// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="DefaultTemplateEngine"/> 渲染、校验、缓存与文件模板的测试
/// </summary>
/// <remarks>
/// 默认引擎是纯正则替换实现，语义边界与 Scriban 不同：变量缺失时占位符原样保留，
/// 上下文为空时整段模板原样返回。这两条容易被误当成 bug，属于该引擎的既定契约，必须锁死。
/// </remarks>
public class DefaultTemplateEngineTests : IDisposable
{
    private readonly DefaultTemplateEngine _engine = new();
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public DefaultTemplateEngineTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// 空模板渲染为空字符串
    /// </summary>
    [Fact]
    public void Render_WhenTemplateEmpty_ReturnsEmptyString()
    {
        var context = CreateContext(("name", "曦寒"));

        Assert.Equal(string.Empty, _engine.Render(string.Empty, context));
    }

    /// <summary>
    /// 上下文没有任何变量时模板原样返回
    /// </summary>
    [Fact]
    public void Render_WhenContextHasNoVariables_ReturnsTemplateUnchanged()
    {
        const string template = "你好 {{name}}";

        Assert.Equal(template, _engine.Render(template, new TemplateContext()));
    }

    /// <summary>
    /// 变量占位符被替换为变量值
    /// </summary>
    [Fact]
    public void Render_WithVariable_ReplacesPlaceholder()
    {
        var context = CreateContext(("name", "曦寒"));

        Assert.Equal("你好 曦寒", _engine.Render("你好 {{name}}", context));
    }

    /// <summary>
    /// 同一个变量出现多次时全部替换
    /// </summary>
    [Fact]
    public void Render_WithRepeatedPlaceholder_ReplacesEveryOccurrence()
    {
        var context = CreateContext(("name", "曦寒"));

        Assert.Equal("曦寒-曦寒", _engine.Render("{{name}}-{{name}}", context));
    }

    /// <summary>
    /// 变量值为 null 时替换为空字符串
    /// </summary>
    [Fact]
    public void Render_WithNullVariableValue_ReplacesWithEmptyString()
    {
        var context = CreateContext(("name", null));

        Assert.Equal("你好 ", _engine.Render("你好 {{name}}", context));
    }

    /// <summary>
    /// 上下文里没有的占位符原样保留
    /// </summary>
    [Fact]
    public void Render_WhenPlaceholderNotInContext_KeepsPlaceholder()
    {
        var context = CreateContext(("name", "曦寒"));

        // 该引擎不做未知变量清理，保留占位符便于定位漏传的参数
        Assert.Equal("曦寒 {{missing}}", _engine.Render("{{name}} {{missing}}", context));
    }

    /// <summary>
    /// 条件为真时输出真值块
    /// </summary>
    [Fact]
    public void Render_WhenConditionTrue_OutputsTrueBlock()
    {
        var context = CreateContext(("isVip", true));

        Assert.Equal("尊贵会员", _engine.Render("{{if isVip}}尊贵会员{{else}}普通用户{{endif}}", context));
    }

    /// <summary>
    /// 条件为假时输出假值块
    /// </summary>
    [Fact]
    public void Render_WhenConditionFalse_OutputsFalseBlock()
    {
        var context = CreateContext(("isVip", false));

        Assert.Equal("普通用户", _engine.Render("{{if isVip}}尊贵会员{{else}}普通用户{{endif}}", context));
    }

    /// <summary>
    /// 没有 else 分支且条件为假时输出空
    /// </summary>
    [Fact]
    public void Render_WhenConditionFalseWithoutElse_OutputsNothing()
    {
        var context = CreateContext(("isVip", false));

        Assert.Equal("前后", _engine.Render("前{{if isVip}}会员{{endif}}后", context));
    }

    /// <summary>
    /// 空字符串变量在存在性条件下判为假
    /// </summary>
    [Fact]
    public void Render_WhenStringVariableEmpty_ConditionIsFalse()
    {
        var context = CreateContext(("name", string.Empty), ("other", 1));

        Assert.Equal("无名", _engine.Render("{{if name}}有名{{else}}无名{{endif}}", context));
    }

    /// <summary>
    /// 相等条件按字符串比较求值
    /// </summary>
    [Fact]
    public void Render_WithEqualityCondition_ComparesAsString()
    {
        var context = CreateContext(("role", "admin"));

        Assert.Equal("管理员", _engine.Render("{{if role == \"admin\"}}管理员{{else}}访客{{endif}}", context));
    }

    /// <summary>
    /// 不等条件按字符串比较求值
    /// </summary>
    [Fact]
    public void Render_WithInequalityCondition_ComparesAsString()
    {
        var context = CreateContext(("role", "guest"));

        Assert.Equal("访客", _engine.Render("{{if role != \"admin\"}}访客{{else}}管理员{{endif}}", context));
    }

    /// <summary>
    /// 循环按集合元素逐项展开
    /// </summary>
    [Fact]
    public void Render_WithLoop_ExpandsEachItem()
    {
        var context = CreateContext(("items", new List<string> { "甲", "乙", "丙" }));

        Assert.Equal("[甲][乙][丙]", _engine.Render("{{for item in items}}[{{item}}]{{endfor}}", context));
    }

    /// <summary>
    /// 循环变量不存在时整段循环输出为空
    /// </summary>
    [Fact]
    public void Render_WhenLoopSourceMissing_OutputsNothing()
    {
        var context = CreateContext(("other", "值"));

        Assert.Equal("前后", _engine.Render("前{{for item in items}}[{{item}}]{{endfor}}后", context));
    }

    /// <summary>
    /// 循环变量不是集合时整段循环输出为空
    /// </summary>
    [Fact]
    public void Render_WhenLoopSourceNotEnumerable_OutputsNothing()
    {
        var context = CreateContext(("items", 42));

        Assert.Equal("前后", _engine.Render("前{{for item in items}}[{{item}}]{{endfor}}后", context));
    }

    /// <summary>
    /// 循环体内可以使用循环外的变量
    /// </summary>
    [Fact]
    public void Render_LoopBody_CanUseOuterVariables()
    {
        var context = CreateContext(("items", new[] { "甲", "乙" }), ("suffix", "！"));

        Assert.Equal("甲！乙！", _engine.Render("{{for item in items}}{{item}}{{suffix}}{{endfor}}", context));
    }

    /// <summary>
    /// 异步渲染与同步渲染结果一致
    /// </summary>
    [Fact]
    public async Task RenderAsync_MatchesSyncRender()
    {
        const string template = "你好 {{name}}";
        var context = CreateContext(("name", "曦寒"));

        var asyncResult = await _engine.RenderAsync(template, context, TestContext.Current.CancellationToken);

        Assert.Equal(_engine.Render(template, context), asyncResult);
    }

    /// <summary>
    /// 解析原样返回模板源码
    /// </summary>
    [Fact]
    public void Parse_ReturnsSourceUnchanged()
    {
        Assert.Equal("你好 {{name}}", _engine.Parse("你好 {{name}}"));
        Assert.Equal(string.Empty, _engine.Parse(null!));
    }

    /// <summary>
    /// 空模板校验通过
    /// </summary>
    [Fact]
    public void Validate_WhenTemplateEmpty_ReturnsSuccess()
    {
        Assert.True(_engine.Validate(string.Empty).IsValid);
    }

    /// <summary>
    /// 花括号成对的模板校验通过
    /// </summary>
    [Fact]
    public void Validate_WhenBracesBalanced_ReturnsSuccess()
    {
        var result = _engine.Validate("你好 {{name}}，欢迎 {{name}}");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 花括号不成对时报占位符格式错误
    /// </summary>
    [Fact]
    public void Validate_WhenBracesUnbalanced_ReportsPlaceholderError()
    {
        var result = _engine.Validate("你好 {{name}");

        Assert.False(result.IsValid);
        Assert.Contains("占位符", result.ErrorMessage);
    }

    /// <summary>
    /// if 与 endif 数量不匹配时报条件语句错误
    /// </summary>
    [Fact]
    public void Validate_WhenIfWithoutEndif_ReportsConditionalError()
    {
        var result = _engine.Validate("{{if isVip}}会员");

        Assert.False(result.IsValid);
        Assert.Contains("条件语句", result.ErrorMessage);
    }

    /// <summary>
    /// for 与 endfor 数量不匹配时报循环语句错误
    /// </summary>
    [Fact]
    public void Validate_WhenForWithoutEndfor_ReportsLoopError()
    {
        var result = _engine.Validate("{{for item in items}}内容");

        Assert.False(result.IsValid);
        Assert.Contains("循环语句", result.ErrorMessage);
    }

    /// <summary>
    /// 同一份模板源码的校验结果被缓存复用
    /// </summary>
    [Fact]
    public void Validate_SameSourceTwice_ReturnsCachedInstance()
    {
        const string template = "你好 {{name}}，编号 {{id}}";

        var first = _engine.Validate(template);
        var second = _engine.Validate(template);

        // 引用相等才能证明走了缓存分支而不是重新算了一遍
        Assert.Same(first, second);
    }

    /// <summary>
    /// 清空缓存后校验结果不再复用旧实例
    /// </summary>
    [Fact]
    public void ClearTemplateCache_DropsValidationCache()
    {
        const string template = "你好 {{name}}，编号 {{id}}";
        var first = _engine.Validate(template);

        _engine.ClearTemplateCache();
        var second = _engine.Validate(template);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 模板缓存的存取删清语义
    /// </summary>
    [Fact]
    public void TemplateCache_SetGetRemoveClear_BehavesAsExpected()
    {
        Assert.Null(_engine.GetCachedTemplate("key"));

        _engine.SetCachedTemplate("key", "你好 {{name}}");
        Assert.Equal("你好 {{name}}", _engine.GetCachedTemplate("key"));

        _engine.SetCachedTemplate("key", "覆盖后的模板");
        Assert.Equal("覆盖后的模板", _engine.GetCachedTemplate("key"));

        Assert.True(_engine.RemoveCachedTemplate("key"));
        Assert.False(_engine.RemoveCachedTemplate("key"));

        _engine.SetCachedTemplate("key", "再放一个");
        _engine.ClearTemplateCache();
        Assert.Null(_engine.GetCachedTemplate("key"));
    }

    /// <summary>
    /// 缓存渲染在键不存在时返回 null
    /// </summary>
    [Fact]
    public void RenderCached_WhenKeyMissing_ReturnsNull()
    {
        Assert.Null(_engine.RenderCached("missing", CreateContext(("name", "曦寒"))));
    }

    /// <summary>
    /// 缓存渲染在键存在时按缓存模板渲染
    /// </summary>
    [Fact]
    public void RenderCached_WhenKeyPresent_RendersCachedTemplate()
    {
        _engine.SetCachedTemplate("greeting", "你好 {{name}}");

        Assert.Equal("你好 曦寒", _engine.RenderCached("greeting", CreateContext(("name", "曦寒"))));
    }

    /// <summary>
    /// 用变量字典直接渲染
    /// </summary>
    [Fact]
    public void RenderWithVariables_ReplacesPlaceholders()
    {
        var variables = new Dictionary<string, object?> { ["name"] = "曦寒" };

        Assert.Equal("你好 曦寒", _engine.RenderWithVariables("你好 {{name}}", variables));
    }

    /// <summary>
    /// 用对象模型渲染时按属性名替换
    /// </summary>
    [Fact]
    public void RenderWithModel_ReplacesByPropertyName()
    {
        Assert.Equal("你好 曦寒", _engine.RenderWithModel("你好 {{Name}}", new GreetingModel { Name = "曦寒" }));
    }

    /// <summary>
    /// 模型为 null 时模板原样返回
    /// </summary>
    [Fact]
    public void RenderWithModel_WhenModelNull_ReturnsTemplateUnchanged()
    {
        Assert.Equal("你好 {{Name}}", _engine.RenderWithModel("你好 {{Name}}", null));
    }

    /// <summary>
    /// 从文件渲染模板
    /// </summary>
    [Fact]
    public void RenderFile_ReadsFileAndRenders()
    {
        var path = WriteTemplateFile("greeting.txt", "你好 {{name}}");

        Assert.Equal("你好 曦寒", _engine.RenderFile(path, CreateContext(("name", "曦寒"))));
    }

    /// <summary>
    /// 模板文件不存在时抛出文件未找到异常
    /// </summary>
    [Fact]
    public void RenderFile_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_tempDirectory, "not-exists.txt");

        Assert.Throws<FileNotFoundException>(() =>
        {
            _engine.RenderFile(path, new TemplateContext());
        });
    }

    /// <summary>
    /// 异步从文件渲染模板
    /// </summary>
    [Fact]
    public async Task RenderFileAsync_ReadsFileAndRenders()
    {
        var path = WriteTemplateFile("greeting-async.txt", "你好 {{name}}");

        var result = await _engine.RenderFileAsync(path, CreateContext(("name", "曦寒")), null, TestContext.Current.CancellationToken);

        Assert.Equal("你好 曦寒", result);
    }

    /// <summary>
    /// 异步渲染时模板文件不存在同样抛出文件未找到异常
    /// </summary>
    [Fact]
    public async Task RenderFileAsync_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_tempDirectory, "not-exists-async.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _engine.RenderFileAsync(path, new TemplateContext(), null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 渲染结果写入文件时自动创建目标目录
    /// </summary>
    [Fact]
    public void RenderToFile_CreatesOutputDirectory()
    {
        var templatePath = WriteTemplateFile("to-file.txt", "你好 {{name}}");
        var outputPath = Path.Combine(_tempDirectory, "nested", "deep", "output.txt");

        _engine.RenderToFile(templatePath, outputPath, CreateContext(("name", "曦寒")));

        Assert.True(File.Exists(outputPath));
        Assert.Equal("你好 曦寒", File.ReadAllText(outputPath, Encoding.UTF8));
    }

    /// <summary>
    /// 异步渲染结果写入文件时自动创建目标目录
    /// </summary>
    [Fact]
    public async Task RenderToFileAsync_CreatesOutputDirectory()
    {
        var templatePath = WriteTemplateFile("to-file-async.txt", "你好 {{name}}");
        var outputPath = Path.Combine(_tempDirectory, "nested-async", "output.txt");

        await _engine.RenderToFileAsync(
            templatePath,
            outputPath,
            CreateContext(("name", "曦寒")),
            null,
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(outputPath));
        Assert.Equal("你好 曦寒", await File.ReadAllTextAsync(outputPath, Encoding.UTF8, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 文件模板加载后写入缓存，源文件消失也仍可取到
    /// </summary>
    [Fact]
    public void GetOrLoadTemplate_CachesFileContent()
    {
        var path = WriteTemplateFile("cached.txt", "你好 {{name}}");

        var first = _engine.GetOrLoadTemplate("cached", path);
        File.Delete(path);
        var second = _engine.GetOrLoadTemplate("cached", path);

        // 第二次读不到文件也能返回内容，说明命中的是缓存而不是重新读盘
        Assert.Equal("你好 {{name}}", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 异步文件模板加载同样写入缓存
    /// </summary>
    [Fact]
    public async Task GetOrLoadTemplateAsync_CachesFileContent()
    {
        var path = WriteTemplateFile("cached-async.txt", "你好 {{name}}");

        var first = await _engine.GetOrLoadTemplateAsync("cached-async", path, null, TestContext.Current.CancellationToken);
        File.Delete(path);
        var second = await _engine.GetOrLoadTemplateAsync("cached-async", path, null, TestContext.Current.CancellationToken);

        Assert.Equal("你好 {{name}}", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 加载并渲染文件模板
    /// </summary>
    [Fact]
    public void GetOrLoadAndRender_LoadsThenRenders()
    {
        var path = WriteTemplateFile("load-render.txt", "你好 {{name}}");

        var result = _engine.GetOrLoadAndRender("load-render", path, CreateContext(("name", "曦寒")));

        Assert.Equal("你好 曦寒", result);
        Assert.Equal("你好 {{name}}", _engine.GetCachedTemplate("load-render"));
    }

    /// <summary>
    /// 校验文件模板时文件不存在返回失败而不是抛异常
    /// </summary>
    [Fact]
    public void ValidateFile_WhenFileMissing_ReturnsFailure()
    {
        var path = Path.Combine(_tempDirectory, "missing-validate.txt");

        var result = _engine.ValidateFile(path);

        Assert.False(result.IsValid);
        Assert.Contains(path, result.ErrorMessage);
    }

    /// <summary>
    /// 校验合法文件模板返回成功
    /// </summary>
    [Fact]
    public void ValidateFile_WhenTemplateValid_ReturnsSuccess()
    {
        var path = WriteTemplateFile("validate-ok.txt", "你好 {{name}}");

        Assert.True(_engine.ValidateFile(path).IsValid);
    }

    /// <summary>
    /// 用变量字典渲染文件模板
    /// </summary>
    [Fact]
    public void RenderFileWithVariables_ReplacesPlaceholders()
    {
        var path = WriteTemplateFile("file-variables.txt", "你好 {{name}}");
        var variables = new Dictionary<string, object?> { ["name"] = "曦寒" };

        Assert.Equal("你好 曦寒", _engine.RenderFileWithVariables(path, variables));
    }

    /// <summary>
    /// 用对象模型渲染文件模板
    /// </summary>
    [Fact]
    public void RenderFileWithModel_ReplacesByPropertyName()
    {
        var path = WriteTemplateFile("file-model.txt", "你好 {{Name}}");

        Assert.Equal("你好 曦寒", _engine.RenderFileWithModel(path, new GreetingModel { Name = "曦寒" }));
    }

    /// <summary>
    /// 释放资源，清理临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // 清理失败不影响测试结论，忽略
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 构造带变量的模板上下文
    /// </summary>
    /// <param name="variables">变量名与变量值</param>
    /// <returns>模板上下文</returns>
    private static ITemplateContext CreateContext(params (string Name, object? Value)[] variables)
    {
        var context = new TemplateContext();
        foreach (var (name, value) in variables)
        {
            context.SetVariable(name, value);
        }
        return context;
    }

    /// <summary>
    /// 在临时目录写入一个模板文件
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="content">模板内容</param>
    /// <returns>文件完整路径</returns>
    private string WriteTemplateFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// 渲染测试用的模型对象
    /// </summary>
    public sealed class GreetingModel
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
