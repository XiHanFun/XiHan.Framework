# XiHan.Framework.Localization.InMemory

## 关于

`XiHan.Framework.Localization.InMemory` 是本地化的默认实现：汇集容器里的全部消息目录，按回退链取模板并格式化，并让 `key.L()` 在任何地方可用。文案随程序集发布，改译文就重新发布。

## 主要特性

* 回退链：精确区域、同语言、默认区域、`en-US`
* 占位符按命中区域的文化格式化
* 找不到键或占位符缺值时退回键与参数的描述

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Localization.InMemory --prerelease
```

### 使用

`Profiles.Minimal` 与 `Profiles.Standard` 已装配本包；只用内核时显式加入：

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryLocalizationFeature>());
```

```json
{ "XiHan": { "Localization": { "DefaultLocale": "en-US", "Locales": "en-US,zh-CN" } } }
```

## 主要类型

此库提供的主要类型：

* `InMemoryLocalizationFeature`：特性入口
* `MessageLocalizer`：同时实现 `IMessageLocalizer` 与 `IMessageResolver`，测试里可直接创建

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/localization-inmemory)

## 相关包

* 本地化契约：[XiHan.Framework.Localization.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Localization.Abstractions)
* 数据库实现：[XiHan.Framework.Localization.Database](https://www.nuget.org/packages/XiHan.Framework.Localization.Database)

## 反馈与贡献

`XiHan.Framework.Localization.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
