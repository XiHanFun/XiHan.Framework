# XiHan.Framework.Templating

基于 Scriban 的企业级模板引擎，提供高性能、安全的模板渲染功能。

## 主要特性

- 🚀 **高性能**: 基于 Scriban 引擎，支持模板预编译和缓存
- 🔒 **安全可靠**: 内置安全检查，防止代码注入和恶意操作
- 🏗️ **模块化设计**: 清晰的抽象层，支持多种模板引擎
- 🎯 **易于使用**: 简洁的 API 设计，支持依赖注入
- 🔧 **功能丰富**: 支持模板继承、片段、变量解析等高级功能

## 快速开始

### 1. 注册服务

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddModule<XiHanTemplatingModule>();
}
```

### 2. 基本使用

```csharp
public class ExampleController : ControllerBase
{
    private readonly ITemplateService _templateService;

    public ExampleController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task<string> RenderTemplate()
    {
        var template = "Hello {{ name }}! Today is {{ date }}.";
        var model = new
        {
            name = "世界",
            date = DateTime.Now.ToString("yyyy-MM-dd")
        };

        return await _templateService.RenderAsync(template, model);
    }
}
```

### 3. 使用变量字典

```csharp
var template = "用户 {{ user.name }} 的年龄是 {{ user.age }} 岁";
var variables = new Dictionary<string, object?>
{
    ["user"] = new { name = "张三", age = 25 }
};

var result = await _templateService.RenderAsync(template, variables);
```

### 4. 文件模板渲染

```csharp
// 从文件渲染模板
var result = await _templateService.RenderFileAsync("templates/email.html", model);
```

## 高级功能

### 模板继承

```html
<!-- layout.html -->
<!DOCTYPE html>
<html>
  <head>
    <title>{{ title }}</title>
  </head>
  <body>
    {{- block content -}}{{- endblock -}}
  </body>
</html>

<!-- page.html -->
{{- extends "layout" -}} {{- block content -}}
<h1>欢迎 {{ user.name }}</h1>
{{- endblock -}}
```

### 模板片段

```html
<!-- 注册片段 -->
{{ partial "user-card" user }}

<!-- user-card 片段 -->
<div class="user-card">
  <h3>{{ name }}</h3>
  <p>{{ email }}</p>
</div>
```

### 自定义函数

```csharp
var contextBuilder = _contextFactory.CreateBuilder();
contextBuilder.AddFunction("format_date", (DateTime date) => date.ToString("yyyy年MM月dd日"));

var context = contextBuilder.Build();
var template = "今天是 {{ format_date(now) }}";
```

### 安全检查

```csharp
var securityResult = _templateService.ValidateTemplate(templateSource);
if (!securityResult.IsValid)
{
    // 处理安全问题
    foreach (var threat in securityResult.Threats)
    {
        Console.WriteLine($"安全威胁: {threat.Description}");
    }
}
```

## 配置选项

```csharp
services.Configure<TemplatingOptions>(options =>
{
    options.DefaultEngine = "Scriban";
    options.EnableCaching = true;
    options.CacheExpiration = TimeSpan.FromMinutes(30);
    options.EnableSecurityChecks = true;
    options.TemplateRootDirectory = "templates";
});
```

## 扩展方法

```csharp
// 使用扩展方法简化调用
var result = await template.RenderAsync(model, serviceProvider);

// 同步渲染
var result = template.Render(model, serviceProvider);

// 验证模板
var isValid = template.Validate(serviceProvider).IsValid;
```

## 架构设计

本模块遵循清晰的分层架构：

- **抽象层** (`Abstractions/`): 定义核心接口和契约
- **实现层** (`Implementations/`): 基于 Scriban 的具体实现
- **服务层** (`Services/`): 高级业务服务
- **扩展层** (`Extensions/`): 便利的扩展方法

## 性能优化

- 模板预编译和缓存
- 变量解析优化
- 安全检查缓存
- 内存管理优化

## 安全特性

- 代码注入防护
- 文件访问控制
- 反射操作限制
- 自定义安全策略

## 许可证

本项目基于 MIT 许可证开源。
