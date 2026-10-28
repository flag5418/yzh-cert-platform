/**
 * ClauseAttrDefLogic — 条款属性池（L0）Logic（SingleTableCore 架构）
 *
 * 后端：`ClauseAttrDefController`（`YzhControllerBase<ClauseAttrDef>`）
 * 路由：`api/Admin/Cert/ClauseAttrDef` ｜ 表：`cert_clause_attr_def`
 *
 * ★ 业务定位（`docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md` §三）：
 *   属性池 = **"世界上有哪些属性"**（受控枚举，30–50 行），
 *   某条款用哪几条由「条款属性配置」页决定（5–12 行/条款）。
 *   ⇒ 本页 ⛔ 不出现「值来源」「适用条款」—— 那是 L1 的事。
 *
 * ★ 三条不可协商的约束：
 *   1. `Code` 是**人可读驼峰**（`Artifacts`），不是 GUID —— 它被 L1 的 `AttrCode`
 *      引用、被提示词引用、被人排查；GUID 会让所有人对着乱码猜。
 *      ⇒ 新增时由用户填（框架只在 `YzhControllerBase.AddCore` 里自动生成，本实体
 *      走同一路径但业务要求人工指定语义化码）。
 *   2. `ValueType` 只有 5 种：`text` / `dict` / `bool` / `json` / `prompt`（⛔ 无 int / date）。
 *   3. `DictCode` **仅 `dict` / `json` 必填**，其余类型后端会强制清空 ——
 *      前端据此做联动必填，避免用户白填后被后端悄悄抹掉。
 *
 * ★ 覆写清单：
 *   - `controllerName` —— 三层同构路由段
 *   - `formFields`    —— `DictCode` 按 `ValueType` 联动必填 + 占位提示
 *   - `defaultValues` —— 新增默认 `text` / 启用 / 排序 0（后端自动取 max+10）
 *   - `entityNameField` —— 删除确认显示中文属性名
 */
import { SingleTableCore } from '@yzh-core'
import type { YzhFormField } from '@yzh-core'
import { ref } from 'vue'

/** 需要字典编码的值类型（与后端 `ClauseAttrDefController.TypesNeedDict` 保持一致） */
const TYPES_NEED_DICT = ['dict', 'json']

/** 值类型 → 一句话说明（给配置者看，不落库） */
const VALUE_TYPE_TIP: Record<string, string> = {
  text: '自由文本，逐条款手填。适合「条款意图」「适用条件」这类一句话描述。',
  dict: '单值枚举，值取自指定字典（如 clause_obligation）。适合「应/宜/可」这类固定档位。',
  bool: '是 / 否两态。适合「是否强制」「是否需现场取证」。',
  json: '受控多选，值取自指定字典并以 JSON 数组存。适合「涉及过程」这类可多选的集合。',
  prompt:
    '值即提示词正文（多行）。三条铁律：① 要求内部输出结构化 JSON ② 只产「读到什么」不产结论 ③ 找不到交空禁推测。',
}

export class ClauseAttrDefLogic extends SingleTableCore<any> {
  controllerName = 'Admin/Cert/ClauseAttrDef'

  /** 新增默认值：文本类型 + 启用（Sort 由后端按 max+10 补，这里给 0 表示"未指定"） */
  protected override get defaultValues(): Record<string, any> {
    return {
      ValueType: 'text',
      IsValid: 1,
      Sort: 0,
    }
  }

  /** 删除确认 / 提示里显示的实体名称字段 */
  protected override get entityNameField(): string {
    return 'AttrName'
  }

  /**
   * 表单里当前选中的值类型（★ 独立镜像 ref，⛔ 不读 `this.formData`）。
   *
   * <p>为什么不能直接读 `formData.ValueType`：`YzhFormDialog` 的 `v-model` 回写是
   * `logic.formData = $event` —— 而页面逻辑实例**不是** `reactive` 的，
   * 这一步会把 `SingleTableCore.formData`（reactive 对象）整个替换成**普通对象**。
   * 之后所有 `this.formData.X` 读取都不再建立响应依赖 ⇒ 依赖它的 getter 永远不重算
   * （实测：值类型切成「字典」后，「字典编码」的必填星号不出现）。
   * ⇒ 用 `fieldProps.onChange` 把值镜像进本 ref，getter 只读 ref。</p>
   */
  private formValueType = ref('text')

  /** 当前表单选中的值类型（小写；空值按 text 处理） */
  get currentValueType(): string {
    return String(this.formValueType.value || 'text').toLowerCase()
  }

  /** 值类型说明文案（弹窗 prepend 提示用） */
  get valueTypeTip(): string {
    return VALUE_TYPE_TIP[this.currentValueType] ?? ''
  }

  /** 值类型下拉变更 → 同步镜像（同时清掉不属于新类型的字典码，避免用户白填） */
  onValueTypeChange(value: unknown): void {
    this.formValueType.value = String(value ?? 'text')
    if (!TYPES_NEED_DICT.includes(this.currentValueType)) {
      // 只清「本地表单」，是否落库由后端 ValidateShape 决定（非 dict/json 一律置空）
      this.formData.DictCode = ''
    }
  }

  /** 新增：镜像回 text（与 `defaultValues.ValueType` 一致） */
  override openAddDialog(): void {
    this.formValueType.value = 'text'
    super.openAddDialog()
  }

  /** 编辑：镜像取行上的值类型 */
  override openEditDialog(row: any): void {
    this.formValueType.value = String(row?.ValueType || 'text')
    super.openEditDialog(row)
  }

  /**
   * 表单字段：`DictCode` 按 `ValueType` 联动必填。
   *
   * <p>★ 为什么必须在前端做：后端 `ValidateShape` 对非 dict/json 类型会**静默清空**
   * `DictCode`（避免"留着上一版的字典码"造成下游误判）。用户填了却被抹掉且不报错
   * ⇒ 必须在界面上直接告诉「当前类型不需要字典」。</p>
   */
  override get formFields(): YzhFormField[] {
    const needDict = TYPES_NEED_DICT.includes(this.currentValueType)
    return super.formFields.map((f) => {
      if (f.prop === 'ValueType') {
        return {
          ...f,
          fieldProps: { ...(f.fieldProps as any), onChange: (v: unknown) => this.onValueTypeChange(v) },
        }
      }
      if (f.prop !== 'DictCode') return f
      return {
        ...f,
        required: needDict,
        placeholder: needDict
          ? '必填：如 clause_obligation / process_domain'
          : '当前值类型不需要字典，填了也会被清空',
      }
    })
  }
}

export default ClauseAttrDefLogic
