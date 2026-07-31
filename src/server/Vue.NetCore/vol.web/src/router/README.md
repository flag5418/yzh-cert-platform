# 认证平台路由配置说明

> **更新时间**: 2026-07-31
> **状态**: ✅ 已配置完成

## 📋 路由配置位置

```
vol.web/src/router/viewGird.js
```

## 🔗 已配置的认证平台路由

| 菜单名称 | 路径 (path) | Vue 组件 | 状态 |
|----------|-------------|----------|------|
| **认证机构管理** | `/CertCertificationBody` | `views/cert/CertificationBody/` | ✅ |
| **ISO标准管理** | `/CertISOStandard` | `views/cert/ISOStandard/` | ✅ |
| **认证申请管理** | `/CertApplication` | `views/cert/CertApplication/` | ✅ |
| **审核任务管理** | `/CertAuditTask` | `views/cert/AuditTask/` | ✅ |
| 工作流配置 | `/CertPlatform/Wf/WorkflowDefinition` | 待创建 | ⚠️ 占位 |
| 审核员管理 | `/CertPlatform/Sys/AuditorManage` | 复用 Sys_User | ✅ |
| 任务状态监控 | `/CertPlatform/Audit/TaskMonitor` | 复用 AuditTask | ✅ |
| 待办任务 | `/CertPlatform/Auditor/PendingTasks` | 复用 AuditTask | ✅ |
| 企业列表 | `/CertPlatform/Ent/EnterpriseList` | `views/cert/Enterprise/` | ✅ |
| 审核任务(页) | `/CertPlatform/Audit/AuditTask` | 复用 AuditTask | ✅ |
| 不符合项管理 | `/CertPlatform/Audit/NonConformity` | 待创建 | ⚠️ 占位 |
| 报告列表 | `/CertPlatform/Rpt/ReportList` | 复用 AuditReport | ✅ |

## 🗄️ 数据库菜单配置

菜单数据存储在 `Sys_Menu` 表中，关键字段：

| 字段 | 说明 | 示例 |
|------|------|------|
| `MenuName` | 菜单显示名 | 认证机构管理 |
| `TableName` | 表名（用于权限） | cert_certification_body |
| `Url` | **路由路径（必须与 viewGird.js 匹配！）** | /CertCertificationBody |
| `ParentId` | 父菜单 ID | 305 |

## ⚠️ 重要：路由匹配规则

Vol 框架的路由跳转流程：
1. 用户点击菜单 → 前端获取菜单的 `Url` 字段
2. 调用 `router.push({ path: Url })`
3. Vue Router 在 `viewGird.js` 中查找匹配的 `path`
4. **如果找不到 → 404 错误！**

### 添加新页面步骤

1. **创建 Vue 组件**
   ```
   views/cert/YourPage/YourPage.vue
   ```

2. **添加路由** (在 viewGird.js 中)
   ```js
   {
     path: '/YourPath',           // ← 必须与 Sys_Menu.Url 一致
     name: 'YourPage',
     component: () => import('@/views/cert/YourPage/YourPage.vue')
   }
   ```

3. **添加菜单** (在数据库中)
   ```sql
   INSERT INTO Sys_Menu (MenuName, TableName, Url, ParentId, ...)
   VALUES ('你的菜单', 'your_table', '/YourPath', 父ID, ...);
   ```

## 🐛 故障排查

### 问题：点击菜单显示 404

**检查清单**：
1. [ ] `viewGird.js` 中是否有对应 `path` 路由？
2. [ ] `Sys_Menu.Url` 是否与路由 `path` 完全一致？
3. [ ] Vue 组件文件是否存在？
4. [ ] 浏览器控制台是否有其他错误？

**常用命令**：
```bash
# 检查语法
node -c src/router/viewGird.js

# 查看菜单配置
docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform \
  -e "SELECT MenuName, Url FROM Sys_Menu WHERE Enable=1;"
```
