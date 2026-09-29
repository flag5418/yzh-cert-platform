/**
 * 企业-阶段-标准关联 API（专家端）
 *
 * ★ 契约 = core 的 `AssociationApi`（6 个方法），与后端
 *   `EnterpriseStageController` 的「树形表格选择器」一节一一对应。
 *   参照实现：`yzh.vue.core/src/api/system/role-api.ts`。
 *
 * ★ 隔离由后端完成 —— `cert_enterprise_stage` 无 `OrgCode` 列，后端按
 *   「本工作区的企业集合」收敛；前端**不传任何隔离参数**。
 *
 * ★ 勾选链路只回传 `{ Code, NodeType }`（见 `AssociationTreeCore.buildSelections`），
 *   所以「阶段 × 标准」被编码进叶子的 `Code`（`STAGE:{阶段码}|STD:{标准Code}`），
 *   由后端解析。前端**不需要**知道这个格式。
 */
import { yzhApi } from '@yzh-core'
import type {
  ApiResponse,
  AssociationDto,
  AssociationSelection,
  CheckTreeNode,
  TreeNode,
} from '@yzh-core'
import { unwrap } from '@yzh-core'

const BASE = '/api/Auditor/EnterpriseStage'

/** 左树根节点：本工作区全部有效企业 */
export async function getEnterpriseTreeRoot(): Promise<TreeNode[]> {
  const res = await yzhApi.post<ApiResponse<TreeNode[]>>(`${BASE}/tree/root`, {})
  return unwrap(res, [])
}

/**
 * 左树子节点。
 *
 * 本页左树是**扁平**的企业列表（后端 `IsLeaf = true`），页面 `:lazy="false"` 不会调用；
 * 保留此函数只为完整实现 `AssociationApi` 契约。
 */
export async function getEnterpriseTreeChildren(
  parentCode: string,
  level = 0,
): Promise<TreeNode[]> {
  const res = await yzhApi.post<ApiResponse<TreeNode[]>>(`${BASE}/tree/children`, {
    ParentCode: parentCode,
    Level: level,
  })
  return unwrap(res, [])
}

/** 右侧「阶段 → 标准」扁平列表（含 `ParentCode` / `CheckFlag`；展示字段在 `Extra` 内） */
export async function getStageStandardTree(enterpriseCode: string): Promise<CheckTreeNode[]> {
  const res = await yzhApi.post<ApiResponse<CheckTreeNode[]>>(`${BASE}/checkTree`, {
    ContextCode: enterpriseCode,
  })
  return unwrap(res, [])
}

/**
 * 企业目录初始化回执（关联建立时**预热**一次；真正的初始化在每次读资料接口时都会自动跑）
 *
 * ★ `Reason` 是原因码，前端据此区分「等待态」与「真故障」，⛔ 不要匹配 `Message` 文案。
 */
export interface DirectoryInitResult {
  StageCode: string
  StandardCode: string
  Initialized: boolean
  Message?: string | null
  /**
   * - `''` 成功
   * - `template_missing` 该机构×标准×阶段**没有目录模板** —— 等待态：管理端补齐模板后
   *   资料目录会自动初始化，**不需要重新建立关联**
   * - `org_unbound` 企业未绑定机构 —— 等待态：补齐机构后自动初始化
   * - `invalid_request` 入参缺失 / 写模板域等调用错误（属代码缺陷）
   */
  Reason?: string
}

/** 批量建关联；`Applied` = 服务端确认「现已关联」的节点 Code 集合（供前端局部更新缓存） */
export async function checkAdd(
  enterpriseCode: string,
  selections: AssociationSelection[],
): Promise<{ Updated: number; Applied?: string[]; DirectoryInit?: DirectoryInitResult[] }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number; Applied?: string[]; DirectoryInit?: DirectoryInitResult[] }>>(
    `${BASE}/check/add`,
    { ContextCode: enterpriseCode, Selections: selections },
  )
  return unwrap(res, { Updated: 0 })
}

/** 批量解除关联（后端**物理删除**，非软删） */
export async function checkRemove(
  enterpriseCode: string,
  selections: AssociationSelection[],
): Promise<{ Updated: number }> {
  const res = await yzhApi.post<ApiResponse<{ Updated: number }>>(`${BASE}/check/remove`, {
    ContextCode: enterpriseCode,
    Selections: selections,
  })
  return unwrap(res, { Updated: 0 })
}

/** 本工作区全部关联对（前端本地缓存初始化 → 左树 badge 计数） */
export async function getAllAssociations(): Promise<AssociationDto[]> {
  const res = await yzhApi.post<ApiResponse<AssociationDto[]>>(`${BASE}/check/all`, {})
  return unwrap(res, [])
}
