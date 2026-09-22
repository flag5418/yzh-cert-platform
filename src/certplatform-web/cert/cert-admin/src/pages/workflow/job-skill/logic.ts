/**
 * JobSkillLogic — 技能管理 Logic（组合页面，SingleTableCore 架构）
 *
 * 左侧分类 + 右侧技能表格
 * 后端：WfSkillController (YzhControllerBase<Skill>，路由 api/Workflow/WfSkill)
 */

import { SingleTableCore } from '@yzh-core'
import { ref } from 'vue'

export class JobSkillLogic extends SingleTableCore<any> {
  controllerName = 'Workflow/WfSkill'

  /** 当前选中分类（空 = 全部），由页面在 dataLoader 入参中带上 */
  currentCategory = ref('')

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return {
      SkillType: 'manual',
      SortOrder: 0,
      IsValid: 1,
      CategoryCode: this.currentCategory.value || '',
    }
  }
}

export default JobSkillLogic
