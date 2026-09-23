import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getOrganizationTree,
  getStageFileTree,
  downloadFile,
} from './useDirectoryApi'
import type {
  StandardDirectoryFile,
} from '@share/types'

// ========================================================
// 树节点类型（统一渲染用）
// ========================================================

export interface TreeNode {
  /** 业务编码（唯一标识，统一使用 Code） */
  Code: string | number
  /** 显示名称 */
  Name: string
  /** 节点类型 */
  Type: 'organization' | 'standard' | 'stage' | 'folder' | 'file'
  Children?: TreeNode[]
  /** 原始数据引用 */
  Raw?: any
  /** 关联的目录编码 */
  DirectoryCode?: string
  /** 机构编码（cbCode） */
  OrgCode?: string
  /** 标准 GUID 编码（stdCode，数据库主键） */
  StdCode?: string
  /** 标准显示编码（standardCode，如 ISO 9001:2015） */
  StandardCode?: string
  /** 阶段编码 */
  PhaseCode?: string
  /** 阶段定义编码（cert_phase_definition.Code，报告模板外键） */
  PhaseDefinitionCode?: string
  /** 文件编码 */
  FileCode?: string
  /** 文件夹编码 */
  FolderCode?: string
  /** 转换状态 */
  ConvertStatus?: string
  /** 规则状态 */
  RuleStatus?: 'none' | 'configured' | 'failed'
  /** 是否已展开 */
  Expanded?: boolean
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
      Code: org.id,
      Name: org.label || org.name,
      Type: 'organization' as const,
      OrgCode: org.cbCode,
      Children: (org.children || []).map((std: any) => ({
        Code: std.id,
        Name: std.label || std.name,
        Type: 'standard' as const,
        OrgCode: std.cbCode,
        StdCode: std.stdCode,
        StandardCode: std.standardCode,
        Children: (std.children || []).map((phase: any) => ({
          Code: phase.id,
          Name: phase.label || phase.name,
          Type: 'stage' as const,
          OrgCode: phase.cbCode,
          StdCode: phase.stdCode,
          StandardCode: phase.standardCode,
          PhaseCode: phase.phaseCode,
          PhaseDefinitionCode: phase.phaseDefinitionCode || '',
          DirectoryCode: extractDirectoryCode(phase.id),
          Children: undefined,
          _loaded: false,
        }))
      }))
    }))
  }

  /** 递归收集树中所有阶段节点 */
  function collectStageNodes(nodes: TreeNode[]): TreeNode[] {
    const stages: TreeNode[] = []
    for (const n of nodes) {
      if (n.Type === 'stage') stages.push(n)
      if (n.Children) stages.push(...collectStageNodes(n.Children))
    }
    return stages
  }

  /** 加载组织树，并行预加载所有阶段的文件夹 */
  async function loadTree() {
    loading.value = true
    try {
      const orgTree = await getOrganizationTree()
      const tree = transformOrgTree(orgTree)

      // 并行预加载所有阶段（stage-files 单请求：文件夹+文件+规则状态一次到位）
      const stages = collectStageNodes(tree)
      await Promise.all(stages.map(async (stage) => {
        const directoryCode = stage.DirectoryCode || extractDirectoryCode(String(stage.Code))
        if (!directoryCode) return
        try {
          const { folders } = await getStageFileTree(directoryCode)
          stage.Children = buildStageNodes(folders, directoryCode)
          stage._loaded = true
        } catch {
          stage.Children = []
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

  /**
   * 加载阶段文件树（懒加载，挂载到 stageNode.Children）。
   * 对齐历史老项目：单请求 stage-files 返回文件夹+文件+规则状态+根目录孤儿文件，
   * 替代旧实现「folders-only + 逐文件夹再查文件」的两级请求模式。
   */
  async function loadStageFiles(stageNode: TreeNode) {
    if (stageNode._loaded || stageNode._loading) return
    
    const directoryCode = stageNode.DirectoryCode || extractDirectoryCode(String(stageNode.Code))
    if (!directoryCode) {
      throw new Error('无法获取目录编码')
    }
    
    stageNode._loading = true
    try {
      const { folders } = await getStageFileTree(directoryCode)
      stageNode.Children = buildStageNodes(folders, directoryCode)
      stageNode._loaded = true
      return stageNode.Children
    } catch (e: any) {
      ElMessage.error('加载阶段文件失败：' + (e?.message || ''))
      return []
    } finally {
      stageNode._loading = false
    }
  }

  /** 后端 StageFolderNode（含 Files/根目录虚拟节点）→ 前端 TreeNode */
  function buildStageNodes(folderNodes: any[], directoryCode: string): TreeNode[] {
    return (folderNodes || []).map((folder: any) => {
      const node: TreeNode = {
        Code: folder.Code || folder.FolderCode,
        Name: folder.Name || folder.FolderName,
        Type: 'folder' as const,
        FolderCode: folder.FolderCode || folder.Code,
        DirectoryCode: directoryCode,
        Raw: folder,
        Children: [],
        _loaded: true,
      }
      // 子文件夹
      if (folder.Children?.length) {
        node.Children!.push(...buildStageNodes(folder.Children, directoryCode))
      }
      // 文件夹直属文件
      for (const f of folder.Files || []) {
        node.Children!.push({
          Code: f.FileCode,
          Name: f.FileName,
          Type: 'file' as const,
          FileCode: f.FileCode,
          DirectoryCode: directoryCode,
          FolderCode: f.FolderCode,
          ConvertStatus: f.ConvertStatus || '',
          RuleStatus: f.RuleStatus || 'none',
          Raw: f,
        })
      }
      return node
    })
  }

  /** 加载文件夹文件（懒加载兜底；stage-files 整树模式下一般不再触发） */
  async function loadFolderFiles(folderNode: TreeNode) {
    if (folderNode._loaded || folderNode._loading) return

    const folderCode = folderNode.FolderCode
    if (!folderCode) return

    folderNode._loading = true
    try {
      const { getFiles } = await import('./useDirectoryApi')
      const files = await getFiles(folderCode)
      folderNode.Children = (files || []).map((file: any) => ({
        Code: file.FileCode || file.fileCode,
        Name: file.FileName || file.fileName,
        Type: 'file' as const,
        FileCode: file.FileCode || file.fileCode,
        DirectoryCode: folderNode.DirectoryCode,
        Raw: file,
        ConvertStatus: file.ConvertStatus || file.convertStatus,
        RuleStatus: 'none' as const,
      }))
      folderNode._loaded = true
      return folderNode.Children
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
    node.Children = []
    if (node.Type === 'stage') {
      loadStageFiles(node)
    } else if (node.Type === 'folder') {
      loadFolderFiles(node)
    }
  }

  /** 默认展开 keys：组织 + 标准（阶段由用户手动点击展开触发懒加载） */
  const defaultExpandedKeys = computed(() => {
    const keys: (string | number)[] = []
    for (const org of fileTreeData.value) {
      keys.push(org.Code)
      for (const std of (org.Children || [])) {
        keys.push(std.Code)
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
