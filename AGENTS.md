# AGENTS.md — AI 工具入口

> 本文件是 AI 编程助手（Cursor / Claude Code / Copilot / Aider 等）的自动加载入口。
> 完整规则请参见：[项目全局规则.md](./项目全局规则.md)

## 快速指针

- **项目规则**: `项目全局规则.md` — 文档体系、目录结构、端口规划、AI 检索协议
- **技术栈**: .NET 8 + Vol 框架（后端） / Vue 3 + Element Plus（前端）
- **数据库**: MySQL 8.0 @ localhost:3307 / Redis @ localhost:6380
- **端口**: 后端 9992 / 后台管理 9990 / 审核员前端 9991
- **Vol 框架指南**: `docs/60-AI工程设计/vol-skill.md`

## AI 行为约束

1. 生成任何代码前，先查阅 `docs/` 中对应业务域的设计文档
2. 后端代码只改 `VOL.Sys/Services/System/Partial/` 下的 Partial Service，禁改 .jsx
3. 前端代码使用 Vol 框架的 view-grid 组件模式，参考 `vol-skill.md` §12
4. 所有文件路径使用 macOS 绝对路径格式
5. 遵守项目全局规则中的文档即宪法原则
