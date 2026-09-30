# XiHanModuleLib

## 概述
XiHanModuleLib 是基于 XiHan.Framework 模块系统的功能模块，由 `xihan-module` 模板生成。

## 核心能力
- `IFeatureNameService`：示例服务接口，默认实现为 `DefaultFeatureNameService`
- `FeatureNameOptions`：模块配置选项

## 依赖关系
- 通过 `FeatureNameModule` 参与模块化生命周期
- 依赖 `XiHan.Framework.Core`；需要其他框架能力时在模块类上追加 `[DependsOn(typeof(...))]` 并引用对应包

## 配置与约定
- 配置节：`FeatureName`（见 `FeatureNameOptions.SectionName`）

```json
{
  "FeatureName": {
    "GreetingPrefix": "你好"
  }
}
```

- 模块类只做装配：`ConfigureServices` 调用 `services.AddFeatureName(configuration)`，服务注册写在 `Extensions/DependencyInjection/FeatureNameServiceCollectionExtensions.cs`

## 使用方式
在宿主的启动模块上声明依赖：

```csharp
[DependsOn(typeof(FeatureNameModule))]
public class MyHostModule : XiHanModule
{
}
```

之后即可注入 `IFeatureNameService`。

## 扩展点
- 在调用 `AddFeatureName` 之前注册自己的 `IFeatureNameService` 实现即可替换默认实现（默认实现以 `TryAdd` 注册）

## 目录结构
```text
XiHanModuleLib/
  Extensions/DependencyInjection/FeatureNameServiceCollectionExtensions.cs
  Options/FeatureNameOptions.cs
  Services/IFeatureNameService.cs
  Services/DefaultFeatureNameService.cs
  FeatureNameModule.cs
  README.md
```
