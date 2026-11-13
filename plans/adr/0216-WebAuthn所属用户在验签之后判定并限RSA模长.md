# ADR-0216 WebAuthn 所属用户在验签之后判定，并限 RSA 模长

## 状态

已采纳（RFC-0008「WebAuthn」三条修订，认证向量增补两条）。

## 日期

2026-11-13

## 背景

5.0 安全审查 S-43（`plans/5.0-安全审查.md`），低危，加固。三处：

- **失败原因泄露所属用户**：认证核对先判定凭据属于谁、再验签。没有私钥的人拿任意凭据 ID 与一个猜测的用户句柄（或先填的账号）去试，签名随便填：
  - 凭据不属于猜测的用户时得到 `user_handle`，属于时得到 `signature`。
  - 凭据 ID 与用户的对应关系就这样被逐个试出来。
- **RSA 模长没有上限**：COSE 键只要求模长不少于 2048 位。存进来一个几万位的模数，之后每次认证的验签都很慢。
- **AAGUID 白名单的建议不成立**：包文档建议「只允许特定型号」时在 `WebAuthnRegisteredCredential.Aaguid` 之上加校验。证明一律请求 `none`、证明声明不校验，AAGUID 是认证器自报的，任何认证器都能报成别的型号。

复现：`WebAuthnCeremonyTests.Ownership_IsCheckedAfterTheSignature_SoItCannotBeProbedWithoutTheKey` 与 `RsaModulus_LongerThan4096Bits_IsRejected`。修正前，前者得到 `user_handle`，后者能读出 8192 位的公钥。

## 决策

1. **核对顺序**：认证核对里，所属用户的判定移到验签之后，签名计数之前。
   - 签名不对的响应只得到 `signature`。签名对了才说明响应来自凭据的持有者，这时 `user_handle` 不再泄露什么。
   - 判定内容不变（ADR-0179）。
2. **RSA 模长**：2048 到 4096 位（256 到 512 字节），超出按 `public_key`。注册与认证读存储的公钥都按这条。
3. **AAGUID**：RFC 与文档写明，在 `none` 证明下 AAGUID 没有经过证明，不能拿来限制型号。要限制型号须请求 `direct` 或 `enterprise` 证明，并自己核对证明声明的信任链与元数据服务；本包仍不做。
4. **向量**：认证向量增补两条。
   - `credential-of-another-user-bad-signature`：他人的凭据配坏签名，期望 `signature`。
   - `rsa-modulus-too-large`：存储的公钥是 8192 位的 RSA，期望 `SECURITY_VALIDATION_006` / `public_key`。

## 影响

- 失败原因的变化：签名不对、所属用户也不对的响应，原因从 `user_handle` 变为 `signature`。按原因区分处理的应用要知道这一点。
- 已存储的超过 4096 位的 RSA 公钥认证时以 `public_key` 失败，要重新注册。常见认证器的 RSA 公钥都是 2048 位。
- 公开 API 不变。
