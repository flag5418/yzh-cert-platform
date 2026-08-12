/**
 * certcore 标准目录 API 封装
 * 统一标准目录相关接口，页面不再直接拼 URL
 */
import http from '@/api/http'
import { unwrap } from '../utils/api'

export function useDirectoryApi() {
  /** 机构→标准→阶段 组织树 */
  async function getOrganizationTree() {
    const res = await http.get('/api/standard-directory/organization-tree')
    return unwrap(res)
  }

  /** 阶段完整文件树（文件夹+文件，含规则属性） */
  async function getStageFiles(directoryCode) {
    const res = await http.get(`/api/standard-directory/stage-files/${directoryCode}`)
    return unwrap(res)
  }

  /** 构造下载 URL（优先转换后路径，前端已由调用方决定） */
  function buildDownloadUrl(path) {
    if (!path) return ''
    return `/api/standard-directory/download?path=${encodeURIComponent(path)}`
  }

  /** 文件预览/下载优先路径：convertedStoragePath ?? storagePath ?? fileCode */
  function pickDownloadPath(file) {
    return (file?.convertedStoragePath || file?.ConvertedStoragePath)
      || (file?.storagePath || file?.StoragePath)
      || (file?.fileCode || file?.FileCode)
      || ''
  }

  return { getOrganizationTree, getStageFiles, buildDownloadUrl, pickDownloadPath }
}
