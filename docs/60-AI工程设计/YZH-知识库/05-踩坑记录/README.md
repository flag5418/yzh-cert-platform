# 05-踩坑记录

**用途**：记录 YZH-Framework 改造过程中发现的问题、教训和解决方案。

> **官方文档对照原则**：每篇踩坑记录均已对照 Vol 框架官方在线文档（http://v3.volcore.xyz）核实。文档头部的「官方文档参考」章节列出了对应的官方文档 URL，根因分析中标注了 `> **官方文档对照**` 的内容为官方文档原文或转述。

## 文档清单

| 文件 | 一句话职责 | 日期 |
|------|-----------|------|
| 2026-08-03_Phase2联调全栈问题修复记录.md | Phase2 全栈联调过程中的后端/前端/DB 问题汇总（7 类问题 + 23 项 Checklist） | 2026-08-03 |
| 2026-08-07_EF-Core-Column映射snake_case导致400错误.md | EF Core column 命名 snake_case / camelCase 映射不一致导致 400 提交失败的根因和修复 | 2026-08-07 |
| 2026-08-07_YZHV2 CrudTable 导出空文件+排序+业务页简化.md | CrudTable 导出 0 字节、排序失效、业务页过度设计 3 连坑修复 | 2026-08-07 |
| 2026-08-08_YzhTreeCheckboxTable-勾选回显与同步.md | YzhTreeCheckboxTable 勾选回显丢失、父子节点勾选同步、半选态 3 类问题修复 | 2026-08-08 |
| 2026-08-08_关联表保存问题与T+V模式修复.md | 关联表 T+V 模式 savechanges 重复键 / FK 丢失问题修复 | 2026-08-08 |
| 2026-08-09_前端路由配置完整指南.md | admin 端 / auditor 端前端路由（CertPlatform 7 大路由）漏挂载、404、重定向配置指南 | 2026-08-09 |
| 2026-08-09_标准目录管理系统路由配置完整指南.md | 标准目录 DirectoryConfig→DirectoryManager 路由映射、菜单权限、按钮权限完整配置 | 2026-08-09 |
| **2026-08-10_DocExtractionRule 预览链 12 类踩坑与根因修复汇总.md** | **DocExtractionRule V1~V13 全部 12 类真实踩坑（http.js res.data 双层 / fetch 缺 JWT / console.debug 过滤看不见 / ZIP Central Directory _rels/.rels 误判 docx 专属 / 7 锚点日志链 / MinIO storagePath 拆分扩展名等），100% 真实 Network hex + Console 日志，下次开发预览相关问题直接对照** | 2026-08-10 |
| **2026-08-11-转换队列化实施踩坑记录.md** | **转换队列化实施 5 类踩坑：循环依赖 VOL.Builder↔VOL.WebApi / ControllerBase 缺 JsonNormal / MySQL 不支持 RETURNING / 端口 9991 冲突 / HttpClient.Headers 不存在** | 2026-08-11 |
| **2026-08-11_Vol框架菜单配置完整指南与踩坑记录.md** | **Vol 框架菜单系统完整指南：MenuType 字段详解、top/classics 布局差异、ParentId 归属、Redis+内存双层缓存机制、SQL 模板、快速诊断命令。覆盖 90% 菜单不显示问题（MenuType错误/ParentId错误/缓存未清除）** | 2026-08-11 |
| **2026-08-13_NOTracking静默失效与文件夹重命名级联修复.md** | **NoTracking 导致 465 个转换任务永久 stuck（ConvertQueueManager/OfficeConvertService 缺 AsTracking）/ GetMaxSequence 按 parentCode 分组导致跨父节点同级编码碰撞（改为按 Depth 全局分配）/ 文件夹重命名缺少 MinIO 级联同步（重写 RenameFolderAsync）/ 根目录级孤立文件不可见（虚拟"根目录"节点兜底）** | 2026-08-13 |
| **2026-08-16_文件管理功能开发经验总结.md** | **文件管理功能开发 4 类踩坑（http.js put 未定义 / MinIO 未注册 DI / Force 缺 [NotMapped] / 重命名 API 返回成功但 DB 未更新）+ 调试方法论（ILogger、DLL 验证、构建缓存清理、系统化验证）** | 2026-08-16 |

## 格式约定

每条记录需包含：
- **问题描述**：遇到什么现象
- **根因分析**：为什么发生
- **官方文档对照**：Vol 框架官方文档中的对应说明（标注 `> **官方文档对照**`）
- **解决方案**：怎么修的
- **预防措施**：如何避免
- **日期**：发现时间

## 官方文档快速索引

| 主题 | 官方文档 URL | 对应踩坑记录 |
|------|-------------|-------------|
| 后台 Service 业务扩展 | http://v3.volcore.xyz/docs/cs/service/guid.html | 08-03, 08-07, 08-08 |
| 数据库访问（repository/EF） | http://v3.volcore.xyz/docs/cs/dev/db.html | 08-03, 08-07, 08-08, 08-11 |
| 接口返回大小写（JsonNormal） | http://v3.volcore.xyz/docs/cs/dev/case.html | 08-07, 08-11 |
| 前端 API 传参 | http://v3.volcore.xyz/docs/cs/dev/api.html | 08-03, 08-09, 08-10, 08-11 |
| searchBefore 查询条件 | http://v3.volcore.xyz/docs/view-grid/methods/searchBefore.html | 08-03, 08-07 |
| onInit/onInited 属性配置 | http://v3.volcore.xyz/docs/view-grid/properties.html | 08-03, 08-07, 08-09 |
| 新建钩子 Add | http://v3.volcore.xyz/docs/cs/service/add.html | 08-08 |
| 编辑钩子 Update | http://v3.volcore.xyz/docs/cs/service/update.html | 08-08 |
| 删除钩子 Del | http://v3.volcore.xyz/docs/cs/service/del.html | 08-08 |
