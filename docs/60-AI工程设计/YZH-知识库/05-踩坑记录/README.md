# 05-踩坑记录

**用途**：记录 YZH-Framework 改造过程中发现的问题、教训和解决方案。

## 文档清单

| 文件 | 一句话职责 | 日期 |
|------|-----------|------|
| 2026-08-03_Phase2联调全栈问题修复记录.md | Phase2 全栈联调过程中的后端/前端/DB 问题汇总 | 2026-08-03 |
| 2026-08-07_EF-Core-Column映射snake_case导致400错误.md | EF Core column 命名 snake_case / camelCase 映射不一致导致 400 提交失败的根因和修复 | 2026-08-07 |
| 2026-08-07_YZHV2 CrudTable 导出空文件+排序+业务页简化.md | CrudTable 导出 0 字节、排序失效、业务页过度设计 3 连坑修复 | 2026-08-07 |
| 2026-08-08_YzhTreeCheckboxTable-勾选回显与同步.md | YzhTreeCheckboxTable 勾选回显丢失、父子节点勾选同步、半选态 3 类问题修复 | 2026-08-08 |
| 2026-08-08_关联表保存问题与T+V模式修复.md | 关联表 T+V 模式 savechanges 重复键 / FK 丢失问题修复 | 2026-08-08 |
| 2026-08-09_前端路由配置完整指南.md | admin 端 / auditor 端前端路由（CertPlatform 7 大路由）漏挂载、404、重定向配置指南 | 2026-08-09 |
| 2026-08-09_标准目录管理系统路由配置完整指南.md | 标准目录 DirectoryConfig→DirectoryManager 路由映射、菜单权限、按钮权限完整配置 | 2026-08-09 |
| **2026-08-10_DocExtractionRule 预览链 12 类踩坑与根因修复汇总.md** | **DocExtractionRule V1~V13 全部 12 类真实踩坑（http.js res.data 双层 / fetch 缺 JWT / console.debug 过滤看不见 / ZIP Central Directory _rels/.rels 误判 docx 专属 / 7 锚点日志链 / MinIO storagePath 拆分扩展名等），100% 真实 Network hex + Console 日志，下次开发预览相关问题直接对照** | 2026-08-10 |

## 格式约定

每条记录需包含：
- **问题描述**：遇到什么现象
- **根因分析**：为什么发生
- **解决方案**：怎么修的
- **预防措施**：如何避免
- **日期**：发现时间

