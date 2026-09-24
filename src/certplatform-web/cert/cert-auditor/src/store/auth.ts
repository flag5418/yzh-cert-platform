import { defineStore } from 'pinia'
import { useAuthState, type UserInfo } from '@yzh-core/composables/useAuthState'

/**
 * auth store（**薄适配层**）
 *
 * 真实实现已上移系统底座 `@yzh-core/composables/useAuthState`（模块级单例，admin + auditor + core 登录页唯一实现）。
 * 本文件只保留 pinia store 外观，使既有调用方（登录页/布局/router）零改动。
 *
 * ⚠️ 命名铁律（`项目全局规则.md` §16.9 ③′）：UserInfo 字段与后端 PascalCase 逐字一致。
 */
export type { UserInfo }

export const useAuthStore = defineStore('auth', () => useAuthState())
