# XiHan.Framework.ObjectStorage

## 概述
XiHan.Framework.ObjectStorage 提供对象存储能力的统一抽象与集成入口，便于接入不同对象存储服务。

## 核心能力
- 对象存储服务的统一注册与抽象
- 上传、下载与访问策略的扩展点
- 与配置、日志等基础设施协同

## 依赖关系
- 通过 `XiHanObjectStorageModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 对象存储配置通过 Options 类型承载
- 推荐在启动模块中统一配置存储端点与凭据
- 本地 Provider 会校验规范化后的路径位于 `RootPath` 内，并拒绝经过已有符号链接或重解析点的路径；运行期间应限制其他主体修改该目录结构。
- 分片会话内的上传、完成、取消与过期清理会顺序执行；合并文件在所有分片校验并写入后才发布，不同会话可并行处理。

## 使用方式
```csharp
[DependsOn(typeof(XiHanObjectStorageModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义对象存储适配器实现
- 自定义存储策略与访问控制

## 目录结构
```text
XiHan.Framework.ObjectStorage/
  README.md
  XiHanObjectStorageModule.cs
```
