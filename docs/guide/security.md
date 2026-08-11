# 数据加解密

密码怎么存、第三方凭证怎么落库还能还原、报文怎么防篡改——这三件事在框架里分属三套机制。选错了要么不安全，要么解不回来。

完整 API 清单见 [Security 包](../packages/security) 与 [Utils 包](../packages/utils)。

## 先分清三类需求

| 需求 | 特征 | 用什么 |
| --- | --- | --- |
| 用户密码 | 只需比对，**永远不需要还原** | `IPasswordHasher`（PBKDF2 单向哈希） |
| 第三方凭证（AK/SK、连接串、机器人 Token） | 调用时**必须拿到明文** | ASP.NET Core `IDataProtector` 可逆加密 |
| 报文完整性 | 不隐藏内容，只防篡改与重放 | HMAC / RSA / SM2 签名 |

::: danger 别把密码做成可逆加密
可逆加密意味着拿到密钥就能批量还原所有用户密码。密码一律走 `IPasswordHasher`；反过来，AK/SK 这类调用时要用的密钥不能哈希，否则永远拿不回来。
:::

## 能力分布

| 能力 | 类型 | 所在包 |
| --- | --- | --- |
| 密码哈希 / 密码策略 / 历史复用 | `IPasswordHasher`、`IPasswordPolicyService`、`IPasswordHistoryStore` | `XiHan.Framework.Security` |
| 国密 SM2 签名、SM4 对称加密 | `Sm2Helper`、`Sm4Helper` | `XiHan.Framework.Security` |
| Blowfish 对称加密 | `BlowfishHelper` | `XiHan.Framework.Security` |
| AES / DES 对称加密 | `AesHelper`、`DesHelper` | `XiHan.Framework.Utils` |
| RSA / DSA / ECDSA / ECIES | `RsaHelper`、`DsaHelper`、`EcdsaHelper`、`EciesHelper` | `XiHan.Framework.Utils` |
| 摘要与消息认证码 | `HashHelper`、`HmacHelper` | `XiHan.Framework.Utils` |
| 开放接口验签与报文加解密 | `XiHanOpenApiSecurityMiddleware` | `XiHan.Framework.Web.Api` |

::: tip 加密辅助类都是静态类
`AesHelper` / `Sm4Helper` / `RsaHelper` 这些不走 DI，引用包后直接静态调用，密钥从哪来、放哪里由调用方负责。只有密码相关的三个服务是注入的。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security
```

```csharp
[DependsOn(typeof(XiHanSecurityModule))]
public class MyModule : XiHanModule { }
```

模块的 `ConfigureServices` 调用 `AddXiHanSecurityServices(configuration)`，登记内容如下：

| 登记项 | 生命周期 | 说明 |
| --- | --- | --- |
| `PasswordHasherOptions` | — | 绑定配置节 `XiHan:Authentication:PasswordHasher` |
| `PasswordPolicyOptions` | — | 绑定配置节 `XiHan:Authentication:PasswordPolicy` |
| `IPasswordHasher` → `PasswordHasher` | `TryAddSingleton` | PBKDF2 哈希器 |
| `IPasswordPolicyService` → `PasswordPolicyService` | `TryAddScoped` | 强度校验 + 历史复用 |
| `IPasswordHistoryStore` → `DefaultPasswordHistoryStore` | `TryAddScoped` | 进程内存实现 |

全部用 `TryAdd*`，所以应用层只要**先于**本模块注册自己的实现就能覆盖。

## 密码存储

### 哈希与校验

```csharp
public class AccountService(IPasswordHasher hasher, IPasswordPolicyService policy)
{
    public async Task<string> RegisterAsync(long userId, string password)
    {
        var check = policy.Validate(password);
        if (!check.IsValid)
        {
            throw new InvalidOperationException(string.Join("；", check.Errors));
        }

        if (await policy.IsPasswordReusedAsync(password, userId, historyCount: 5))
        {
            throw new InvalidOperationException("不能复用最近使用过的密码");
        }

        return hasher.HashPassword(password);
    }

