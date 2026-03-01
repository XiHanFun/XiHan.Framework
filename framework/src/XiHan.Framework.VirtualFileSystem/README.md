# XiHan.Framework.VirtualFileSystem

## 概述
XiHan.Framework.VirtualFileSystem 提供虚拟文件系统能力，统一文件资源的访问、组织与替换策略。

## 核心能力
- 虚拟文件系统的统一抽象与注册
- 文件资源定位与访问的基础能力
- 与模板、对象存储等模块协同

## 依赖关系
- 通过 `XiHanVirtualFileSystemModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 文件系统配置通过 Options 类型承载
- 建议在启动模块统一配置资源路径与加载策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanVirtualFileSystemModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义文件系统提供者
- 自定义文件资源映射与替换策略

## 目录结构
```text
XiHan.Framework.VirtualFileSystem/
  README.md
  XiHanVirtualFileSystemModule.cs
```
