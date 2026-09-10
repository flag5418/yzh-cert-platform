/**
 * SysDept API - 部门管理
 *
 * 后端接口（Vol 框架 TreeTable 风格）：
 * - getTreeTableRootData: 加载根节点
 * - getTreeTableChildrenData: 懒加载子节点
 * - getPageData: 分页查询（带 parent 条件）
 * - Add: 新增
 * - Update: 修改
 * - Del: 删除
 */

import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

/** 部门实体（camelCase，由 yzhApi.normalizeKeys 自动转换） */
export interface SysDept {
  departmentId: string
  departmentName: string
  departmentCode?: string
  parentId?: string
  departmentType?: string
  enable?: number
  remark?: string
  createDate?: string
  creator?: string
  modifier?: string
  modifyDate?: string
}

/** 树节点 DTO（后端返回） */
export interface DeptTreeNode {
  departmentId: string
  departmentName: string
  departmentCode?: string
  parentId?: string
  enable?: number
  remark?: string
  hasChildren: boolean
}

/** 分页查询 */
export async function getDeptPage(params: PageParams): Promise<Page<SysDept>> {
  return yzhApi.post<Page<SysDept>>('/api/Sys_Department/getPageData', params)
}

/** 加载根节点 */
export async function getDeptRootNodes(): Promise<DeptTreeNode[]> {
  const res = await yzhApi.post<{ rows: DeptTreeNode[]; total: number }>(
    '/api/Sys_Department/getTreeTableRootData',
    {}
  )
  return res.rows
}

/** 加载子节点 */
export async function getDeptChildren(parentId: string): Promise<DeptTreeNode[]> {
  const res = await yzhApi.post<{ rows: DeptTreeNode[] }>(
    '/api/Sys_Department/getTreeTableChildrenData',
    { departmentId: parentId }
  )
  return res.rows
}

/** 新增部门（Vol 框架 Add 接口） */
export async function addDept(data: Partial<SysDept>): Promise<any> {
  const saveModel = {
    TableName: 'Sys_Department',
    MainData: data,
    DetailData: [],
    DelKeys: []
  }
  return yzhApi.post('/api/Sys_Department/Add', saveModel)
}

/** 修改部门（Vol 框架 Update 接口） */
export async function updateDept(data: Partial<SysDept>): Promise<any> {
  const saveModel = {
    TableName: 'Sys_Department',
    MainData: data,
    DetailData: [],
    DelKeys: []
  }
  return yzhApi.post('/api/Sys_Department/Update', saveModel)
}

/** 删除部门（Vol 框架 Del 接口） */
export async function deleteDept(departmentIds: string[]): Promise<any> {
  return yzhApi.post('/api/Sys_Department/Del', departmentIds)
}