    public bool Login(string storedHash, string input)
    {
        return hasher.VerifyPassword(storedHash, input);
    }
}
```

### 哈希串是自描述的

`HashPassword` 的返回值是五段冒号分隔的字符串：

```text
version : iterations : hashAlgorithm : salt(Base64) : hash(Base64)
1:600000:SHA256:qw3f…:8Kd2…
```

校验时从串里**读回当时的参数**重算，而不是用当前配置——所以调整参数不会让存量密码失效。比对用 `CryptographicOperations.FixedTimeEquals`，恒定时间，避免时序侧信道。

默认参数：PBKDF2-SHA256、600000 次迭代、32 字节随机盐、32 字节输出。

### 参数升级靠 NeedsRehash

```csharp
if (hasher.VerifyPassword(user.PasswordHash, input))
{
    if (hasher.NeedsRehash(user.PasswordHash))
    {
        user.PasswordHash = hasher.HashPassword(input);   // 用新参数重算并落库
    }
}
```

登录成功那一刻是唯一能拿到明文的时机，重哈希只能在这里做。

::: warning NeedsRehash 只比较三项
它比对的是 `Version`、`Iterations`、`HashAlgorithm`。只改 `SaltSize` 或 `HashSize` **不会**触发重哈希——要让存量密码跟着走，必须同时把 `Version` 加一。

另外 `HashAlgorithm` 只识别 `MD5` / `SHA1` / `SHA256` / `SHA384` / `SHA512`，配了别的值会静默回落到 SHA256。
:::

### 密码策略

`IPasswordPolicyService.Validate` 依次检查：长度上下限、大小写/数字/特殊字符、30 条内置弱密码黑名单、`CustomBlacklist` 子串命中、连续 3 个相同字符、连续 3 位字母或数字序列（`abc`、`321` 都算）。

评分构成（`PasswordValidationResult.Score`）：

| 项 | 分值 |
| --- | --- |
| 超出最小长度的部分 | 每字符 1 分，最多 10 |
| 含大写 / 小写 / 数字 | 各 10 |
| 含特殊字符 | 15 |
| 命中内置黑名单 | −30 |
| 命中自定义黑名单 | −20 |
| 重复字符 / 连续序列 | 各 −10 |

分值下限为 0，实际能拿到的上限是 55——它是相对强度参考，不要拿去当百分比展示。

::: warning 历史复用检查传的是明文
```csharp
await policy.IsPasswordReusedAsync(newPassword, userId, historyCount, ct);
```
第一个参数是**新密码明文**。历史里存的是加盐哈希，同一明文每次哈希结果都不同，只能用 `VerifyPassword(历史哈希, 明文)` 逐条比对，不能直接比字符串。
:::

::: danger 默认历史存储是内存的，而且不会自动写入
`DefaultPasswordHistoryStore` 用静态 `ConcurrentDictionary` 存在进程里，重启即失、多实例不共享。

更要注意：`IPasswordHistoryStore` 接口**只有读方法** `GetRecentPasswordHashesAsync`。写入靠 `DefaultPasswordHistoryStore.RecordPassword(userId, passwordHash, maxHistoryCount)` 这个静态方法，框架不会在改密成功后自动调用它。不接管的话历史队列恒空，`IsPasswordReusedAsync` 永远返回 `false`。

生产做法：实现 `IPasswordHistoryStore` 读数据库最近 N 条哈希，并在改密流程里自己写一条历史。
:::

## 对称加密

| 辅助类 | 密钥要求 | 模式 | 密文形态 |
| --- | --- | --- | --- |
| `Sm4Helper` | 恰好 16 字节 UTF-8 | CBC + PKCS7 | `IV(16) + 密文`，整体 Base64 |
| `BlowfishHelper` | ≤ 56 字节 UTF-8 | CBC + PKCS7 | `IV(8) + 密文`，整体 Base64 |
| `AesHelper` | 见下方说明 | CBC | 纯密文 Base64 |
| `DesHelper` | 8 字节 | CBC | 纯密文 Base64 |

`Sm4Helper` 与 `BlowfishHelper` 每次加密都生成随机 IV 并前置拼在密文头部，解密时自动读回——相同明文加相同密钥不会产生相同密文，直接用即可：

```csharp
var cipher = Sm4Helper.Encrypt("需要保护的内容", "0123456789abcdef");  // key 必须 16 字节
var plain = Sm4Helper.Decrypt(cipher, "0123456789abcdef");
```

::: warning AesHelper 的单口令重载是确定性的
`AesHelper.Encrypt(plainText, password)` 用**全零盐**从口令 PBKDF2 派生 Key 与 IV，因此相同明文 + 相同口令永远得到相同密文，能被比对和字典攻击。

要随机 IV 就用三参重载 `Encrypt(plainText, key, iv)` 自己管理：`key` 必须是 16/24/32 字节的 UTF-8 字符串，`iv` 必须是 16 字节，每条数据换一个 IV 并随密文一起存。
:::

::: danger DES 只用于对接遗留系统
`DesHelper.Encrypt(plainText)` / `Decrypt(encryptedText)` 的单参重载用的是**写死在代码里的密钥和 IV**，等同于没有加密。DES 本身 56 位有效密钥也早已不具备强度，新数据一律不要用。
:::

## 非对称加密与签名

### RSA

```csharp
// 签名：默认 SHA256 + PKCS#1 v1.5
var signature = RsaHelper.SignData(payload, privateKey);
var ok = RsaHelper.VerifyData(payload, signature, publicKey);

