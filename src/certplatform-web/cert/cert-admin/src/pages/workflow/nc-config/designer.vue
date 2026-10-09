<!--
  NC 规则配置页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner

  ⚠️ 命名铁律（项目全局规则 §16.9 / YZH 架构铁律）
    数据库列名 = C# 属性名 = TS 字段名 = PascalCase，三处逐字一致，禁止局部改写。
    - 规则实体 ValidationRule → PascalCase（RuleName / RuleCode / RuleJson / LayoutJson / IsValid）
    - 组织树 organization-tree 是服务层手写 DTO（camelCase：id/label/cbCode/stdCode/phaseCode），
      属于已登记的例外，不参与本铁律。
    - RuleJson / LayoutJson 内部的 workflow 配置 schema（nodeId/nodeType/inputPorts…）
      是落库 payload 格式，沿用历史 camelCase，改动会破坏既有数据。

  ★ buildPayload 的两条硬约束（2026-10-08）：
    ① RuleJson 空画布必须发 null —— «NULL = 未配 DAG» 是唯一判据（后端 ValidationRuleRules.HasWorkflow）；
       发 "{}" 会让"未配"被当成"已配"。
    ② IsValid 是 int（1=启用 / 0=禁用），不再是 bool IsActive（铁律九）。
    ⛔ 不要在 :save-config / :tree-config 的内联表达式里写 `//` 行注释 ——
       Vue 编译器按单表达式解析，行注释会直接报 "Error parsing JavaScript expression"（已踩）。
-->
<template>
  <WorkflowDesigner
    title="NC 规则配置"
    :workflow-type="'validation'"
    :tree-config="{
      loadApi: '/api/Admin/Workflow/ValidationRule/filter',
      loadMethod: 'post',
      loadBodyBuilder: (filter) => ({
        Page: 1, PageSize: 200,
        Filters: [
          filter.OrgCode ? { Field: 'OrgCode', Value: filter.OrgCode, Operator: 'Equal' } : null,
          filter.StandardCode ? { Field: 'StandardCode', Value: filter.StandardCode, Operator: 'Equal' } : null,
          filter.PhaseCode ? { Field: 'PhaseCode', Value: filter.PhaseCode, Operator: 'Equal' } : null
        ].filter(Boolean)
      }),
      detailApi: '/api/Admin/Workflow/ValidationRule',
      textField: 'RuleName',
      codeField: 'RuleCode',
      filterLeaf: (item) => item.JudgeMode !== 'manual'
    }"
    :save-config="{
      api: '/api/Admin/Workflow/ValidationRule/update',
      getUrl: (ctx) => (ctx.leaf.Code ? '/api/Admin/Workflow/ValidationRule/update' : '/api/Admin/Workflow/ValidationRule/add'),
      buildPayload: (ctx) => {
        const r = ctx.leaf
        return {
          Code: r.Code,
          OrgCode: r.OrgCode || ctx.filter.OrgCode,
          StandardCode: r.StandardCode || ctx.filter.StandardCode,
          PhaseCode: r.PhaseCode || ctx.filter.PhaseCode,
          ClauseCode: r.ClauseCode,
          RuleCode: r.RuleCode,
          RuleName: r.RuleName,
          RuleNameEn: r.RuleNameEn,
          SeverityIfViolated: r.SeverityIfViolated || 'minor',
          JudgeMode: r.JudgeMode || 'auto',
          RuleJson: ctx.config?.nodes?.length ? JSON.stringify(ctx.config) : null,
          LayoutJson: JSON.stringify(ctx.layout),
          NcDescriptionTemplate: r.NcDescriptionTemplate,
          IsValid: r.IsValid === 0 ? 0 : 1,
          Remark: r.Remark
        }
      }
    }"
    :execute-config="{ enabled: true, runApi: '/api/Admin/Workflow/test/run' }"
    @save-success="onSaveSuccess"
    @execute-success="onExecuteSuccess"
  />
</template>

<script setup lang="ts">
import WorkflowDesigner from '@share/components/workflow/WorkflowDesigner.vue'
import { ElMessage } from 'element-plus'

function onSaveSuccess(result: any) {
  if (result?.success === false) ElMessage.error(result?.message || '保存失败')
}
function onExecuteSuccess(result: any) { void result }
</script>

<style scoped lang="less">
.studio-layout { display: flex; flex-direction: column; height: 100%; }
</style>
