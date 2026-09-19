/**
 * JobSkillLogic — 技能管理 Logic（组合页面）
 *
 * 左侧分类 + 右侧技能表格
 * 后端：JobSkillController (YzhControllerBase<JobSkill>)
 */

import { CrudPageLogic } from '@yzh-core'

export class JobSkillLogic extends CrudPageLogic<any> {
  controllerName = 'Workflow/JobSkill'

  /** 初始化 */
  async init(): Promise<void> {
    await this.loadConfig()
  }
}

export default JobSkillLogic
