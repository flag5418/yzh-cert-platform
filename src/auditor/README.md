# src/auditor/ — 审核员前端

> Vue 3 + TypeScript + Naive UI + Vite

## 技术栈

- Vue 3 Composition API (`<script setup lang="ts">`)
- Naive UI 组件库（组件按需引入；非组件环境消息用 `createDiscreteApi`）
- Vue Router 4（路由，含全局登录守卫与 document.title 设置）
- Pinia（状态管理：app 主题模式 light/dark / user token 与用户信息）
- Axios（HTTP 客户端：baseURL `/api`、timeout 30000、请求拦截加 Bearer token、响应拦截兼容 Vol 的 `{code,message,data}` 结构，错误提示用 Naive UI message）

## 启动

```bash
cd src/auditor
npm install    # 首次
npm run dev    # 固定监听 9991（strictPort），/api 代理到 9992
```

> 端口：开发服务固定 9991（vite.config.ts 已配置 `strictPort: true`）；后端 API 9992。

## 目录结构

```
auditor/src/
├── api/          API 接口层（http.ts 为 Axios 实例）
├── layouts/      布局（DefaultLayout.vue：侧边栏 + 顶栏 + 内容区）
├── router/       路由配置（含全局守卫）
├── stores/       Pinia 状态（app.ts 主题模式 / user.ts 用户）
├── theme/        Naive UI 主题配置（themeOverrides 品牌主色）
├── utils/        工具（message.ts 消息/通知封装）
├── views/        页面组件
│   ├── login/        登录
│   ├── workspace/    审核工作台
│   ├── audit/        审核详情（:taskId）
│   └── report/       审核报告（:taskId）
├── components/   公共组件
├── styles/       全局样式（global.css）
└── assets/       静态资源
```

## 关键词

`auditor` `审核员` `前端` `Vue3` `Naive UI` `Vite` `审核` `工作台` `9991`
