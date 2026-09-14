import { yzhApi } from '@yzh-core'

export const certOrgStandardApi = {
  /** 左树：所有有效认证机构 */
  treeRoot: () => yzhApi.post('/api/Foundation/CertOrgStandard/tree/root'),
  /** 右表：全量标准 + checked 状态 */
  list: (orgCode: string) =>
    yzhApi.post('/api/Foundation/CertOrgStandard/list', { OrgCode: orgCode }),
  /** 单条保存：勾选/取消 */
  save: (data: { OrgCode: string; StandardCode: string; Linked: boolean }) =>
    yzhApi.post('/api/Foundation/CertOrgStandard/save', data),
}
