# XiHan.Framework.Authentication

曦寒框架认证模块，提供完整的身份认证功能。

## 功能特性

### 1. 密码管理

- **密码哈希**: 使用 PBKDF2 算法进行安全的密码哈希
- **密码强度检查**: 支持自定义密码策略
- **密码策略**: 可配置的密码规则（长度、大小写、特殊字符等）

### 2. JWT Token 认证

- **Token 生成**: 支持访问令牌和刷新令牌
- **Token 验证**: 完整的 Token 验证功能
- **Token 刷新**: 支持使用刷新令牌更新访问令牌

### 3. OTP 双因素认证

- **TOTP 支持**: 基于时间的一次性密码
- **HOTP 支持**: 基于计数器的一次性密码
- **恢复码**: 备用恢复码生成和验证

## 快速开始

### 1. 配置服务

在 `appsettings.json` 中配置认证选项：

```json
{
  "Authentication": {
    "PasswordHasher": {
      "Iterations": 600000,
      "SaltSize": 32,
      "HashSize": 32
    },
    "PasswordPolicy": {
      "MinimumLength": 8,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireDigit": true,
      "RequireSpecialCharacter": true,
      "MaxFailedAccessAttempts": 5,
      "LockoutDurationMinutes": 30
    },
    "Jwt": {
      "SecretKey": "your-secret-key-here-at-least-32-characters",
      "Issuer": "XiHanFramework",
      "Audience": "XiHanFrameworkUsers",
      "AccessTokenExpirationMinutes": 60,
      "RefreshTokenExpirationDays": 7
    },
    "Otp": {
      "Digits": 6,
      "TimeStep": 30,
      "AllowedSkew": 1
    }
  }
}
```

### 2. 注册服务

模块会自动注册基础服务，你只需要实现 `IAuthenticationService` 接口：

```csharp
public class MyAuthenticationService : IAuthenticationService
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOtpService _otpService;

    // 实现接口方法...
}

// 在启动时注册
services.AddScoped<IAuthenticationService, MyAuthenticationService>();
```

### 3. 使用示例

#### 密码哈希和验证

```csharp
public class UserService
{
    private readonly IPasswordHasher _passwordHasher;

    public async Task CreateUser(string username, string password)
    {
        // 哈希密码
        var hashedPassword = _passwordHasher.HashPassword(password);

        // 保存用户...
    }

    public async Task<bool> ValidatePassword(string hashedPassword, string providedPassword)
    {
        // 验证密码
        return _passwordHasher.VerifyPassword(hashedPassword, providedPassword);
    }
}
```

#### JWT Token 生成

```csharp
public class AuthService
{
    private readonly IJwtTokenService _jwtTokenService;

    public async Task<JwtTokenResult> Login(string username, string password)
    {
        // 验证用户凭据...

        // 生成 Token
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "User")
        };

        return _jwtTokenService.GenerateAccessToken(claims);
    }
}
```

#### OTP 双因素认证

```csharp
public class TwoFactorService
{
    private readonly IOtpService _otpService;

    public async Task<TwoFactorSetupResult> EnableTwoFactor(string userId, string issuer, string account)
    {
        // 生成密钥
        var secret = _otpService.GenerateTotpSecret();

        // 生成二维码 URI
        var qrCodeUri = _otpService.GenerateTotpUri(secret, issuer, account);

        // 生成恢复码
        var recoveryCodes = _otpService.GenerateRecoveryCodes();

        return new TwoFactorSetupResult
        {
            Secret = secret,
            QrCodeUri = qrCodeUri,
            RecoveryCodes = recoveryCodes
        };
    }

    public async Task<bool> VerifyTwoFactor(string secret, string code)
    {
        return _otpService.VerifyTotpCode(secret, code);
    }
}
```

## 安全建议

1. **密钥管理**:

   - 使用环境变量或密钥管理服务存储敏感信息
   - JWT 密钥至少 32 字符

2. **密码策略**:

   - 建议最小长度 8 位
   - 要求大小写、数字和特殊字符
   - 设置账户锁定策略

3. **Token 管理**:

   - 访问令牌有效期不宜过长（建议 15-60 分钟）
   - 刷新令牌应安全存储
   - 实现 Token 黑名单机制

4. **双因素认证**:
   - 为敏感操作启用 2FA
   - 妥善保存恢复码
   - 定期更新密钥

## 依赖项

- XiHan.Framework.Core
- XiHan.Framework.Security
- XiHan.Framework.Utils
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt

## 许可证

MIT License - 详见 LICENSE 文件
