# XiHan.Framework.Authentication

## 概述
XiHan.Framework.Authentication 提供认证相关的基础能力与策略支持，封装认证流程、凭据验证与认证结果模型。

## 核心能力
- 认证流程与结果模型的统一抽象
- 用户凭据验证策略与安全策略扩展
- 第三方登录：Google / GitHub / Gitee / QQ / 微信 / 企业微信 / 飞书 / 钉钉，账号授权与扫码登录两种方式
- 与授权、日志等基础设施协同

## 第三方登录
配置节 `XiHan:Authentication:OAuth`，`Providers[]` 逐条注册成独立的 AuthenticationScheme：

```json
"Providers": [
  { "Name": "wechat-qr", "Provider": "wechat", "Mode": "QrCode",  "ClientId": "开放平台网站应用 AppId", "ClientSecret": "..." },
  { "Name": "wechat-mp", "Provider": "wechat", "Mode": "Account", "ClientId": "公众号 AppId",          "ClientSecret": "..." }
]
```

`Name` 是方案名（决定回调路径 `/signin-{Name}`），`Provider` 是提供商类型，`Mode` 选账号授权还是扫码登录。
同一家开两种方式就写两条：`Name` 不同、`Provider` 相同。绑定关系的读写走 `IExternalLoginStore`。

八家处理器全部自研，共用基类 `XiHanOAuthHandler<TOptions>`，本包不引入任何第三方 OAuth 提供商包。
新增一家：写一个继承 `XiHanOAuthProviderOptions` 的选项类，按需覆写处理器方法，再在 `RegisterProvider` 加一个分支。

## 依赖关系
- 通过 `XiHanAuthenticationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 认证策略与配置通过 Options 类型承载
- 推荐在启动模块中统一配置认证参数与策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanAuthenticationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义认证服务实现
- 自定义认证策略与凭据验证流程

## 目录结构
```text
XiHan.Framework.Authentication/
  README.md
  XiHanAuthenticationModule.cs
```
