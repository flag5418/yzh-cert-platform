import { ref, computed } from 'vue'
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
  /** 机构编码（cbCode） */
  orgCode?: string
  /** 标准 GUID 编码（stdCode，数据库主键） */
  stdCode?: string
  /** 标准显示编码（standardCode，如 ISO 9001:2015） */
  standardCode?: string
  /** 阶段编码 */
  phaseCode?: string
  /** 阶段定义编码（cert_phase_definition.Code，报告模板外键） */
  phaseDefinitionCode?: string
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

  /** 组织树 → el-tree 结构（阶段节点不设 children，由预加载填充） */
  function transformOrgTree(data: any[]): TreeNode[] {
    return (data || []).map((org) => ({
      id: org.id,
      name: org.label || org.name,
      type: 'organization' as const,
      orgCode: org.cbCode,
      children: (org.children || []).map((std: any) => ({
        id: std.id,
        name: std.label || std.name,
        type: 'standard' as const,
        orgCode: std.cbCode,
        stdCode: std.stdCode,
        standardCode: std.standardCode,
        children: (std.children || []).map((phase: any) => ({
          id: phase.id,
          name: phase.label || phase.name,
          type: 'stage' as const,
          orgCode: phase.cbCode,
          stdCode: phase.stdCode,
          standardCode: phase.standardCode,
          phaseCode: phase.phaseCode,
          phaseDefinitionCode: phase.phaseDefinitionCode || '',
          directoryCode: extractDirectoryCode(phase.id),
          children: undefined,
          _loaded: false,
        }))
      }))
    }))
  }

  /** 递归收集树中所有阶段节点 */
  function collectStageNodes(nodes: TreeNode[]): TreeNode[] {
    const stages: TreeNode[] = []
    for (const n of nodes) {
      if (n.type === 'stage') stages.push(n)
      if (n.children) stages.push(...collectStageNodes(n.children))
    }
    return stages
  }

  /** 加载组织树，并行预加载所有阶段的文件夹 */
  async function loadTree() {
    loading.value = true
    try {
      const orgTree = await getOrganizationTree()
      const tree = transformOrgTree(orgTree)

      // 并行预加载所有阶段的文件夹数据
      const stages = collectStageNodes(tree)
      await Promise.all(stages.map(async (stage) => {
        const directoryCode = stage.directoryCode || extractDirectoryCode(String(stage.id))
        if (!directoryCode) return
        try {
          const folders = await getFolders(directoryCode)
          stage.children = buildFolderNodes(folders, directoryCode)
          stage._loaded = true
        } catch {
          stage.children = []
          stage._loaded = true
        }
      }))

      // 只赋值一次，确保 el-tree 拿到的是完整数据
      fileTreeData.value = tree

      return fileTreeData.value
    } catch (e: any) {
      ElMessage.error('加载目录树失败：' + (e?.message || '未知错误'))
      fileTreeData.value = []
      return []
    } finally {
      loading.value = false
    }
  }

  /** 将后端文件夹树递归转换为前端 TreeNode 结构（递归处理 Children） */
  function buildFolderNodes(folders: any[], directoryCode: string): TreeNode[] {
    return (folders || []).map((folder: any) => ({
      id: folder.FolderCode || folder.folderCode,
      name: folder.FolderName || folder.folderName,
      type: 'folder' as const,
      folderCode: folder.FolderCode || folder.folderCode,
      directoryCode,
      raw: folder,
      children: (folder.Children && folder.Children.length > 0)
        ? buildFolderNodes(folder.Children, directoryCode)
        : [{ id: '__placeholder__' } as TreeNode],
      _loaded: folder.Children && folder.Children.length > 0,
    }))
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
      stageNode.children = buildFolderNodes(folders, directoryCode)
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

  /** 默认展开 keys：组织 + 标准（阶段由用户手动点击展开触发懒加载） */
  const defaultExpandedKeys = computed(() => {
    const keys: (string | number)[] = []
    for (const org of fileTreeData.value) {
      keys.push(org.id)
      for (const std of (org.children || [])) {
        keys.push(std.id)
      }
    }
    return keys
  })

  return {
    fileTreeData,
    loading,
    defaultExpandedKeys,
    loadTree,
    loadStageFiles,
    loadFolderFiles,
    handleDownload,
    refreshNode,
    extractDirectoryCode,
    transformOrgTree,
  }
}
