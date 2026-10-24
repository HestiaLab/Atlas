# ADR-0035：中文优先的文档与代码注释语言政策

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-24
- **决策人**：Atlas 维护者（2026-10-24 批准）
- **取代**：[ADR-0026](./0026-code-comment-language-policy.md)；`AGENTS.md` 与 `CONTRIBUTING.md` 中的英文权威条款
- **部分修订**：英文镜像条款由 [ADR-0036](./0036-chinese-only-documentation.md) 修订（中文优先决策保留）
- **相关**：AGENTS.md、CONTRIBUTING.md；质量发现 CORE-P2-20、BLAZOR-01（反向迁移）

## 背景

Atlas 后续主要面向国内开发者。此前的语言政策（ADR-0026）要求公共 API XML
文档使用英文、内部注释英文优先，并规定 `docs/en/` 为每个语言对的权威版本、
`docs/zh/` 为镜像。最近两轮质量审计按该政策把 `Atlas.Core` 的公共 API 与
`Atlas.Blazor` 的公共 XML 文档译为英文，增加了国内开发者的阅读成本，也与
“中文优先”的产品定位冲突。

当前仓库同时存在两类不一致：

- 代码注释语言混合：`src/Atlas.Core` 有 85 个文件、`src/Atlas.Blazor` 有
  15 个文件（含 6 个 TypeScript 文件）的注释仍为英文；其余注释以中文为主。
- 文档虽有完整中文版本，但 `docs/zh/` 全部标记为英文主文档的翻译，权威性
  与中文优先的定位相反。

本 ADR 把语言权威翻转为中文优先，同时保留面向国际开发者的英文镜像。迁移
只改变语言，不改变任何代码行为、公共 API 形状、SRS 语义、交互协议、持久化
格式或安全边界。

## 决策

采用**中文优先、英文镜像**的政策：

1. **代码注释与内部 XML 文档**：使用中文。
2. **公共 API XML 文档**（随 NuGet XML 文档发布）：使用中文。消费者可见的
   契约文档语言随产品定位转为中文。
3. **稳定标识符保持 ASCII**：类型与成员名、错误码、诊断 ID、消息键、交互
   协议字段与值、XML schema 名、XML/JSON 属性名、REQ/ADR/DES 编号等不翻译。
4. **运行时可见文本本轮不翻译**：异常消息、日志、诊断文本和 UI 文案保持
   现状（英文）。这些文本属于可观察行为，翻译会改变运行时行为并破坏现有
   测试断言；如需本地化，应另立 ADR，设计资源键、回退语言与消费方兼容策略。
5. **文档目录**：`docs/zh/` 是权威版本，`docs/en/` 是英文镜像。中文文档与
   英文镜像必须保持相同的路径、文件名、需求与设计 ID、AC 数量与顺序。
6. **双语维护清单**：
   - 同步维护英文镜像：根目录 README、NuGet `package-readme.md`、SRS、HLD、
     requirements-traceability、guides、theming、security、ADR 索引与 ADR。
   - 历史冻结：`development/` 与 `evidence/` 的英文版本标记为 Outdated，
     保留为历史记录，不再要求与中文版同步。
   - 根目录兼容入口 `docs/SRS.md`、`docs/HLD.md`、
     `docs/requirements-traceability.md` 指向 `docs/zh/`。
7. **治理文档**：`AGENTS.md`、`CONTRIBUTING.md` 以中文为权威，保留
   `AGENTS.en.md`、`CONTRIBUTING.en.md` 英文镜像。根目录 `README.md` 以中文
   为权威，`README.en.md` 为英文镜像；`README.zh.md` 保留为兼容入口。
8. **迁移规则**：
   - 只改注释与文档语言；不得改变代码逻辑、标识符、字符串字面量、错误码、
     消息键、交互协议、schema、PublicAPI 基线、SRS 需求 ID/AC 数量与顺序，
     以及已接受 ADR 的决策内容。
   - 每批改动独立通过 build（Debug + Release）、test、format 与 TypeScript
     typecheck/test/build 四门禁。
   - 注释-only 差异必须可审计；翻译不得新增 CS1591 等文档警告。
   - 新增公共成员的 XML 文档从首次提交起使用中文。
9. **ADR 语言**：ADR 以中文正文为权威，英文镜像保留。ADR-0026 仅标记为
   Superseded，正文与历史理由不重写。

## 备选方案

### 维持英文优先（现状）

保留 ADR-0026 与 `docs/en` 权威模型。国际贡献者阅读成本最低，但国内开发者
需要同时理解英文 API 文档、英文注释与中文译文，且与本项目“面向国内开发者”
的定位冲突。拒绝。

### 全部英文

把剩余中文注释与文档也译为英文。仓库语言最统一，但进一步背离产品定位，
并需要重写大量已稳定的中文设计说明。拒绝。

### 逐条注释双语

在每个公共成员上同时提供中英文 `<summary>`/`<remarks>`。维护成本翻倍，
生成的 XML 文档噪声大，且两种语言容易漂移。拒绝；改用“中文权威 + 英文
镜像”的文档级双语模型。

### 中文文档且移除英文

只保留中文文档，删除 `docs/en/`。维护成本最低，但会失去国际贡献者与
NuGet 国际消费者，与保留英文镜像的决策冲突。拒绝。

### 运行时文本同步本地化

在本轮同时翻译异常消息、日志和 UI 文案。会改变可观察行为、破坏测试断言，
并需要资源键与回退机制。拒绝；留待单独 ADR。

## 后果

- 国内开发者获得一致的中文 API 文档、代码注释和权威规范。
- NuGet XML 文档语言发生变化；这是可见但非行为性的变化，消费者升级时
  可能注意到。
- 国际贡献者依赖英文镜像；镜像同步成为新的维护责任。
- `docs/en/` 不再是权威，PR 模板、治理文档和跨语言链接需要同步更新。
- 运行时文本仍为英文，形成“代码与文档中文、运行时文本英文”的过渡状态；
  未来可单独决策是否本地化。
- `development/` 与 `evidence/` 的英文镜像冻结为历史记录，不再保证与
  中文版同步。

## 验证

- 四门禁全绿：`dotnet build`（Debug + Release）、`dotnet test`、
  `dotnet format --verify-no-changes`、TypeScript typecheck/test/build。
  迁移基线为 341 Core + 135 Blazor + 51 JS 测试。
- 差异审计：代码改动仅限注释与 XML 文档行；标识符与字符串字面量零变化。
- `PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt` 零差异。
- `dotnet build` 无新增 CS1591 等文档警告。
- 文档链接检查通过；根目录入口指向 `docs/zh/`。
- 追溯矩阵记录本轮语言政策迁移，并更正 Blazor 测试数。

## 取代关系

- 取代 [ADR-0026](./0026-code-comment-language-policy.md) 的分层语言政策：
  公共 XML 文档英文、内部注释英文优先、文档英文权威。
- 取代 `AGENTS.md` 与 `CONTRIBUTING.md` 中“English is canonical /
  `docs/en/` authority”的条款。
- 不改变任何已接受 ADR 的架构决策；ADR-0026 仅标记为 Superseded，正文保留
  作为历史记录。
