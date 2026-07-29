# src/ — 源代码目录

> 本目录包含平台全部源代码，按端拆分。

## 子项目一览

| 目录 | 技术栈 | 端口 | 说明 |
|------|--------|------|------|
| [server/](server/) | .NET 8 + Vol + EF Core | 9992 | 统一后端 API，基于 Vol 框架 |
| [admin/](admin/) | Vue 3 + Element Plus | 9990 | 后台管理端（Vol 自带，待定制） |
| [auditor/](auditor/) | Vue 3 + Element Plus + Vite | 9991 | 审核员 Web 端 |
| auditor-app/ | — | — | 审核员移动端（预留） |

## 关键词索引

`server` `后端` `API` `Vol框架` `admin` `后台管理` `auditor` `审核员` `前端` `Vue3` `Element Plus` `移动端` `auditor-app`
