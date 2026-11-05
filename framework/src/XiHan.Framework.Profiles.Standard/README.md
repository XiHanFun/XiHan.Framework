# XiHan.Framework.Profiles.Standard

## 关于

`XiHan.Framework.Profiles.Standard` Standard 组合包：Minimal 加上 `Domain` 与持久化、缓存、多租户、安全、消息五个子系统的抽象和内存默认实现。大多数业务服务从这里开始，零外部基础设施即可运行。

## 主要特性

* `UseStandardProfile()` 一次装配五个内存默认实现
* 再 `UseFeature` 一个显式 Provider（如 Redis 缓存、JWT 安全）即顶替对应默认实现
* 内存实现进程重启即丢数据，上线前按需换 Provider

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Profiles.Standard --prerelease
```

### 使用

装配组合，再加上业务能力包与需要的协议适配器。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OutboxFeature>());   // 可选：事务性发件箱

var app = builder.Build();
app.UseXiHan();
app.MapXiHanOperations();
```

## 主要类型

此库提供的主要类型：

* `StandardProfile`：静态类，扩展方法 `UseStandardProfile`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/profiles-standard)

## 相关包

* 最小组合：[XiHan.Framework.Profiles.Minimal](https://www.nuget.org/packages/XiHan.Framework.Profiles.Minimal)
* 完整组合：[XiHan.Framework.Profiles.Full](https://www.nuget.org/packages/XiHan.Framework.Profiles.Full)

## 反馈与贡献

`XiHan.Framework.Profiles.Standard` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
