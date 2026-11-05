# XiHan.Framework.Localization.Conformance

## 关于

`XiHan.Framework.Localization.Conformance` 是本地化的一致性测试套件，给写本地化提供程序的人验证实现用，不是业务代码的依赖。

## 主要特性

* 回退链与按命中区域的文化格式化
* 缺模板或缺参数时退回描述
* 错误按键与细节翻译、按调用上下文的区域

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Localization.Conformance --prerelease
```

### 使用

在提供程序的测试项目里继承 `MessageLocalizerConformance` 并实现 `CreateLocalizer`，全部用例随之执行；实现同时必须是 `IMessageResolver`：

```csharp
public sealed class MyLocalizerConformanceTests : MessageLocalizerConformance
{
    protected override IMessageLocalizer CreateLocalizer(
        IReadOnlyList<MessageCatalog> catalogs, LocalizationOptions options)
        => new MyMessageLocalizer(catalogs, options);
}
```

## 主要类型

此库提供的主要类型：

* `MessageLocalizerConformance`：本地化器的一致性基类

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 本地化契约：[XiHan.Framework.Localization.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Localization.Abstractions)
* 内存实现：[XiHan.Framework.Localization.InMemory](https://www.nuget.org/packages/XiHan.Framework.Localization.InMemory)
* 数据库实现：[XiHan.Framework.Localization.Database](https://www.nuget.org/packages/XiHan.Framework.Localization.Database)

## 反馈与贡献

`XiHan.Framework.Localization.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
