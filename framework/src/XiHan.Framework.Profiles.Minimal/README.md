# XiHan.Framework.Profiles.Minimal

## 关于

`XiHan.Framework.Profiles.Minimal` Minimal 组合包：一个包引用得到原语层、内核、ASP.NET Core 宿主与内存本地化，没有任何业务子系统。适合边缘服务、命令行工具与插件宿主。

## 主要特性

* 内核管道、健康探针与 Problem Details
* 默认语言文案（缺省 `en-US`，内置 10 种区域）

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Profiles.Minimal --prerelease
```

### 使用

`UseMinimalProfile()` 只装配内存本地化，其余能力按需 `UseFeature`。

```csharp
builder.AddXiHan(xihan => xihan
    .UseMinimalProfile()
    .UseFeature<MyFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
```

## 主要类型

此库提供的主要类型：

* `MinimalProfile`：静态类，扩展方法 `UseMinimalProfile`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/profiles-minimal)

## 相关包

* 标准组合：[XiHan.Framework.Profiles.Standard](https://www.nuget.org/packages/XiHan.Framework.Profiles.Standard)
* 完整组合：[XiHan.Framework.Profiles.Full](https://www.nuget.org/packages/XiHan.Framework.Profiles.Full)

## 反馈与贡献

`XiHan.Framework.Profiles.Minimal` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
