/**
 * certcore useFileTree —— 目录树数据逻辑
 * 从 DocExtractionRule/index.vue 提取并泛化：
 *   - 组织树转换（机构→标准→阶段）
 *   - 阶段懒加载（单次请求返回文件夹+文件）
 *   - 目录编码提取
 * 供 CertDirectoryTree / DirectoryManager / 规则定义 / 报告定义等页面复用
 */
import { ref } from 'vue'
import { useDirectoryApi } from './useDirectoryApi'

export function useFileTree() {
  const fileTreeData = ref([])
  const loading = ref(false)
  const { getOrganizationTree, getStageFiles } = useDirectoryApi()

  /** 从阶段 ID 提取目录编码（SDC-标准|阶段） */
  function extractDirectoryCode(stageId) {
    if (!stageId) return null
    if (String(stageId).startsWith('SDC-')) return stageId
    const parts = String(stageId).split('|')
    if (parts.length >= 3) {
      const standardCode = parts[1].replace(/[:\-\s]/g, '')
      const phaseCode = parts[2].replace(/[\-\s]/g, '')
      return `SDC-${standardCode}|${phaseCode}`
    }
    return null
  }

  /** 组织树 → el-tree 结构（阶段节点 children 空，点击懒加载） */
  function transformOrgTree(data) {
    return (data || []).map((org) => ({
      id: org.id,
      name: org.label || org.name,
      type: 'organization',
      children: (org.children || []).map((std) => ({
        id: std.id,
        name: std.label || std.name,
        type: 'standard',
        standardCode: std.code || std.id,
        children: (std.children || []).map((phase) => ({
          id: phase.id,
          name: phase.label || phase.name,
          type: 'stage',
          directoryCode: extractDirectoryCode(phase.id),
          children: [],
          _loaded: false,
          _loading: false
        }))
      }))
    }))
  }

  /** 后端 StageFolderNode → el-tree 结构 */
  function transformStageFileTree(folderNodes, depth = 0) {
    if (!Array.isArray(folderNodes)) return []
    const result = []
    for (const folder of folderNodes) {
      const folderNode = {
        id: folder.Code || folder.FolderCode || `folder-${depth}-${result.length}`,
        name: folder.Name || folder.FolderName || `文件夹${result.length + 1}`,
        type: 'folder',
        ruleStatus: 'none',
        _raw: folder,
        children: []
      }
      if (folder.Children && folder.Children.length > 0) {
        folderNode.children.push(...transformStageFileTree(folder.Children, depth + 1))
      }
      if (folder.Files && folder.Files.length > 0) {
        folderNode.children.push(...folder.Files.map((file, idx) => ({
          id: file.FileCode || `file-${depth}-${idx}`,
          fileCode: file.FileCode || `file-${depth}-${idx}`,
          name: file.FileName || `文件${idx + 1}`,
          type: 'file',
          ruleStatus: file.RuleStatus || 'none',
          convertStatus: file.ConvertStatus || file.convertStatus || '',
          extractFieldCount: file.ExtractFieldCount || 0,
          tableDefCount: file.TableDefCount || 0,
          storagePath: file.StoragePath,
          convertedStoragePath: file.ConvertedStoragePath,
          mimeType: file.MimeType,
          fileSize: file.FileSize,
          _raw: file
        })))
      }
      result.push(folderNode)
    }
    return result
  }

  /** 统计节点数量 */
  function countNodes(nodes) {
    if (!Array.isArray(nodes)) return 0
    return nodes.reduce((sum, node) => sum + 1 + countNodes(node.children), 0)
  }

  /** 加载组织树 */
  async function loadTree() {
    loading.value = true
    try {
      const { ok, data } = await getOrganizationTree()
      fileTreeData.value = ok ? transformOrgTree(data) : []
      return fileTreeData.value
    } finally {
      loading.value = false
    }
  }

  /** 加载阶段文件树（懒加载，挂载到 stageNode.children） */
  async function loadStageFiles(stageNode) {
    const directoryCode = stageNode.directoryCode || extractDirectoryCode(stageNode.id)
    if (!directoryCode) throw new Error('无法获取目录编码')
    const { ok, data } = await getStageFiles(directoryCode)
    if (!ok) throw new Error('加载阶段文件失败')
    const treeData = transformStageFileTree(data?.Folders || data || [])
    if (Array.isArray(stageNode.children)) {
      stageNode.children.splice(0, stageNode.children.length, ...treeData)
    } else {
      stageNode.children = treeData
    }
    stageNode._loaded = true
    return treeData
  }

  return {
    fileTreeData,
    loading,
    loadTree,
    loadStageFiles,
    extractDirectoryCode,
    transformOrgTree,
    transformStageFileTree,
    countNodes
  }
}
