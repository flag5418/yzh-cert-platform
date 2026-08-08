/**
 * YZH V3.0 配置驱动 Store (Vuex Module)
 *
 * 职责：
 * 1. 登录后全量加载所有页面 UI 配置（yzh_page_config + yzh_field_config）
 * 2. 存储到内存 + localStorage 持久化
 * 3. 提供同步 getConfig(pageKey) 方法，各页面零延迟读取
 * 4. 支持手动刷新配置（开发阶段频繁使用）
 *
 * 安全：
 * - 接口需要 JWT 登录鉴权才能访问
 * - 配置信息仅包含 UI 渲染所需字段，不含业务数据
 *
 * 使用方式：
 *   // 1. 登录成功后调用（App.vue / Index.vue）
 *   this.$store.dispatch('yzhConfig/init')
 *
 *   // 2. 各页面读取配置（同步，无网络请求）
 *   const config = this.$store.getters['yzhConfig/getConfig']('ISOStandard')
 *
 *   // 3. 手动刷新（顶部工具栏按钮）
 *   this.$store.dispatch('yzhConfig/refresh')
 */

// 导入 Vol 框架的 http 实例（自动携带 Token、自动处理 baseURL）
import http from '@/api/http'

// ============================================================
// localStorage Key 常量
// ============================================================
const STORAGE_KEY = 'yzh_page_configs_v3'
const VERSION_KEY = 'yzh_config_version_v3'

// ============================================================
// 默认状态
// ============================================================
const getDefaultState = () => ({
  /** 所有页面配置字典 { pageKey: { pageMeta, fieldConfigs } } */
  configs: {},
  /** 服务端版本号 */
  version: '',
  /** 是否已加载完成 */
  loaded: false,
  /** 是否正在加载中 */
  loading: false,
  /** 最后一次同步时间 (ISO string) */
  lastSyncTime: null,
  /** 错误信息 */
  error: null,
})

// ============================================================
// Vuex Module
// ============================================================
export default {
  namespaced: true,

  state: getDefaultState(),

  mutations: {
    SET_CONFIGS(state, { version, configs }) {
      state.version = version || ''
      state.configs = configs || {}
      state.loaded = true
      state.loading = false
      state.error = null
      state.lastSyncTime = new Date().toISOString()

      // 持久化到 localStorage
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(configs))
        localStorage.setItem(VERSION_KEY, version)
      } catch (e) {
        console.warn('[YZHConfigStore] localStorage 写入失败:', e)
      }
    },

    SET_LOADING(state, loading) {
      state.loading = loading
      if (loading) state.error = null
    },

    SET_ERROR(state, error) {
      state.error = typeof error === 'string' ? error : error?.message || '未知错误'
      state.loading = false
    },

    /** 从 localStorage 恢复缓存（应用启动时调用） */
    RESTORE_FROM_CACHE(state) {
      try {
        const cached = localStorage.getItem(STORAGE_KEY)
        const cachedVersion = localStorage.getItem(VERSION_KEY)
        if (cached && cachedVersion) {
          state.configs = JSON.parse(cached)
          state.version = cachedVersion
          console.log(`[YZHConfigStore] 📦 从本地缓存恢复 ${Object.keys(state.configs).length} 个页面配置 (v${cachedVersion})`)
        }
      } catch (e) {
        console.warn('[YZHConfigStore] localStorage 读取失败，清除损坏数据')
        localStorage.removeItem(STORAGE_KEY)
        localStorage.removeItem(VERSION_KEY)
      }
    },

    RESET(state) {
      Object.assign(state, getDefaultState())
      localStorage.removeItem(STORAGE_KEY)
      localStorage.removeItem(VERSION_KEY)
    },
  },

  getters: {
    /**
     * 获取指定页面的完整配置（同步返回）
     * @param {string} pageKey - 页面标识（如 'ISOStandard'）
     * @returns {{ pageMeta, fieldConfigs } | null}
     */
    getConfig: (state) => (pageKey) => {
      if (!pageKey) return null
      return state.configs[pageKey] || null
    },

    /**
     * 检查某个页面的配置是否已加载
     */
    hasConfig: (state) => (pageKey) => !!state.configs[pageKey],

    /**
     * 获取所有已加载的 pageKey 列表
     */
    pageKeys: (state) => Object.keys(state.configs),

    /**
     * 获取当前版本号
     */
    version: (state) => state.version,

    /**
     * 是否已就绪（有配置数据）
     */
    isReady: (state) => state.loaded && Object.keys(state.configs).length > 0,

    /**
     * 加载状态信息（用于 UI 展示）
     */
    statusInfo: (state) => ({
      loaded: state.loaded,
      loading: state.loading,
      error: state.error,
      pageCount: Object.keys(state.configs).length,
      version: state.version,
      lastSyncTime: state.lastSyncTime,
    }),
  },

  actions: {
    /**
     * 初始化：先恢复本地缓存，再从服务端全量拉取（强制刷新）
     * 登录成功后调用
     */
    async init({ commit, dispatch }) {
      // 1. 先从本地恢复（让页面立即可用旧配置）
      commit('RESTORE_FROM_CACHE')

      // 2. 从服务端全量拉取最新配置（每次登录都强制刷新）
      await dispatch('refresh')
    },

    /**
     * 刷新配置：从服务端重新拉取全量配置
     * - 开发阶段可手动触发
     * - 登录时自动触发
     */
    async refresh({ commit, state }) {
      if (state.loading) return

      commit('SET_LOADING', true)

      try {
        // 使用 Vol 框架的 http 实例（自动携带 Token、自动处理 baseURL）
        // http.get(url, params, loading, config) 返回的是 response.data
        const data = await http.get('/api/yzh-page-config/all', null, false)

        if (!data?.success) {
          throw new Error(data?.message || '获取配置失败')
        }

        const { version, configs } = data.data || {}

        commit('SET_CONFIGS', { version, configs })

        console.log(`[YZHConfigStore] ✅ 配置刷新成功: v${version}, ${Object.keys(configs || {}).length} 个页面`)

        return { success: true, version, count: Object.keys(configs || {}).length }
      } catch (e) {
        console.error('[YZHConfigStore] ❌ 配置刷新失败:', e)
        commit('SET_ERROR', e)

        return { success: false, error: e.message }
      }
    },

    /**
     * 清除配置（登出时调用）
     */
    clear({ commit }) {
      commit('RESET')
    },
  },
}
