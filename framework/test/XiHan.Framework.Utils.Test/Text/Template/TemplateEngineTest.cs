using XiHan.Framework.Utils.Text.Template;

namespace XiHan.Framework.Utils.Test.Text.Template;

/// <summary>
/// 模板引擎测试
/// </summary>
public class TemplateEngineTest
{
    #region 基础模板渲染测试

    [Fact(DisplayName = "基本变量替换测试")]
    public void Render_WithDictionary_ShouldReplaceVariables()
    {
        // Arrange
        var template = "Hello, {{name}}! Your age is {{age}}.";
        var values = new Dictionary<string, object?>
        {
            { "name", "张三" },
            { "age", 30 }
        };

        // Act
        var result = TemplateEngine.Render(template, values);

        // Assert
        Assert.Equal("Hello, 张三! Your age is 30.", result);
    }

    [Fact(DisplayName = "使用对象模型渲染测试")]
    public void Render_WithObjectModel_ShouldReplaceVariables()
    {
        // Arrange
        var template = "Hello, {{Name}}! Your age is {{Age}}.";
        var model = new TestPerson
        {
            Name = "李四",
            Age = 25
        };

        // Act
        var result = TemplateEngine.Render(template, model);

        // Assert
        Assert.Equal("Hello, 李四! Your age is 25.", result);
    }

    [Fact(DisplayName = "空模板测试")]
    public void Render_WithEmptyTemplate_ShouldReturnEmpty()
    {
        // Arrange
        var template = "";
        var values = new Dictionary<string, object?>
        {
            { "name", "王五" }
        };

        // Act
        var result = TemplateEngine.Render(template, values);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact(DisplayName = "空数据测试")]
    public void Render_WithEmptyValues_ShouldReturnOriginalTemplate()
    {
        // Arrange
        var template = "Hello, {{name}}!";
        var values = new Dictionary<string, object?>();

        // Act
        var result = TemplateEngine.Render(template, values);

        // Assert
        Assert.Equal(template, result);
    }

    #endregion 基础模板渲染测试

    #region 高级模板渲染测试

    [Fact(DisplayName = "条件语句测试-True分支")]
    public void RenderAdvanced_WithTrueCondition_ShouldRenderTrueBranch()
    {
        // Arrange
        var template = "{{if isActive}}用户活跃{{else}}用户不活跃{{endif}}";
        var values = new Dictionary<string, object?>
        {
            { "isActive", true }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("用户活跃", result);
    }

    [Fact(DisplayName = "条件语句测试-False分支")]
    public void RenderAdvanced_WithFalseCondition_ShouldRenderFalseBranch()
    {
        // Arrange
        var template = "{{if isActive}}用户活跃{{else}}用户不活跃{{endif}}";
        var values = new Dictionary<string, object?>
        {
            { "isActive", false }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("用户不活跃", result);
    }

    [Fact(DisplayName = "条件语句测试-无Else分支")]
    public void RenderAdvanced_WithFalseConditionNoElse_ShouldRenderEmpty()
    {
        // Arrange
        var template = "{{if isActive}}用户活跃{{endif}}";
        var values = new Dictionary<string, object?>
        {
            { "isActive", false }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("", result);
    }

    [Fact(DisplayName = "条件语句测试-字符串比较相等")]
    public void RenderAdvanced_WithEqualStringComparison_ShouldRenderTrueBranch()
    {
        // Arrange
        var template = "{{if role == \"admin\"}}管理员{{else}}普通用户{{endif}}";
        var values = new Dictionary<string, object?>
        {
            { "role", "admin" }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("管理员", result);
    }

    [Fact(DisplayName = "条件语句测试-字符串比较不等")]
    public void RenderAdvanced_WithNotEqualStringComparison_ShouldRenderFalseBranch()
    {
        // Arrange
        var template = "{{if role != \"admin\"}}普通用户{{else}}管理员{{endif}}";
        var values = new Dictionary<string, object?>
        {
            { "role", "user" }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("普通用户", result);
    }

    [Fact(DisplayName = "循环语句测试")]
    public void RenderAdvanced_WithForLoop_ShouldRenderItems()
    {
        // Arrange
        var template = "用户列表：{{for user in users}}{{user.Name}}，{{endfor}}";
        var users = new List<TestPerson>
        {
            new() { Name = "张三", Age = 30 },
            new() { Name = "李四", Age = 25 },
            new() { Name = "王五", Age = 28 }
        };
        var values = new Dictionary<string, object?>
        {
            { "users", users }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("用户列表：张三，李四，王五，", result);
    }

    [Fact(DisplayName = "嵌套循环和条件测试")]
    public void RenderAdvanced_WithNestedLoopAndCondition_ShouldRenderCorrectly()
    {
        // Arrange
        var template = "成年用户：{{for user in users}}{{if user.Age >= 18}}{{user.Name}}({{user.Age}})，{{endif}}{{endfor}}";
        var users = new List<TestPerson>
        {
            new() { Name = "张三", Age = 30 },
            new() { Name = "小明", Age = 15 },
            new() { Name = "李四", Age = 25 }
        };
        var values = new Dictionary<string, object?>
        {
            { "users", users }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("成年用户：张三(30)，李四(25)，", result);
    }

    [Fact(DisplayName = "空列表循环测试")]
    public void RenderAdvanced_WithEmptyList_ShouldRenderEmpty()
    {
        // Arrange
        var template = "用户列表：{{for user in users}}{{user.Name}}，{{endfor}}";
        var users = new List<TestPerson>();
        var values = new Dictionary<string, object?>
        {
            { "users", users }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("用户列表：", result);
    }

    [Fact(DisplayName = "简单数组循环测试")]
    public void RenderAdvanced_WithSimpleArray_ShouldRenderItems()
    {
        // Arrange
        var template = "数字：{{for num in numbers}}{{num}}，{{endfor}}";
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var values = new Dictionary<string, object?>
        {
            { "numbers", numbers }
        };

        // Act
        var result = TemplateEngine.RenderAdvanced(template, values);

        // Assert
        Assert.Equal("数字：1，2，3，4，5，", result);
    }

    #endregion 高级模板渲染测试

    #region 辅助类

    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    #endregion 辅助类
}
