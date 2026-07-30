<template>
  <div
    class="layout-container"
    v-if="isCreated"
    :class="{ 'layout-container-padding': $global.gridPadding || padding }"
  >
    <slot name="gridHeader"></slot>
    <!--头部自定义组件-->
    <component
      :is="dynamicComponent.gridHeader"
      ref="gridHeader"
      @parentCall="parentCall"
    ></component>
    <div class="view-container">
      <div class="grid-search">
        <div
          ref="fixedSearchBox"
          :class="[fixedSearchForm ? 'fiexd-search-box' : 'search-box']"
          v-show="searchBoxShow"
        >
          <vol-form
            v-if="searchFormOptions.length"
            ref="searchForm"
            :load-key="false"
            :label-width="labelWidth"
            :eventNext="false"
            :formRules="searchFormOptions"
            :formFields="searchFormFields"
            :label-position="labelPosition"
            :select2Count="select2Count"
          >
            <template #footer>
              <div v-if="!fixedSearchForm" class="form-closex">
                <el-button
                  size="small"
                  type="primary"
                  plain
                  @click="advancedSearch"
                >
                  <i class="el-icon-search" />{{ $ts("查询") }}
                </el-button>

                <el-button
                  size="small"
                  type="success"
                  plain
                  @click="onResetSearch"
                >
                  <i class="el-icon-refresh-right" />{{ $ts("重置") }}
                </el-button>
                <el-button
                  size="small"
                  plain
                  @click="searchBoxShow = !searchBoxShow"
                >
                  <i class="el-icon-switch-button" />{{ $ts("关闭") }}
                </el-button>
              </div>
            </template>
          </vol-form>
          <div v-if="fixedSearchForm" class="fs-line"></div>
        </div>
        <div class="view-header">
          <div class="desc-text" v-if="table.cnName">
            <i class="el-icon-s-grid" />
            <span>{{ $ts(table.cnName) }}</span>
          </div>
          <view-grid-expand
            :render="gridRender.h"
            :item="gridRender.data"
          ></view-grid-expand>
          <div class="btn-left-slot">
            <slot name="btnLeft"></slot>
          </div>
          <div class="notice">
            <div v-if="text" v-html="text"></div>
            <a class="text" :title="extend.text">{{ extend.text }}</a>
          </div>
          <!--快速查询字段-->
          <div class="search-line" v-if="!fixedSearchForm && !searchBoxShow">
            <QuickSearch
              ref="quickSearch"
              v-if="singleSearch"
              :searchFormOptions="searchFormOptions"
              :searchFormFields="searchFormFields"
              :select2Count="select2Count"
              :label-width="labelWidth"
              :queryFields="queryFields"
              :tiggerPress="search"
            ></QuickSearch>
          </div>
          <slot name="btnRight"></slot>
          <!--操作按钮组-->
          <div class="btn-group">
            <template :key="bIndex" v-for="(btn, bIndex) in gridButtons">
              <template v-if="btn.data">
                <el-dropdown size="small" :split-button="false">
                  <el-button
                    :color="btn.color"
                    :dark="false"
                    :type="btn.type"
                    :plain="btn.plain"
                  >
                    {{ $ts(btn.name) }}
                    <i class="el-icon-arrow-down el-icon--right"></i
                  ></el-button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item
                        v-for="(item, index) in btn.data"
                        :key="index"
                      >
                        <div @click="registerClick(item.onClick)">
                          <i :class="item.icon"></i>
                          {{ $ts(item.name) }}
                        </div>
                      </el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </template>
              <view-grid-expand
                :render="btn.render"
                :item="btn"
                v-else-if="btn.render"
              ></view-grid-expand>
              <el-button
                v-else
                :type="btn.type"
                size="small"
                :color="btn.color"
                :dark="false"
                :class="btn.class"
                :plain="btn.plain"
                v-show="!btn.hidden"
                :disabled="btn.readonly || btn.disabled"
                @click="registerClick(btn.onClick)"
              >
                <i :class="btn.icon"></i> {{ $ts(btn.name) }}
              </el-button>
            </template>

            <el-dropdown
              size="small"
              popper-class="vol-drop-button"
              @click="changeDropdown"
              v-if="moreButtons.length"
            >
              <el-button type="default" plain size="small" class="more-btn">
                {{ $ts("更多")
                }}<i class="el-icon-arrow-down el-icon--right"></i>
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <template v-for="(item, mindex) in moreButtons" :key="mindex">
                    <el-dropdown-item
                      @click="changeDropdown(item.name)"
                      :name="item.name"
                    >
                      <div v-show="!item.hidden">
                        <i :class="item.icon"></i>
                        {{ $ts(item.name) }}
                      </div>
                    </el-dropdown-item>
                  </template>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
            <el-button
              class="setting-btn"
              type="default"
              style="
                padding-left: 8px !important;
                padding-right: 8px !important;
              "
              size="small"
              :plain="true"
              color="#626aef"
              v-if="showCustom"
              @click="customColumClick"
            >
              <i class="el-icon-sort"></i>
            </el-button>
          </div>
        </div>
      </div>
      <!--body自定义组件-->
      <div class="grid-body">
          <div class="grid-bottom" v-if="gridBodyText">
            <el-alert :title="gridBodyText" class="alert-primary"
            :closable="false"></el-alert>
          </div>
        <slot name="gridBody"></slot>
        <component
          :is="dynamicComponent.gridBody"
          ref="gridBody"
          @parentCall="parentCall"
        ></component>
      </div>
      <!--table表格-->
      <div class="grid-container">
        <!-- 2021.05.02增加树形结构 rowKey -->
        <vol-table
          ref="table"
          :single="single"
          :rowKey="rowKey"
          :loadTreeChildren="loadTreeTableChildren"
          @loadBefore="loadTableBefore"
          @loadAfter="loadTableAfter"
          @rowChange="rowOnChange"
          @rowClick="rowOnClick"
          @rowDbClick="rowOnDbClick"
          @selectionChange="selectionOnChange"
          :tableData="[]"
          :linkView="linkData"
          :columns="columns"
          :pagination="pagination"
          :height="height"
          :max-height="tableMaxHeight"
          :pagination-hide="paginationHide"
          :url="url"
          :load-key="false"
          :defaultLoadPage="load"
          :index="true"
          :beginEdit="tableBeginEdit"
          :endEditBefore="tableEndEditBefore"
          :column-index="columnIndex"
          :text-inline="textInline"
          :ck="ck"
          :select2Count="select2Count"
          :selectable="onSelectable"
          :lazy="lazy"
          :defaultExpandAll="defaultExpandAll"
          :rowParentField="rowParentField"
          :expandRowKeys="expandRowKeys"
          :dragPosition="dragPosition"
          :spanMethod="onSpanMethod"
          :reserveSelection="reserveSelection"
          :sortable="sortable"
          @onSortEnd="onSortEnd"
          :extraHeight="extraHeight"
          @headerDragend="onHeaderDragend"
          :tableV2="tableV2"
          :row-height="rowHeight"
        ></vol-table>
      </div>
    </div>
    <slot name="gridFooter"></slot>
    <!--footer自定义组件-->
    <component
      :is="dynamicComponent.gridFooter"
      ref="gridFooter"
      @parentCall="parentCall"
    ></component>
    <!-- 列表显示明细表 -->
    <div v-if="showFooterDetail">
        <view-grid-detail-footer ref="grdiDetailFooterRef"  :asyncApi="!!asyncApi" :generic="generic" :height="height" :table="table" :detail="detail" :details="details"></view-grid-detail-footer>
    </div>
  </div>
  <!-- 编辑弹出框 -->
  <vol-box
    v-model="boxModel"
    :title="boxOptions.title"
    :width="boxOptions.width"
    :height="boxOptions.height"
    :modal="boxOptions.modal"
    :draggable="boxOptions.draggable"
    :padding="0"
    :on-model-close="onGridModelClose"
    @fullscreen="onFullscreen"
    :full="full"
  >
    <!--明细头部自定义组件-->
    <template #content>
      <div class="vol-edit-box">
        <div class="vol-edit-content">
          <slot name="modelHeader"></slot>
          <component
            :is="dynamicComponent.modelHeader"
            ref="modelHeader"
            @parentCall="parentCall"
          ></component>
          <div class="item form-item" style="padding-top: 10px">
            <vol-form
              ref="form"
              :key="`${currentAction}-${boxModel}`"
              :editor="editor"
              :load-key="false"
              :label-width="boxOptions.labelWidth"
              :eventNext="eventNext"
              :formRules="editFormOptions"
              :formFields="editFormFields"
              :select2Count="select2Count"
              :label-position="labelPosition"
              @tabClick="editFormTabClick"
            ></vol-form>
          </div>
          <!--明细body自定义组件-->
          <slot name="modelBody"></slot>
           <div class="grid-bottom" style="margin-left: 10px;margin-right: 10px;" v-if="modelBodyText">
            <el-alert :title="modelBodyText" class="alert-primary"
            :closable="false"></el-alert>
          </div>
          <component
            :is="dynamicComponent.modelBody"
            ref="modelBody"
            @parentCall="parentCall"
          ></component>
          <div
            v-show="hasDetail&&showDetail"
            v-if="detail.columns && detail.columns.length > 0"
            class="grid-detail table-item item"
          >
            <div class="toolbar">
              <div class="title form-text">
                <span>
                  <i class="el-icon-edit-outline"></i>
                  {{ $ts(detail.cnName) }}
                </span>
              </div>
              <div class="detail-content">
                <slot name="detailContent"></slot>
              </div>
              <!--明细表格按钮-->
              <div class="btns detail-btns" v-show="!isBoxAudit">
                <template
                  v-for="(btn, bIndex) in detailOptions.buttons"
                  :key="bIndex"
                >
                  <view-grid-expand
                    :render="btn.render"
                    :item="btn"
                    v-if="btn.render"
                  ></view-grid-expand>

                  <el-button
                    v-else
                    :plain="btn.plain"
                    v-show="!(typeof btn.hidden == 'boolean' && btn.hidden)"
                    @click="registerClick(btn.onClick)"
                    size="small"
                    ><span :style="{ color: btn.color }"
                      ><i :class="btn.icon"></i>{{ $ts(btn.name) }}</span
                    ></el-button
                  >
                </template>
              </div>
            </div>
            <vol-table
              ref="detail"
              @loadBefore="loadDetailTableBefore"
              @loadAfter="loadDetailTableAfter"
              @rowChange="detailRowOnChange"
              @rowClick="detailRowOnClick"
              :url="detailOptions.url"
              :load-key="true"
              :index="true"
              :tableData="detailOptions.data"
              :columns="detailOptions.columns"
              :pagination="detailOptions.pagination"
              :height="detailOptions.height"
              :single="detailOptions.single"
              :pagination-hide="detailOptions.paginationHide"
              :defaultLoadPage="detailOptions.load"
              :beginEdit="detailOptions.beginEdit"
              :endEditBefore="detailOptions.endEditBefore"
              :endEditAfter="detailOptions.endEditAfter"
              :column-index="detailOptions.columnIndex"
              :ck="detailOptions.ck"
              :text-inline="detailOptions.textInline"
              :select2Count="select2Count"
              :selectable="detailOnSelectable"
              :spanMethod="onDetailSpanMethod"
              :sortable="detailOptions.sortable"
              @onSortEnd="detailOnSortEnd"
              @headerDragend="onDetailHeaderDragend"
              :tableV2="detailOptions.tableV2"
              :row-height="detailOptions.rowHeight"
              event-next
            ></vol-table>
          </div>
          <!--明细footer自定义组件-->
          <component
            :is="dynamicComponent.modelFooter"
            ref="modelFooter"
            @parentCall="parentCall"
          ></component>
          <slot name="modelFooter"></slot>
        </div>
        <div class="vol-edit-box-right">
          <slot name="modelRight"></slot>
          <component
            :is="dynamicComponent.modelRight"
            ref="modelRight"
            @parentCall="parentCall"
          ></component>
        </div>
      </div>
    </template>
    <template #footer>
      <div style="display: flex; align-items: center; justify-content: right">
        <slot name="modelBtn"></slot>
        <div style="text-align: center" v-show="isBoxAudit">
          <el-button
            size="small"
            type="primary"
            plain
            @click="onGridModelClose(false)"
          >
            <i class="el-icon-close">{{ $ts("关闭") }}</i>
          </el-button>
          <el-button
            size="small"
            type="primary"
            v-show="auditParam.showViewButton"
            @click="auditParam.model = true"
          >
            <i class="el-icon-view">{{ $ts("审批") }}</i>
          </el-button>
        </div>

        <div v-show="!isBoxAudit">
          <el-button
            v-for="(btn, bIndex) in boxButtons"
            :key="bIndex"
            :type="btn.type"
            size="small"
            :plain="btn.plain"
            v-show="!(typeof btn.hidden == 'boolean' && btn.hidden)"
            :disabled="btn.hasOwnProperty('disabled') && !!btn.disabled"
            @click="registerClick(btn.onClick)"
          >
            <i :class="btn.icon"></i>{{ $ts(btn.name) }}
          </el-button>

          <el-button size="small" plain @click="onGridModelClose(false)">
            <i class="el-icon-close">{{ $ts("关闭") }}</i>
          </el-button>
        </div>
      </div>
    </template>
  </vol-box>

  <!--导入excel功能-->
  <vol-box
    v-if="upload.url"
    v-model="upload.excel"
    :width="600"
    :lazy="true"
    :title="(boxModel ? detailOptions.cnName : table.cnName) + '-导入'"
  >
    <UploadExcel
      ref="upload_excel"
      @importExcelAfter="importExcelAfter"
      :importExcelBefore="importExcelBefore"
      :url="upload.url"
      :template="upload.template"
      :desc="importDesc"
    >
      <slot name="importContent"></slot>
    </UploadExcel>
  </vol-box>
  <!--审批弹出框 -->
  <ViewGridAudit
    v-if="auditInited"
    @auditClick="saveAudit"
    @flowLoadAfter="flowLoadAfter"
    @signAfter="signAfter"
    :option="table"
    ref="auditRef"
  >
    <template #auditContent>
      <slot name="auditContent"></slot>
    </template>
    <template #auditButton>
      <slot name="auditButton"></slot>
    </template>
  </ViewGridAudit>

  <custom-column ref="customColumnRef"></custom-column>
