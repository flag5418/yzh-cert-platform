/**
 * EnterpriseLogic — 企业管理 Logic（SingleTableCore 架构）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseController.cs（YzhControllerBase<Enterprise>）
 * 路由：/api/Auditor/Enterprise
 *
 * 全部差异都在后端，前端零手写 CRUD：
 * - 工作区隔离：后端 OnBuildingFilter 强制 OrgCode = 当前登录用户所属工作区
 * - 企业编号自动生成：后端 NextEnterpriseNoAsync（ENT-0001 …）
 * - 建档 2 表事务：cert_enterprise 写入 + 组织树「企业信息 / 企业节点」两层同步
 *   （虚拟体系机构 → 专家注册人员 → **企业信息** → 具体企业）
 * - 改名 / 禁用 / 启用 / 删除 均同步机构树节点（后端 Attach/Detach 辅助方法）
 *
 * 列 / 表单 / 搜索 / 按钮全部由后端 EntityConfig 驱动
 * （CertPlatform.Auditor/Assets/EntityConfigs/Enterprise.json）。
 */

import { SingleTableCore } from '@yzh-core'

export class EnterpriseLogic extends SingleTableCore<any> {
  controllerName = 'Auditor/Enterprise'

  /**
   * 新增默认值
   * ⚠️ 只放**表单里真实存在**的字段（否则是「提交了看不见的值」）：
   * · `Status` —— 用户 2026-09-26 裁定：与 `IsValid` 是两个含义，但 `Status` 本身无意义 → **已从实体/表/配置彻底删除**
   * · `Sort`   —— 企业表不参与手工排序 → 已从表单配置移除
   */
  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1 }
  }
}

export default EnterpriseLogic
