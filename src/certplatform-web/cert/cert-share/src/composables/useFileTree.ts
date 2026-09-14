import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getOrganizationTree,
  getFolders,
  getFiles,
  downloadFile,
} from './useDirectoryApi'
import type {
  StandardDirectoryFile,
} from '@share/types'

// ========================================================
// 树节点类型（统一渲染用）
// ========================================================

export interface TreeNode {
  id: string | number
  name: string
  type: 'organization' | 'standard' | 'stage' | 'folder' | 'file'
  children?: TreeNode[]
  /** 原始数据引用 */
  raw?: any
  /** 关联的目录编码 */
  directoryCode?: string
  /** 标准编码 */
  standardCode?: string
  /** 阶段编码 */
  phaseCode?: string
  /** 文件编码 */
  fileCode?: string
  /** 文件夹编码 */
  folderCode?: string
  /** 转换状态 */
  convertStatus?: string
  /** 规则状态 */
  ruleStatus?: 'none' | 'configured' | 'failed'
  /** 是否已展开 */
  expanded?: boolean
  /** 是否已加载 */
  _loaded?: boolean
  /** 加载中 */
  _loading?: boolean
}

// ========================================================
// 组合式函数：文件树管理
// ========================================================

export function useFileTree() {
  const fileTreeData = ref<TreeNode[]>([])
  const loading = ref(false)

  /** 从阶段 ID 提取目录编码（SDC-标准|阶段） */
  function extractDirectoryCode(stageId: string): string | null {
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
  function transformOrgTree(data: any[]): TreeNode[] {
    return (data || []).map((org) => ({
      id: org.id,
      name: org.label || org.name,
      type: 'organization' as const,
      expanded: true,
      children: (org.children || []).map((std: any) => ({
        id: std.id,
        name: std.label || std.name,
        type: 'standard' as const,
        standardCode: std.code || std.id,
        expanded: false,
        children: (std.children || []).map((phase: any) => ({
          id: phase.id,
          name: phase.label || phase.name,
          type: 'stage' as const,
          directoryCode: extractDirectoryCode(phase.id),
          children: [],
          expanded: false,
          _loaded: false,
          _loading: false,
        }))
      }))
    }))
  }

  /** 加载组织树 */
  async function loadTree() {
    loading.value = true
    try {
      const orgTree = await getOrganizationTree()
      fileTreeData.value = transformOrgTree(orgTree)
      return fileTreeData.value
    } catch (e: any) {
      ElMessage.error('加载目录树失败：' + (e?.message || '未知错误'))
      fileTreeData.value = []
      return []
    } finally {
      loading.value = false
    }
  }

  /** 加载阶段文件树（懒加载，挂载到 stageNode.children） */
  async function loadStageFiles(stageNode: TreeNode) {
    if (stageNode._loaded || stageNode._loading) return
    
    const directoryCode = stageNode.directoryCode || extractDirectoryCode(String(stageNode.id))
    if (!directoryCode) {
      throw new Error('无法获取目录编码')
    }
    
    stageNode._loading = true
    try {
      const folders = await getFolders(directoryCode)
      stageNode.children = (folders || []).map((folder: any) => ({
        id: folder.FolderCode || folder.folderCode,
        name: folder.FolderName || folder.folderName,
        type: 'folder' as const,
        folderCode: folder.FolderCode || folder.folderCode,
        directoryCode,
        raw: folder,
        children: [],
        _loaded: false,
      }))
      stageNode._loaded = true
      return stageNode.children
    } catch (e: any) {
      ElMessage.error('加载阶段文件失败：' + (e?.message || ''))
      return []
    } finally {
      stageNode._loading = false
    }
  }

  /** 加载文件夹文件（懒加载） */
  async function loadFolderFiles(folderNode: TreeNode) {
    if (folderNode._loaded || folderNode._loading) return
    
    const folderCode = folderNode.folderCode
    if (!folderCode) return
    
    folderNode._loading = true
    try {
      const files = await getFiles(folderCode)
      folderNode.children = (files || []).map((file: any) => ({
        id: file.FileCode || file.fileCode,
        name: file.FileName || file.fileName,
        type: 'file' as const,
        fileCode: file.FileCode || file.fileCode,
        directoryCode: folderNode.directoryCode,
        raw: file,
        convertStatus: file.ConvertStatus || file.convertStatus,
        ruleStatus: 'none' as const,
      }))
      folderNode._loaded = true
      return folderNode.children
    } catch (e: any) {
      ElMessage.error('加载文件失败：' + (e?.message || ''))
      return []
    } finally {
      folderNode._loading = false
    }
  }

  /** 下载文件 */
  async function handleDownload(file: StandardDirectoryFile) {
    if (!file.StoragePath) {
      ElMessage.warning('文件未上传，无法下载')
      return
    }
    try {
      const blob = await downloadFile(file.StoragePath)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = file.FileName
      a.click()
      URL.revokeObjectURL(url)
    } catch (e: any) {
      ElMessage.error('下载失败：' + (e?.message || ''))
    }
  }

  /** 刷新当前节点 */
  function refreshNode(node: TreeNode) {
    node._loaded = false
    node.children = []
    if (node.type === 'stage') {
      loadStageFiles(node)
    } else if (node.type === 'folder') {
      loadFolderFiles(node)
    }
  }

  return {
    fileTreeData,
    loading,
    loadTree,
    loadStageFiles,
    loadFolderFiles,
    handleDownload,
    refreshNode,
    extractDirectoryCode,
    transformOrgTree,
  }
}
