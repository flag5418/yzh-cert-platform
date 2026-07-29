# src/auditor/ — 审核员前端

> Vue 3 + TypeScript + Element Plus + Vite

## 技术栈

- Vue 3 Composition API (`<script setup lang="ts">`)
- Element Plus UI 组件库
- Vue Router 4（路由）
- Pinia（状态管理）
- Axios（HTTP 客户端，已配置 JWT 拦截）

## 启动

```bash
cd src/auditor
npm install    # 首次
npm run dev    # 监听 9991，API 代理到 9992
```

## 目录结构

```
auditor/src/
├── api/          API 接口层（http.ts 为 Axios 实例）
├── router/       路由配置
├── stores/       Pinia 状态
├── views/        页面组件
│   ├── login/        登录
│   ├── workspace/    审核工作台
│   ├── audit/        审核详情
│   └── report/       审核报告
├── components/   公共组件
├── styles/       全局样式（global.css）
└── assets/       静态资源
```

## 关键词

`auditor` `审核员` `前端` `Vue3` `Element Plus` `Vite` `审核` `工作台`
