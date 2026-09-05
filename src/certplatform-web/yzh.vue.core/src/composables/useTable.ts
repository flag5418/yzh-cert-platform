import { ref, reactive } from 'vue'
import type { Page, PageParams } from '../components/table/types'

export function useTable<T = any>() {
  const loading = ref(false)
  const rows = ref<T[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const searchParams = reactive<Record<string, any>>({})

  async function loadData(loader: (params: PageParams) => Promise<Page<T>>) {
    loading.value = true
    try {
      const params: PageParams = {
        page: page.value,
        rows: pageSize.value,
        ...searchParams
      }
      const res = await loader(params)
      rows.value = res.rows || []
      total.value = res.total || 0
    } finally {
      loading.value = false
    }
  }

  function setSearchParams(params: Record<string, any>) {
    Object.assign(searchParams, params)
    page.value = 1
  }

  function resetSearchParams() {
    Object.keys(searchParams).forEach((k) => delete searchParams[k])
    page.value = 1
  }

  return {
    loading,
    rows,
    total,
    page,
    pageSize,
    searchParams,
    loadData,
    setSearchParams,
    resetSearchParams
  }
}
