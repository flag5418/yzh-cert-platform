/**
 * 全局字典池（V4 自研）
 *
 * 替代 vol 的 .dataDic() 机制
 * 用法：
 *   import { dictStore, useDict } from '@/yzh/store/dict'
 *   await dictStore.load('iso_category')
 *   const t = dictStore.translate('iso_category', value)
 */
import { reactive } from 'vue'

interface DictItem {
  value: string
  label: string
}

interface DictPool {
  [dictCode: string]: DictItem[]
}

const pool = reactive<DictPool>({})
const loading: Record<string, Promise<DictItem[]>> = {}

// 内置字典（兜底，避免请求失败时空白）
const builtIn: Record<string, DictItem[]> = {
  enable: [
    { value: '1', label: '启用' },
    { value: '0', label: '禁用' }
  ],
  iso_category: [
    { value: 'quality', label: '质量管理体系' },
    { value: 'environment', label: '环境管理体系' },
    { value: 'ohsas', label: '职业健康安全' },
    { value: 'isms', label: '信息安全管理' },
    { value: 'energy', label: '能源管理' }
  ],
  standard_status: [
    { value: 'draft', label: '草稿' },
    { value: 'published', label: '已发布' },
    { value: 'obsolete', label: '已废止' },
    { value: 'implemented', label: '已实施' }
  ]
}

export const dictStore = {
  /**
   * 异步加载字典
   */
  async load(code: string): Promise<DictItem[]> {
    if (pool[code] && pool[code].length) return pool[code]
    if (loading[code]) return loading[code]

    loading[code] = (async () => {
      try {
        const res = await fetch(
          `/api/Sys_DictionaryList/getPageData?dicCode=${code}&page=1&rows=1000`,
          {
            headers: { Authorization: `Bearer ${localStorage.getItem('YZH_TOKEN') || ''}` }
          }
        )
        const json = await res.json()
        const items: DictItem[] = (json.rows || json.data?.rows || []).map((r: any) => ({
          value: String(r.DicValue ?? r.dicValue ?? r.value ?? ''),
          label: r.DicName ?? r.dicName ?? r.label ?? r.name ?? String(r.DicValue ?? '')
        }))
        pool[code] = items.length ? items : builtIn[code] || []
        return pool[code]!
      } catch {
        pool[code] = builtIn[code] || []
        return pool[code]!
      } finally {
        delete loading[code]
      }
    })()
    return loading[code]
  },

  /**
   * 翻译（无值返回原 value）
   */
  translate(code: string, value: any): string {
    if (value === undefined || value === null || value === '') return '-'
    const items = pool[code] || builtIn[code]
    if (!items) return String(value)
    const found = items.find((i) => i.value === String(value))
    return found ? found.label : String(value)
  },

  /**
   * 获取选项
   */
  options(code: string): DictItem[] {
    return pool[code] || builtIn[code] || []
  },

  /**
   * 预加载
   */
  async preload(codes: string[]) {
    await Promise.all(codes.map((c) => this.load(c)))
  }
}
