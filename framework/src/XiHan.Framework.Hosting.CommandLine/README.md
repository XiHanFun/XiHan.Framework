# XiHan.Framework.Hosting.CommandLine

## 关于

`XiHan.Framework.Hosting.CommandLine` 是命令行宿主：在通用宿主里把命令行参数翻译成内核调用，一份操作契约在命令行上也能服务。适合运维工具与批处理脚本。

## 主要特性

* 内置 `help`、`graph`、`pipeline`、`operations`、`invoke` 命令
* `invoke` 的字段选项、`--json`、`--data` 拼成载荷，经完整内核管道与授权
* 成功输出 JSON 到 stdout，失败输出错误到 stderr，退出码按错误种类
* 可实现 `ICommand` 扩展自定义命令

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Hosting.CommandLine --prerelease
```

### 使用

在通用宿主里启用：

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan.UseMinimalProfile().UseFeature<GreetingsFeature>());
builder.Services.AddXiHanCommandLine();
return await builder.Build().RunXiHanCommandLineAsync(args);
```

```bash
app operations                             # 登记表
app invoke greetings.create --name Ada     # 字段选项拼成载荷
app greetings create --name Ada            # <resource> <verb> 是 invoke 的简写
```

`GreetingsFeature` 是你自己的能力包。

## 主要类型

此库提供的主要类型：

* `AddXiHanCommandLine`、`RunXiHanCommandLineAsync`：启用与运行入口
* `ICommand`、`AddXiHanCommand<T>`：自定义命令

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/hosting-commandline)

## 相关包

* ASP.NET Core 宿主：[XiHan.Framework.Hosting.AspNetCore](https://www.nuget.org/packages/XiHan.Framework.Hosting.AspNetCore)
* 命令行工具：[XiHan.Framework.Cli](https://www.nuget.org/packages/XiHan.Framework.Cli)

## 反馈与贡献

`XiHan.Framework.Hosting.CommandLine` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