// 加密大数据：RSA 包 AES 密钥的混合模式
var cipher = RsaHelper.EncryptWithAes(bigText, publicKey);
var plain = RsaHelper.DecryptWithAes(cipher, privateKey);
```

| 方法族 | 默认值 | 适用 |
| --- | --- | --- |
| `Encrypt` / `Decrypt` | 填充 `OaepSHA256`，自动分段 | 小数据；密文体积膨胀明显 |
| `EncryptWithAes` / `DecryptWithAes` | AES-256-CBC 数据 + RSA-OAEP-SHA256 包密钥 | 大文本、文件、高频加解密 |
| `SignData` / `VerifyData` | `SHA256` + `RSASignaturePadding.Pkcs1` | 报文签名 |

密钥长度低于 2048 位或不是 8 的倍数，`GenerateKeys` 直接抛 `ArgumentException`。混合密文的结构是 `[版本 1 字节][RSA(AESKey) 长度 2 字节][RSA(AESKey)][RSA(AESIV) 长度 2 字节][RSA(AESIV)][AES(Data)]`，版本号不为 1 会拒绝解密。

::: danger 公钥必须是 SubjectPublicKeyInfo 格式
`RsaHelper` 内部用 `ImportSubjectPublicKeyInfo` 导入公钥（即 X.509 公钥、PEM 里的 `-----BEGIN PUBLIC KEY-----`），而 `GenerateKeys()` / `ExportPublicKeyFromPrivateKey()` 导出的是 **PKCS#1** 编码——两者不通用，把生成的公钥直接喂给 `Encrypt` / `VerifyData` 会解析失败。

私钥没有这个问题：导入时先试 PKCS#8、失败再退回 PKCS#1，两种都能吃。

要一对能直接用的密钥，自己导出 SPKI：

```csharp
using var rsa = RSA.Create(2048);
var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
```
:::

### 国密 SM2 / SM4

`Sm2Helper` 基于 BouncyCastle，曲线 `sm2p256v1`，签名算法 `SM3WITHSM2`（SM3 摘要在签名器内部完成，不单独暴露 SM3 辅助类）：

```csharp
var ok = Sm2Helper.VerifyData(canonicalString, signatureBase64, publicKeyBase64);
```

::: danger SM2 的密钥格式同样要对齐
`SignData` 把私钥 Base64 解码后**直接当作标量 d**，`VerifyData` 走 `curve.DecodePoint(...)` 把公钥当作**曲线点编码**（未压缩点以 `0x04` 开头）。而 `GenerateKeys()` 导出的是 PKCS#8 / SubjectPublicKeyInfo 的 DER——格式不同，不能直接互喂。

对接国密时以对方给的原始 d 值与曲线点为准，别用 `GenerateKeys()` 的输出去签、再拿它的公钥去验。
:::

国密全链路的分工：SM2 负责签名验签，SM4 负责对称加密，SM3 摘要由 `SM3WITHSM2` 内部承担。

### 其他非对称算法

`DsaHelper` / `EcdsaHelper` 提供 `GenerateKeys` / `SignData` / `VerifyData`，`EciesHelper` 提供基于椭圆曲线的 `Encrypt` / `Decrypt`。签名场景优先 ECDSA 或 SM2，DSA 仅用于兼容既有系统。

## 哈希与消息认证码

```csharp
HashHelper.Sha256("content");        // 大写十六进制
HashHelper.StreamMd5(stream);        // 流/文件校验，SHA256 版本是 StreamHash
HmacHelper.HmacSha256(key, message); // Base64
```

`HashHelper` 的所有方法返回 `Convert.ToHexString` 的**大写**十六进制串，比对时注意大小写；`HmacHelper` 的字符串重载返回 Base64，字节重载 `ComputeHmacBytes(algorithm, key, data)` 返回原始字节，算法名传 `"HMACSHA1"` / `"HMACSHA256"` / `"HMACSHA512"` 这类全大写字符串。

::: warning 哈希不是加密
`HashHelper` 只保证完整性。用于口令派生请走 `IPasswordHasher`；用于防篡改请加密钥，即用 `HmacHelper` 而不是裸哈希——裸 SHA256 拼一个共享密钥的写法能被长度扩展攻击。
:::

## 凭证可逆落库：DataProtection

框架不封装这一层，直接用 ASP.NET Core 的 `IDataProtectionProvider`。约定的做法是每类密文一个专用保护器：

```csharp
public static class SecretProtectionPurposes
{
    public const string CipherPrefix = "dp:";
    public const string StorageSecretAccessKey = "MyApp.StorageConfig.SecretAccessKey.v1";
}

