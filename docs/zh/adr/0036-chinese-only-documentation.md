# ADR-0036：中文唯一文档与单一国际 README

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-24
- **决策人**：Atlas 维护者（2026-10-24 批准）
- **修订**：[ADR-0035](./0035-chinese-first-language-policy.md) 的英文镜像条款
- **相关**：AGENTS.md、CONTRIBUTING.md、SECURITY.md、.github 模板、`package-readme.md`

## 背景

ADR-0035 将语言政策转为中文优先，但保留了 `docs/en/` 的全量英文镜像（57 份
Markdown，另含 1 个 DBC 样例）。
实际运行中，完整镜像带来持续的同步成本，而绝大多数规范内容只有中文读者；
维护者决定不再维护逐篇英文翻译，只保留一份面向国际开发者的 README。

若继续保留镜像，任何规范变更都要求双份更新，容易漂移，也与“中文唯一权威”
的定位冲突。本 ADR 收敛文档结构，同时保留一个稳定的国际入口。

## 决策

1. `docs/zh/` 是唯一维护的文档集；删除 `docs/en/` 全部内容。
2. 根目录 `README.en.md` 是唯一的英文文档入口，面向国际开发者，简要介绍
   项目、构建命令、示例与许可证，并链接到中文权威文档；它不复制 SRS、HLD
   或指南正文。
3. 根目录 `README.md` 保持中文权威；`AGENTS.md`、`CONTRIBUTING.md`、
   `SECURITY.md`、`.github` 的 PR/issue 模板使用中文。
4. NuGet `package-readme.md` 保持英文，作为包的国际入口；链接到
   `README.en.md` 与 `docs/zh/`。
5. 所有 `docs/zh/` 文档不再维护英文镜像链接，只声明中文唯一权威。
6. ADR 以中文为唯一版本；不再维护 `docs/en/adr/` 镜像。
7. ADR-0035 的“保留英文镜像”条款由本 ADR 修订；ADR-0035 的中文优先决策
   继续有效。ADR-0026 仍为 Superseded。
8. 翻译规则不变：保留需求/设计 ID、Actor/Priority/Source、AC 数量与顺序、
   代码标识符、数值约束与 must/should/may 强度；运行时可见文本不翻译。
9. 本变更是文档结构变更，行为零变化；四门禁与链接检查必须通过。

## 备选方案

### 继续维护全量英文镜像

内容最完整，但 58 个镜像文件的同步成本高、容易漂移；维护者明确不需要。拒绝。

### 只删除治理文档镜像，保留技术文档英文镜像

减少部分重复，但仍保留 50 多份镜像，未解决根本维护成本；且“部分英文、
部分中文”的边界难以解释。拒绝。

### 不提供英文入口

维护成本最低，但国际开发者无法快速了解项目；与开源定位冲突。拒绝。

### 机器翻译全部英文文档

不维护人工镜像，但生成物质量不可控，且会形成第二套文档。拒绝；由
`README.en.md` 与翻译工具承担国际读者的辅助理解。

## 后果

- `docs/en/` 的 58 个文件（57 份 Markdown + 1 个 DBC 样例）被删除；历史内容可通过
  git 历史检索。
- 57 份既有中文文档头部移除 English mirror 链接；国际读者从 `README.en.md` 进入。
- `SECURITY.md` 与 PR/issue 模板改为中文；GitHub 表单字段名与标签保持 ASCII。
- NuGet `package-readme.md` 保持英文，避免国际消费者失去入口。
- 维护责任收敛为单一中文文档集 + 一份英文 README。
- 国际贡献者需要阅读中文文档或使用翻译工具；这是有意的取舍。

## 验证

- `docs/en/` 不存在；仓库内无可点击的 `docs/en` 链接（历史说明中的代码文本除外）。
- `README.en.md`、`package-readme.md` 链接有效。
- 四门禁全绿：build（Debug + Release）、test、format、TypeScript
  typecheck/test/build。
- PublicAPI 零差异；去注释代码零差异。
- 文档相对链接检查 0 断链。

## 取代关系

修订 [ADR-0035](./0035-chinese-first-language-policy.md) 中关于英文镜像、双语
维护清单与 ADR 双语的条款；ADR-0035 的中文优先决策继续有效。ADR-0026 已由
ADR-0035 取代，本 ADR 不改变该状态。
