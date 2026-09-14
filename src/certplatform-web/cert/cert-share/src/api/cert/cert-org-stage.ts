import { yzhApi } from '@yzh-core'

export const certOrgStageApi = {
  /** 左树：所有有效认证机构 */
  treeRoot: () => yzhApi.post('/api/Foundation/CertOrgStage/tree/root'),
  /** 右表：全量阶段 + checked 状态 */
  list: (orgCode: string) =>
    yzhApi.post('/api/Foundation/CertOrgStage/list', { OrgCode: orgCode }),
  /** 单条保存：勾选/取消 */
  save: (data: { OrgCode: string; StageCode: string; Linked: boolean }) =>
    yzhApi.post('/api/Foundation/CertOrgStage/save', data),
}