public sealed class StorageSecretProtector
{
    private readonly IDataProtector _protector;

    public StorageSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(SecretProtectionPurposes.StorageSecretAccessKey);
    }

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        // 已带前缀说明是密文，不重复加密（幂等）
        if (plaintext.StartsWith(SecretProtectionPurposes.CipherPrefix, StringComparison.Ordinal))
        {
            return plaintext;
        }

        return SecretProtectionPurposes.CipherPrefix + _protector.Protect(plaintext);
    }

    public string? Unprotect(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : _protector.Unprotect(value[SecretProtectionPurposes.CipherPrefix.Length..]);
    }
}
```

四条要点：

| 要点 | 原因 |
| --- | --- |
| 一类密文一个 Purpose | Purpose 参与密钥派生，互相之间解不开，泄露一处不牵连其他 |
| Purpose 字符串带 `.v1` 后缀 | 需要轮换时递增版本号，且**上线后不得随意改动**，改了历史密文就解不开 |
| 密文加固定前缀 | 让 `Protect` 幂等，避免二次加密把值套两层 |
| 解密失败直接抛 | fail-closed。吞掉异常返回原值，等于把密文当明文发出去 |

::: danger 多实例必须共享密钥环
默认密钥环写在本机文件系统，多副本部署时 A 实例加密的值 B 实例解不开，表现为随机的解密失败。多实例请把密钥持久化到共享存储（`PersistKeysToFileSystem` 指向共享卷、或数据库/Redis 提供程序），并做好该密钥环自身的备份——丢了等于所有密文作废。
:::

保护器本身无状态、只依赖单例 `IDataProtectionProvider`，注册为 `Singleton` 即可。

## 加密的设置项

`SettingManager` 对 `IsEncrypted = true` 的设置项自动加解密，密钥来自配置节 `XiHan:Settings:Aes` 的 `Key`（任意长度字符串，AES 的 Key 与 IV 由它经 PBKDF2 派生）。

```json
{
  "XiHan": {
    "Settings": {
      "Aes": { "Key": "从环境变量或密钥管理服务注入" }
    }
  }
}
```

::: warning 没配 Key 会直接抛异常
读写加密设置项时若 `Key` 为空，`SettingManager` 抛 `XiHanException` 而不是退回内置占位密钥。这是有意的 fail-closed：宁可启动失败，也不要用一个人人都知道的默认密钥把数据"加密"了。
:::

详见 [Settings 包](../packages/settings)。

## 开放接口验签

`XiHanOpenApiSecurityMiddleware` 提供请求签名、内容签名、防重放与报文加解密，配置节 `XiHan:Web:Api:OpenApiSecurity`，默认关闭。

请求头：

| 请求头 | 含义 |
| --- | --- |
| `X-Access-Key` | 客户端标识 |
| `X-Timestamp` / `X-Nonce` | Unix 秒 / 随机串，用于防重放 |
| `X-Signature` | 请求签名（十六进制或 Base64 均接受） |
| `X-Content-Sign` | 请求体摘要（小写十六进制） |
| `X-Sign-Algorithm` / `X-Content-Sign-Algorithm` / `X-Encrypt-Algorithm` | 三个算法选择头，缺省时回落到客户端配置、再回落到全局默认 |
| `X-Encrypt-Iv` / `X-Encrypt-Response` | 请求体加密 IV / 要求响应加密 |

待签名串由六行拼成（`\n` 连接），查询串按键、值分别做 `Ordinal` 排序后 `key=value` 用 `&` 连接、键值各自 URL 编码：

```text
HTTP方法(大写)
路径
规范化查询串
内容签名
时间戳
随机串
```

支持的算法：

| 类别 | 支持 | 开关放开后额外支持 |
| --- | --- | --- |
| 请求签名 | `HMACSHA256`、`HMACSHA512`、`RSASHA256`、`SM2` | `HMACSHA1`（`AllowLegacySignatureAlgorithms`） |
| 内容签名 | `SHA256`、`SHA512` | `MD5`（`AllowLegacyContentSignatureAlgorithms`） |
| 报文加密 | `NONE`、`AES`、`AES-CBC` | `BLOWFISH`（`AllowLegacyEncryptionAlgorithms`） |

::: warning 不加密报文时必须显式发 X-Encrypt-Algorithm: NONE
`DefaultEncryptionAlgorithm` 是 `AES-CBC`。客户端不带这个头时会回落到默认值，中间件会拿明文 JSON 去走解密流程，返回 400「请求体解密失败」。

另外内容签名是对**解密后的明文体**计算的，加密场景下签名基准是明文而不是密文。
:::

完整配置项见 [Web.Api 包](../packages/web-api)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 改了 `Iterations` 后老用户登不上 | 不会发生。哈希串自带参数，校验用串里的值；只有哈希串被截断/改写才失败 |
| 调大 `SaltSize` 后 `NeedsRehash` 一直是 `false` | 它只比较 `Version` / `Iterations` / `HashAlgorithm`，改盐长需同时提升 `Version` |
| 密码历史复用检查永远通过 | 默认存储是只读内存实现，改密后没人调 `RecordPassword`；生产要自实现 `IPasswordHistoryStore` |
| 相同明文每次加密结果一样 | 用了 `AesHelper.Encrypt(text, password)`（全零盐派生 Key/IV）；换三参重载并每条数据用新 IV |
| RSA 加密/验签报密钥解析错误 | 公钥不是 SubjectPublicKeyInfo 编码；用 `ExportSubjectPublicKeyInfo()` 重新导出 |
| SM2 验签恒为 false | 公钥不是曲线点编码，或私钥不是原始标量；`GenerateKeys()` 的输出格式与签名/验签期望的格式不同 |
| 换台机器部署后凭证全解不开 | DataProtection 密钥环没共享，各实例用各自的本机密钥 |
| 启动读设置项抛 `XiHanException` | 有 `IsEncrypted` 的设置项但没配 `XiHan:Settings:Aes:Key` |
| 开放接口返回 400「请求体解密失败」 | 没发 `X-Encrypt-Algorithm: NONE`，回落到默认的 `AES-CBC` |
| 开放接口返回 409「检测到重复请求」 | `X-Nonce` 重复，或客户端重试时复用了同一个 nonce |

## 下一步

- [认证与授权](./authentication)：登录、令牌与权限判定
- [配置与选项](./configuration)：密钥从环境变量与配置提供程序注入的方式
- [Security 包](../packages/security)：完整 API 与全部配置项
- [Utils 包](../packages/utils)：AES / RSA / 哈希等辅助类清单
- [Web.Api 包](../packages/web-api)：开放接口安全中间件的完整配置
