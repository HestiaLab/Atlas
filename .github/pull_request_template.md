## 摘要

- 变更内容：
- 原因：

## SRS 追溯

- 相关 SRS 需求（`docs/zh/SRS.md`）：REQ-F-xxx
- 新增 SRS 需求（如有）：REQ-F-xxx

## 验证

- [ ] `dotnet build Atlas.sln` — 所有项目
- [ ] `dotnet test Atlas.sln` — 全部 xUnit 测试通过
- [ ] `dotnet format Atlas.sln --verify-no-changes` — 无格式违规
- [ ] 在 `src/Atlas.Blazor` 中执行 `npm ci && npm run typecheck && npm run test:js && npm run build:js`
- [ ] 文档改动：相对链接与锚点检查通过（0 断链）
- [ ] 未运行；原因：

## 影响

- 公共 API 影响（`Atlas.Core` / `Atlas.Blazor`）：
- NuGet 打包或依赖影响：
- 文档影响（`docs/`）：中文文档是 `docs/zh/` 下唯一维护的权威版本；不再维护英文镜像；国际入口为根目录 `README.en.md`。
- TypeScript / JS interop 影响：

## AI 协助

- [ ] 使用了 AI 协助并已披露——阅读文档、运行验证、记录人工验证。
- [ ] 未使用有意义的 AI 协助。
