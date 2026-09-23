declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}

/**
 * Vite 环境变量类型（cert-share 作为 Vite 库独立类型检查时也需要，
 * 不能只依赖宿主应用（cert-admin / cert-auditor）的 vite-env.d.ts）。
 * 与 `vite/client` 的 ImportMetaEnv 声明结构一致，重复声明会正常合并。
 */
interface ImportMetaEnv {
  readonly DEV: boolean
  readonly PROD: boolean
  readonly MODE: string
  readonly BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
