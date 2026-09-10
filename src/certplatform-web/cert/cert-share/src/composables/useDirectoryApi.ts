import { ref } from 'vue'
import { yzhApi } from '@yzh-core/api/client'

export function useDirectoryApi() {
  const loading = ref(false)

  async function loadDirectoryTree() {
    loading.value = true
    try {
      // TODO: 待后端目录树 API 就绪后补充
      return []
    } finally {
      loading.value = false
    }
  }

  return { loading, loadDirectoryTree }
}
