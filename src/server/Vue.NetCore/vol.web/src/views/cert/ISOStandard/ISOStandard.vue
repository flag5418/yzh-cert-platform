<!--
 *Author：CertPlatform
 *Date：2026-07-31
 *Contact：cert@platform.com
 *基于 YZHBaseCrud 的 ISO 标准管理页面
 -->
<template>
  <YZHBaseCrud
    :options="viewOptions"
    module-name="ISOStandard"
    description="ISO 标准管理：管理各认证机构可开展认证的ISO标准（如 ISO 9001、ISO 13485 等）"
    @on-init="handleInit"
    @add-after="handleAddAfter"
    @update-after="handleUpdateAfter"
  />
</template>

<script setup lang="jsx">
import YZHBaseCrud from "@/components/yzh/YZHBaseCrud.vue";
import viewOptions from "./options.js";

const handleInit = ($vm => {
  // 可以在此自定义配置
  // $vm.setFixedSearchForm(true);
});

const handleAddAfter = (result, formData) => {
  console.log("ISO Standard 新增成功:", result);
  return true;
};

const handleUpdateAfter = (result, formData) => {
  console.log("ISO Standard 更新成功:", result);
  return true;
};
</script>
