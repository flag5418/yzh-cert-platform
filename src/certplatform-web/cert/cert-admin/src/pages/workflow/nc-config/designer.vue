<!--
  NC 规则配置页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner

  ⚠️ 命名铁律（项目全局规则 §16.9 / YZH 架构铁律）
    数据库列名 = C# 属性名 = TS 字段名 = PascalCase，三处逐字一致，禁止局部改写。
    - 规则实体 ValidationRule → PascalCase（RuleName / RuleCode / RuleJson / LayoutJson / IsActive）
    - 组织树 organization-tree 是服务层手写 DTO（camelCase：id/label/cbCode/stdCode/phaseCode），
      属于已登记的例外，不参与本铁律。
    - RuleJson / LayoutJson 内部的 workflow 配置 schema（nodeId/nodeType/inputPorts…）
      是落库 payload 格式，沿用历史 camelCase，改动会破坏既有数据。
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
          filter.OrgCode ? { Field: 'OrgCode', Value: filter.OrgCode, Operator: 'Equal' } : null,
          filter.StandardCode ? { Field: 'StandardCode', Value: filter.StandardCode, Operator: 'Equal' } : null,
          filter.PhaseCode ? { Field: 'PhaseCode', Value: filter.PhaseCode, Operator: 'Equal' } : null
        ].filter(Boolean)
      }),
      detailApi: '/api/ValidationRule',
      textField: 'RuleName',
      codeField: 'RuleCode'
    }"
    :save-config="{
      api: '/api/ValidationRule/update',
      getUrl: (ctx) => (ctx.leaf.Code ? '/api/ValidationRule/update' : '/api/ValidationRule/add'),
      buildPayload: (ctx) => {
        const r = ctx.leaf
        return {
          Code: r.Code,
          OrgCode: r.OrgCode || ctx.filter.OrgCode,
          StandardCode: r.StandardCode || ctx.filter.StandardCode,
          PhaseCode: r.PhaseCode || ctx.filter.PhaseCode,
          ClauseCode: r.ClauseCode,
          WorkflowCode: r.WorkflowCode,
          RuleCode: r.RuleCode,
          RuleName: r.RuleName,
          RuleNameEn: r.RuleNameEn,
          SeverityIfViolated: r.SeverityIfViolated || 'minor',
          RuleJson: JSON.stringify(ctx.config),
          LayoutJson: JSON.stringify(ctx.layout),
          NcDescriptionTemplate: r.NcDescriptionTemplate,
          IsActive: r.IsActive !== false,
          Remark: r.Remark
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
