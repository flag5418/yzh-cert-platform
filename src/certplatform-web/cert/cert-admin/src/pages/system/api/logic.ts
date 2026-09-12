/**
 * ApiLogic - 接口管理 Logic
 *
 * 功能：
 * - 展示所有接口列表（按分组）
 * - 触发接口同步（扫描 + 增量更新）
 * - 跳转到 Swagger 测试
 */

import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getApiList, syncApis, type ApiItem, type SyncResult } from '@/api/system/api'

export class ApiLogic {
  // ──── 接口列表 ────
  apiList = ref<ApiItem[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  syncing = ref(false)

  // ──── 搜索过滤 ────
  searchKeyword = ref('')
  filterMethod = ref('')

  // ──── 分组统计 ────
  groupStats = computed(() => {
    const stats: Record<string, number> = {}
    for (const api of this.apiList.value) {
      stats[api.GroupPath] = (stats[api.GroupPath] || 0) + 1
    }
    return Object.entries(stats).sort((a, b) => a[0].localeCompare(b[0]))
  })

  // ──── 过滤后的接口列表 ────
  filteredApis = computed(() => {
    let result = this.apiList.value

    if (this.searchKeyword.value) {
      const keyword = this.searchKeyword.value.toLowerCase()
      result = result.filter(
        (a) =>
          a.Name.toLowerCase().includes(keyword) ||
          a.Path.toLowerCase().includes(keyword) ||
          a.GroupPath.toLowerCase().includes(keyword),
      )
    }

    if (this.filterMethod.value) {
      result = result.filter((a) => a.Method === this.filterMethod.value)
    }

    return result
  })

  // ──── 同步结果 ────
  syncResult = ref<SyncResult | null>(null)

  // ========================================================
  // 加载接口列表
  // ========================================================

  async loadApiList(): Promise<void> {
    this.loading.value = true
    try {
      this.apiList.value = await getApiList()
    } catch (e: any) {
      ElMessage.error(e.message || '加载接口列表失败')
    } finally {
      this.loading.value = false
    }
  }

  // ========================================================
  // 触发同步
  // ========================================================

  async handleSync(): Promise<void> {
    try {
      await ElMessageBox.confirm(
        '将扫描所有 Controller 接口并同步到数据库。已有接口的权限关联不会被清除。确认继续？',
        '同步接口',
        {
          confirmButtonText: '确认同步',
          cancelButtonText: '取消',
          type: 'warning',
        },
      )
    } catch {
      return
    }

    this.syncing.value = true
    this.syncResult.value = null

    try {
      this.syncResult.value = await syncApis()
      ElMessage.success(
        `同步完成：新增 ${this.syncResult.value.Added} 个，更新 ${this.syncResult.value.Updated} 个，删除 ${this.syncResult.value.Deleted} 个`,
      )
      await this.loadApiList()
    } catch (e: any) {
      ElMessage.error(e.message || '同步失败')
    } finally {
      this.syncing.value = false
    }
  }

  // ========================================================
  // Swagger 测试跳转（后端地址：9992 端口）
  // ========================================================

  openSwagger(): void {
    // Swagger 运行在后端正口（9992），不是前端（9990）
    window.open('http://127.0.0.1:9992/swagger/index.html', '_blank')
  }

  // ========================================================
  // 重置过滤
  // ========================================================

  resetFilter(): void {
    this.searchKeyword.value = ''
    this.filterMethod.value = ''
  }
}
