# certplatform-web 前端重构

> **版本**：V1.0 | **日期**：2026-09-05

## 目录结构

```
certplatform-web/
├── yzh.vue.core/     ← 核心组件库（YzhTable, YzhForm, YzhApiClient）
├── share/            ← 业务共享层（跨角色复用）
├── admin/            ← 管理员端（端口9990，Element Plus）
├── auditor/          ← 审核员端（端口9991）
└── enterprise/       ← 企业端（待建）
```

## 启动方式

### 管理员端
```bash
cd admin
./start.sh
# 访问: http://127.0.0.1:9990
```

### 审核员端
```bash
cd auditor
./start.sh
# 访问: http://127.0.0.1:9991
```

### 构建生产版本
```bash
cd admin && node node_modules/.bin/vite build
cd auditor && node node_modules/.bin/vite build
```

## 技术栈
- Vue 3 + TypeScript + Vite
- Element Plus（admin端）
- Pinia（状态管理）
- Vue Router（路由）

## 依赖说明
node_modules 通过 symlink 复用 vol.web 的依赖，无需重复安装。

## 文档
- 重构方案：`docs/20-体系认证/03-详细设计/03-平台基础/certplatform-web-前端重构-V1.md`
- 启动指南：`docs/20-体系认证/03-详细设计/03-平台基础/certplatform-web-启动指南-V1.md`