</template>

<script lang="jsx">
// 第一次打开弹出框时,明细表数据没加载
import {
  ref,
  reactive,
  getCurrentInstance,
  onBeforeMount,
  onUnmounted,
  onMounted,
  onActivated,
  shallowRef,
  toRaw,
  defineAsyncComponent,
  computed,
  nextTick,
} from "vue";
import action from "./Action";
import { useRouter, useRoute } from "vue-router";
import viewGridProps from "./ViewGridProps.js";
import ViewGridExpand from "./ViewGridExpand.js";
import ViewGridDataConfig from "./ViewGridDataConfig.jsx";
import * as ViewGridProvider from "./ViewGridProvider.jsx";
import { initMethods } from "./ViewGridExposeMethods.jsx";
import {
  initButtonsAuthFields,
  getButtons
} from "./ViewGridInitButtonsAuthFields.jsx";
import { initReadonly } from "./ViewGridReadonly.jsx";
import * as ViewGridProviderDetail from "./ViewGridProviderDetail.jsx";
//审批初始化配置
import { ViewGridAuditConfig} from "./ViewGridAuditConfig.jsx";

import ViewGridFilter from "./ViewGridFilter.js";
import { initViewColumns } from "./ViewGridCustomColumn.js";
import { initDicData } from "./ViewGridDicData.js";
import ViewGridEvent from "./ViewGridEvent.js";

