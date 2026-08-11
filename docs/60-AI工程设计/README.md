# 60-AI工程设计

> **作用**：AI 辅助开发的方法论、模板、Skills、知识库。定义了"AI 怎么参与这个项目"。

---

## 文档清单

| 文件 | 职责 | 状态 |
|------|------|------|
| [AI代码生成检查清单-V1.md](<./AI代码生成检查清单-V1.md>) | AI 在生成代码前必须逐项检查的清单，整合所有约束 | 成熟态 V1.2 |
| [vol-skill.md](<./vol-skill.md>) | Vol 框架 AI 开发技能（开发用） | 正式发布 |
| [vol-framework-complete-guide.md](<./vol-framework-complete-guide.md>) | Vol 框架完整指南 | 正式发布 |
| [vol-framework-troubleshooting.md](<./vol-framework-troubleshooting.md>) | Vol 框架常见问题排查 | 正式发布 |
| [cert-platform-page-development-guide.md](<./cert-platform-page-development-guide.md>) | 认证平台页面开发指南 | 正式发布 |
| [YZH-知识库/README.md](<./YZH-知识库/README.md>) | YZH-Framework 改造的知识底座，含 Vol 能力清单、YZH 增量、边界约束、代码模板、踩坑记录 | Phase 1 完成 |
| [YZH-知识库/01-Vol能力清单.md](<./YZH-知识库/01-Vol能力清单.md>) | Vol 框架能力结构化索引（2026-07-31） | 正式发布 |
| [YZH-知识库/02-YZH增量清单.md](<./YZH-知识库/02-YZH增量清单.md>) | YZH 增量能力清单 | 正式发布 |
| [YZH-知识库/03-边界与约束.md](<./YZH-知识库/03-边界与约束.md>) | 不可修改的边界与已废弃方案 | 正式发布 |
| [YZH-知识库/04-代码模板/](<./YZH-知识库/04-代码模板/README.md>) | 常用代码模板（BaseEntity 扩展、ActionFilter、Autofac 注册） | 正式发布 |
| [YZH-知识库/05-踩坑记录/](<./YZH-知识库/05-踩坑记录/README.md>) | AI 开发踩坑记录（Phase2 联调、EF Core、CrudTable、路由配置、DocExtractionRule 等） | 持续更新 |

> 注：
> - `当前项目规则整理 + 下次开发 TODO 清单-V1.md`、`YZH-V3.0-架构设计文档.md`、`YZH-前端框架建设方案-V1.0-待审批版.md` 为**当前执行中**文档，已移至 `70-当前执行/`（2026-08-10 清理）。
> - `17-AI工程设计方法论-V1.md` 已过时，已归档至 `历史文档/`。

---

## 关键词索引

`AI` `宪法` `cursorrules` `Skill` `知识库` `模板` `Design Tokens` `代码生成` `工作流` `审查` `可控性` `Vol框架` `vol-skill` `view-grid` `YZH-知识库` `Vol能力清单` `YZH增量` `代码模板` `踩坑记录` `检查清单` `就绪度` `快速路由`

---

## 依赖关系

- 依赖 `00-工程体系/`：AI 行为受用户画像和协作协议约束
- 依赖 `20-架构决策/`：宪法中的技术栈来源于架构决策

## 被哪些文件夹依赖

- `40-领域设计/`：领域文档深化时会引用 Skills 模板
- `70-当前执行/`：当前执行中的开发工作遵循本目录方法论与知识库
