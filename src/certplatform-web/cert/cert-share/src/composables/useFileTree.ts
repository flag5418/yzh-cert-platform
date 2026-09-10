import { ref } from 'vue'

export interface TreeNode {
  id: string | number
  name: string
  type: 'organization' | 'standard' | 'stage' | 'folder' | 'file'
  children?: TreeNode[]
  fileCode?: string
  convertStatus?: string
  ruleStatus?: 'none' | 'configured' | 'failed'
  directoryCode?: string
  _loaded?: boolean
  _loading?: boolean
}

export function useFileTree() {
  const fileTreeData = ref<TreeNode[]>([])
  const loading = ref(false)

  async function loadTree() {
    loading.value = true
    try {
      // TODO: 待后端目录树 API 就绪后补充
      fileTreeData.value = []
    } finally {
      loading.value = false
    }
  }

  async function loadStageFiles(stage: TreeNode) {
    stage._loaded = true
  }

  return {
    fileTreeData,
    loading,
    loadTree,
    loadStageFiles
  }
}
