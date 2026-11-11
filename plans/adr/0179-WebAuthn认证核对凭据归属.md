# ADR-0179 WebAuthn 认证核对凭据归属

## 状态

已采纳（RFC-0008「WebAuthn」认证第 4 步修订，修订 ADR-0170）。

## 日期

2026-11-12

## 背景

5.0 安全审查 S-04（`plans/5.0-安全审查.md`），中危。

- `WebAuthnCeremonies.VerifyAuthentication` 只在「存储的凭据带用户句柄」且「响应也带 `userHandle`」时比对两者，任何一边没有就跳过。
- 方法没有「期望用户」入参；`WebAuthnStoredCredential.UserHandle` 可空，`WebAuthnRegisteredCredential` 也不交回句柄，应用很容易存成空。
- 场景一（先填账号，或作为第二因素）：知道受害者口令的攻击者，用自己账号下的通行密钥签受害者的挑战，并省略 `userHandle`。应用按 `response.Id` 找到的是攻击者自己的凭据，签名、计数都对，核对通过，应用完成了受害者的登录。注册只认 `none` 证明，软件密钥就够。
- 场景二（免用户名）：文档让应用按 `userHandle` 找用户。存储的凭据没记句柄时，攻击者交上自己的凭据加受害者的句柄，就能以受害者登录。

复现：`IdentifierFirst_ACredentialOfAnotherUser_IsDenied`、`Usernameless_TheResponseMustCarryTheOwnersHandle`，以及一致性向量 `credential-of-another-user`、`usernameless-without-handle`。修正前，前者原本没有能拒绝的入参，后者核对通过。

## 备选

1. **只在文档里要求应用自己核对归属**：否决。归属是 WebAuthn §7.2 第 6 步，属于仪式本身；交给每个应用自己记得做，正是这次缺陷的来源。
2. **拆成「已认出用户」与「免用户名」两个方法**：否决。两者只差一个入参，拆开要重复全部核对步骤。
3. **存储的凭据必须记所属用户，核对时传入期望用户（可空）**：采纳，与规范的两个分支一一对应。

## 决策

1. **`WebAuthnStoredCredential.UserHandle` 改为必填**（注册选项的 `user.id`），不是 base64url 时抛 `ArgumentException`。
2. **`VerifyAuthentication(response, expectedChallenge, credential, expectedUserHandle)`**：
   - `expectedUserHandle` 给出（登录前已认出用户）：凭据的所属用户必须是他。
   - `expectedUserHandle` 为空（免用户名）：响应必须带 `userHandle`。
   - 响应带了 `userHandle`：必须等于所属用户。
   - 任一不满足为 `SECURITY_DENIED_005`，原因 `user_handle`。在签名核对之前判定，与 §7.2 的顺序一致。
3. **`WebAuthnVerifiedAssertion` 新增 `UserHandle`**（取自存储的凭据）：免用户名的流程据此确定登录的用户，不取响应里的句柄。
4. **一致性向量**：认证样例新增可选字段 `expectedUserHandle`（Go 的 `WebAuthnCase` 同步）。新增三条样例：凭据属于别人、已认出用户而响应不带句柄（通过）、免用户名而响应不带句柄。向量用测试认证器重新生成。
5. **文档**：示例按两种流程传参；注册时存下所属用户，并拒绝已登记过的凭据 ID。

## 影响

- 源码迁移：
  - `VerifyAuthentication(response, challenge, stored)` 改为 `VerifyAuthentication(response, challenge, stored, expectedUserHandle)`：先认出用户的流程传他的句柄，免用户名的流程传 `null`。
  - 构造 `WebAuthnStoredCredential` 时必须给出所属用户的句柄。
  - 读 `WebAuthnVerifiedAssertion` 的位置构造多了一个参数。
- 行为：免用户名流程里不带 `userHandle` 的响应被拒绝（可发现凭据按规范总会带）。
- `Security.WebAuthn` 是 `Preview`，又是本轮新增的包，GA 前修改。
