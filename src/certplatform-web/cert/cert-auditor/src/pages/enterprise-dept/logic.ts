/**
 * EnterpriseDeptLogic — 企业机构管理 Logic（SingleTableCore 架构）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseDeptController.cs（YzhControllerBase<CertEnterpriseDept>）
 * 路由：/api/Auditor/EnterpriseDept
 *
 * 按企业建立部门树（不限层级），每部门带负责人信息。
 * 入场计划选择部门、定访谈对象时引用。
 *
 * 必须先选企业才能查（后端 OnBuildingFilter 强制：未选企业返回空）。
 */

import { SingleTableCore } from '@yzh-core'

export class EnterpriseDeptLogic extends SingleTableCore<any> {
  controllerName = 'Auditor/EnterpriseDept'

  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1, Sort: 0 }
  }
}

export default EnterpriseDeptLogic