import Empty from "@/components/basic/Empty.vue";

export default {
  components: {
    ViewGridExpand,
    QuickSearch: defineAsyncComponent(() =>
      import("@/components/basic/QuickSearch.vue")
    ),
    Audit: defineAsyncComponent(() => import("@/components/basic/Audit.vue")),
    UploadExcel: defineAsyncComponent(() =>
      import("@/components/basic/UploadExcel.vue")
    ),
    "custom-column": defineAsyncComponent(() =>
      import("./ViewGridCustomColumn.vue")
    ),
    "vol-header": defineAsyncComponent(() => import("./../VolHeader.vue")),
    ViewGridAudit: defineAsyncComponent(() => import("./ViewGridAudit.vue")),
    "view-grid-detail-footer": defineAsyncComponent(() => import("./ViewGridDetailFooter.vue"))
  },
  props: { ...viewGridProps() },
  emit: ["parentCall"],
  setup(props, { attrs, emit, expose, slots }) {
    const route = useRoute();
    const { proxy, ctx } = getCurrentInstance();
    ctx.$route = route;
    const dynamicCom = {
      gridHeader: Empty,
      gridBody: Empty,
      gridFooter: Empty,
      modelHeader: Empty,
      modelBody: Empty,
      modelRight: Empty,
      modelFooter: Empty,
    };
    //合并扩展组件
    if (props.extend.components) {
      for (const key in props.extend.components) {
        if (props.extend.components[key]) {
          dynamicCom[key] = toRaw(props.extend.components[key]);
        }
      }
    }
    const dynamicComponent = shallowRef(dynamicCom);
    const isCreated = ref(false);
    const dataConfig = ViewGridDataConfig();
    const { maxBtnLength, pagination, newTabEdit, hiddenFields } = dataConfig;
    dataConfig.asyncApi.value=proxy.base.getAsyncApi(props.table.name)
    
    const {
      initBoxHeightWidth,
      initFlowQuery,
      getUrl,
      initExtraHeight,
      initOntinueAdd,
      initOptions
    } = ViewGridProvider;
    initOptions(proxy,props,dataConfig)
    
    const { initDetailOptions } = ViewGridProviderDetail;
    const exposeMethods = initMethods(proxy, props, dataConfig);
    if (props.table.fixedSearch) {
      exposeMethods.setFixedSearchForm(true);
    }
    const parentCall = (fun) => {
      if (typeof fun == "function") {
        fun(proxy);
      }
    };

    ctx.$error = (message) => {
      proxy.$message.error(message);
    };
    ctx.$success = (message) => {
      proxy.$message.success(message);
    };

    pagination.sortName = props.table.sortName || props.table.key;

    if (proxy.$global.pagination) {
      Object.assign(pagination, proxy.$global.pagination);
    }
    newTabEdit.value = props.table.newTabEdit;

    const onGridModelClose = (iconClick) => {

      let boxVal = proxy.onModelClose.call(proxy, iconClick);
      if (!boxVal) return;
      dataConfig.boxModel.value = !props.onModelClose(iconClick);
    };

    //初始化配置信息
    //初始化按钮
    initButtonsAuthFields(proxy, props, dataConfig, route, dataConfig.hiddenFields); //初始化弹出框与明细表格按钮
    //初始化默认只读配置信息
    const viewGridReadonlyMethods = initReadonly(proxy, props, dataConfig);

    //初始化字典配置
    const initDicKeys = (reset) => {
      initDicData(proxy, props, ctx, dataConfig, reset); //初始下框数据源
    };
    const gridEvent = ViewGridEvent(proxy, props, ctx, dataConfig);
    //const { loadTableBefore, loadTableAfter } = gridEvent;
    //查询url
    dataConfig.url.value = getUrl(
      action.PAGE,
      null,
      props.table,
      dataConfig.dyPage,
      props,
      dataConfig.asyncApi.value
    );

    const gridButtons = computed(() => {
      return dataConfig.buttons.value
        .filter((x) => {
          return !x.hidden;
        })
        .filter((x, i) => {
          return i < maxBtnLength.value;
        });
    });
    const moreButtons = computed(() => {
      return dataConfig.buttons.value
        .filter((x) => {
          return !x.hidden;
        })
        .filter((x, i) => {
          return i >= maxBtnLength.value;
        });
    });
    const initAdvancedSearch = () => {
      const buttons = dataConfig.buttons.value;
      const btnIndex = buttons.findIndex((x) => {
        return x.value == "Search" && !x.hidden;
      });
      if (btnIndex == -1) {
        return;
      }
      const advancedBtn = buttons[btnIndex + 1];
      if (
        !advancedBtn ||
        advancedBtn.value != "advanced" ||
        advancedBtn.hidden
      ) {
        return;
      }
      const searchBtn = buttons[btnIndex];
      if (!searchBtn.v == "3.5") {
        return;
      }
      advancedBtn.name = "";
      advancedBtn.type = searchBtn.type;
      advancedBtn.plain = searchBtn.plain;
      advancedBtn.type = searchBtn.type;
      advancedBtn.color = searchBtn.color;
      advancedBtn.class = "btn-advanced";
      advancedBtn.icon = "el-icon-arrow-down"; // 'el-icon-sort'//el-icon-arrow-down
      searchBtn.class += " search-qy-btn";
    };
    //连续添加
    const setContinueAdd = (isAdd) => {
      initOntinueAdd(proxy, props, dataConfig, isAdd);
    };
    //明细配置
    const detailMethods = initDetailOptions(proxy, props, dataConfig);
    const { initAuditColumn, signAfter } = ViewGridAuditConfig(
      proxy,
      props,
      ctx,
      dataConfig
    );
    onBeforeMount(async () => {
      //调用初始化信息
      await proxy.onInit.call(proxy);
      //console.log(dataConfig.showTableAudit.value )

      setContinueAdd();
      if (proxy.$grid) {
        Object.keys(proxy.$grid).forEach((key) => {
          const fn = proxy.$grid[key];
          typeof fn == "function" && fn.call(proxy);
        });
      }
      await props.onInit(proxy);
  
      //审批初始化配置
      initAuditColumn(false);
      getButtons(proxy, props, ctx, dataConfig);
      initViewColumns(proxy, props, dataConfig, false);
      //初始编辑框等数据
      initBoxHeightWidth(proxy, props, ctx, dataConfig);
      initDicKeys();

      if (dataConfig.showFooterDetail.value) {
        dataConfig.height.value=dataConfig.height.value/2;
      }

      await proxy.onInited.call(proxy);
      await props.onInited(proxy);
  
      initAdvancedSearch();

      isCreated.value = true;
      initExtraHeight(proxy, dataConfig, true);
    });

    const customColumClick = () => {
      proxy.$refs.customColumnRef.show(
        props.columns,
        dataConfig.orginColumnFields,
        props.table.name
      );
    };
    onMounted(() => {
      proxy.mounted.call(proxy);
    });
    onUnmounted(() => {
      proxy.destroyed.call(proxy);
    });
    onActivated(() => {
      proxy.onActivated && proxy.onActivated.call(proxy);
      initFlowQuery(proxy, props, dataConfig, route);
    });
    // textInline.value = true;
    return {
      isCreated,
      ...exposeMethods,
      initDicKeys,
      ...gridEvent,
      parentCall,
      onGridModelClose,
      ...ViewGridProvider,
      ...ViewGridProviderDetail,
      ...ViewGridFilter,
      ...dataConfig,
      dynamicComponent,
      gridButtons,
      moreButtons,
      ...detailMethods,
      signAfter,
      setContinueAdd,
      customColumClick,
      ...props.extend.methods,
      ...viewGridReadonlyMethods,
    };
  },
};
</script>
<style lang="less" scoped>
@import "./ViewGrid.less";
</style>
<style lang="less" scoped>
.btn-group ::v-deep(.el-dropdown .el-button:focus-visible) {
  outline: 0px !important;
  outline-offset: 1px;
}

.vertical-center-modal ::v-deep(.srcoll-content) {
  padding: 0;
}

.view-model-content {
  background: #eee;
}
</style>
<style lang="less" scoped>
.form-item ::v-deep(.form-tabs) {
  margin-top: -10px;
}

.search-line ::v-deep(.vol-form-item) {
  margin-top: 4px !important;
}
</style>
