---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_1c189011a03f11f1a54f525400f8a581
    ReservedCode1: mdlkYgtwv1N62opfRv2SKslp/5buCLwUiLh6lPfKj+dZdX130RKArH133hCzYvbT5al4hUX+XtjW6Vk+JqRTeapedyfYgUkNWk8T3VWPayLpalm4aq3+s615YQB84uuUtoqi51EOsd5E/XB44T9zZxkV1+ux2ZNx/MR7Qj2QYHTXg6owqgW6XjjA34A=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_1c189011a03f11f1a54f525400f8a581
    ReservedCode2: mdlkYgtwv1N62opfRv2SKslp/5buCLwUiLh6lPfKj+dZdX130RKArH133hCzYvbT5al4hUX+XtjW6Vk+JqRTeapedyfYgUkNWk8T3VWPayLpalm4aq3+s615YQB84uuUtoqi51EOsd5E/XB44T9zZxkV1+ux2ZNx/MR7Qj2QYHTXg6owqgW6XjjA34A=
---

# auditor 前端基础框架

> 版本：V1.0 | 日期：2026-08-25 | 状态：成熟态

## 一、文档定位

本文件描述 `src/auditor`（审核员前端）的基础框架约定，作为审核员端后续业务开发的技术基准。auditor 端与 admin 端共用同一后端（9992），前端技术选型与框架结构独立管理。

## 二、技术栈

| 层 | 技术 | 版本 | 说明 |
|------|------|------|------|
| 框架 | Vue 3 Composition API | 3.4+ | `<script setup lang="ts">` |
| 语言 | TypeScript | 5.x | 严格模式 |
| UI 组件库 | Naive UI | ^2.45.2 | 组件按需引入；消息/通知用 `createDiscreteApi` |
| 构建 | Vite | 8.x | dev 固定 9991，strictPort |
| 路由 | Vue Router | 4.x | 全局守卫 + document.title |
| 状态 | Pinia | 2.x | app（主题）/ user（token） |
| HTTP | Axios | 1.x | baseURL `/api`，timeout 30000 |

auditor 端**不使用** Element Plus（与 admin 端解耦），组件库依赖仅保留 `naive-ui`、`vfonts`。

## 三、基础框架结构说明

```
src/auditor/src/
├── api/          API 接口层（http.ts 为 Axios 实例）
├── layouts/      DefaultLayout.vue：侧边栏 + 顶栏 + 内容区主布局
├── router/       路由（login/workspace/audit/report 四条路由 + 全局守卫）
├── stores/       app.ts 主题模式（light/dark + localStorage 持久化）/ user.ts 用户信息
├── theme/        主题配置（themeOverrides 品牌主色 #1a5fb4）
├── utils/        message.ts 统一消息/通知封装
├── views/        login / workspace / audit / report 占位页面
├── components/   公共组件
├── styles/       global.css（全局样式 + 暗色模式辅助类）
└── assets/       静态资源
```

## 四、约定

### 4.1 主题

- 主题模式存于 Pinia `stores/app.ts`，字段 `themeMode`：`light` / `dark`，持久化键 `theme-mode`。
- 根组件 `App.vue` 通过 `NConfigProvider`（`zhCN` + `dateZhCN`）注入，暗色模式切换 `darkTheme`。
- 品牌主色在 `src/theme/index.ts` 的 `themeOverrides` 中集中维护（primaryColor 等），业务代码不写死颜色。
- `styles/global.css` 提供 `--yzh-*` CSS 变量与 `html[data-theme='dark']` 暗色适配。

### 4.2 路由

- 路由表：`/login`（noAuth）、`/workspace`、`/audit/:taskId`、`/report/:taskId`；`meta.title` / `meta.noAuth` 为页面标题与免登录标记。
- 全局守卫：无 token 且非 noAuth 页面 → 跳转 `/login` 并携带 `redirect`；`afterEach` 按 `meta.title` 设置 `document.title`。
- 新增业务页面必须在 `meta.title` 填写中文标题。

### 4.3 消息提醒

- 组件内使用 `useMessage()` / `useNotification()`（Naive UI Provider 注入）。
- 非组件环境（如 `api/http.ts` 拦截器）统一使用 `src/utils/message.ts` 的 `createDiscreteApi` 封装。
- **禁止**引入 `ElMessage` / Element Plus 任何依赖。

### 4.4 API 约定

- `http.ts`：baseURL `/api`、timeout 30000；请求拦截自动加 `Authorization: Bearer <token>`；响应拦截兼容 Vol 的 `{code, message, data}` 结构，`code !== 0` 报错，401 清 token 并跳 `/login`。

## 五、启动命令

```bash
cd src/auditor
npm install
npm run dev      # 固定 9991，/api 代理到 9992
npm run build    # vue-tsc -b && vite build
```

## 六、端口规划

| 服务 | 端口 | 说明 |
|------|------|------|
| auditor 前端 | 9991 | Vite dev server（`strictPort: true`，防漂移） |
| admin 前端 | 9990 | 后台管理端 |
| 后端 API | 9992 | Vol 后端，auditor 通过 `/api` 代理访问 |

## 七、与 admin 端 Element Plus 的边界

| 维度 | admin 端（src/admin） | auditor 端（src/auditor） |
|------|------|------|
| UI 组件库 | Element Plus | Naive UI |
| 主题体系 | Element Plus 主题变量 | NConfigProvider + themeOverrides |
| 消息提示 | ElMessage | useMessage / utils/message.ts |
| 端口 | 9990 | 9991 |
| 代码隔离 | 两目录独立 package.json / node_modules，依赖互不引用 | 同左 |

两前端仅共享后端 API 约定（`{code,message,data}` 响应结构、`/api` 前缀、Bearer token），代码层不互相 import。

## 关键词

`auditor` `Naive UI` `审核员前端` `9991` `Vue3` `基础框架`
*（内容由AI生成，仅供参考）*
