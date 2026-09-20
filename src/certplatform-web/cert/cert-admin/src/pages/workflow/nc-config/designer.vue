<!--
  NC 规则配置页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner
-->
<template>
  <WorkflowDesigner
    title="NC 规则配置"
    :workflow-type="'validation'"
    :tree-config="{
      loadApi: '/api/ValidationRule/filter',
      loadMethod: 'post',
      loadBodyBuilder: (filter) => ({
        Page: 1, PageSize: 200,
        Filters: [
          filter.orgCode ? { Field: 'OrgCode', Value: filter.orgCode, Operator: 'Equal' } : null,
          filter.standardCode ? { Field: 'StandardCode', Value: filter.standardCode, Operator: 'Equal' } : null,
          filter.phaseCode ? { Field: 'PhaseCode', Value: filter.phaseCode, Operator: 'Equal' } : null
        ].filter(Boolean)
      }),
      textField: 'ruleName',
      codeField: 'ruleCode'
    }"
    :save-config="{
      api: '/api/ValidationRule/update',
      getUrl: (ctx) => {
        const r = ctx.leaf
        return (r.Code || r.ruleCode) ? '/api/ValidationRule/update' : '/api/ValidationRule/add'
      },
      buildPayload: (ctx) => {
        const r = ctx.leaf
        return {
          Code: r.Code || r.code || r.ruleCode,
          OrgCode: r.OrgCode || r.orgCode || ctx.filter.orgCode,
          StandardCode: r.StandardCode || r.standardCode || ctx.filter.standardCode,
          PhaseCode: r.PhaseCode || r.phaseCode || ctx.filter.phaseCode,
          ClauseCode: r.ClauseCode || r.clauseCode,
          WorkflowCode: r.WorkflowCode || r.workflowCode,
          RuleCode: r.RuleCode || r.ruleCode,
          RuleName: r.RuleName || r.ruleName,
          RuleNameEn: r.RuleNameEn || r.ruleNameEn,
          SeverityIfViolated: r.SeverityIfViolated || r.severityIfViolated || 'minor',
          RuleJson: JSON.stringify(ctx.config),
          LayoutJson: JSON.stringify(ctx.layout),
          NcDescriptionTemplate: r.NcDescriptionTemplate || r.ncDescriptionTemplate,
          IsActive: r.isActive !== false,
          Remark: r.Remark || r.remark
        }
      }
    }"
    :execute-config="{ enabled: true, runApi: '/api/Workflow/test/run' }"
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
