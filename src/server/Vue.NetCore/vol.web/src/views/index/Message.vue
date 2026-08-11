<template>
  <div class="notification" @click="showMsg">
    <el-badge :is-dot="msgCount > 0" :max="99" :show-zero="false" class="item" :offset="[3, -3]">
      <el-icon size="15">
        <Bell />
      </el-icon>
    </el-badge>
  </div>
  <vol-box v-model="model" :width="420" :padding="5">
    <div class="msg-header">
      <el-tabs v-model="activeName" class="msg-tabs" @tab-change="loadMessages">
        <el-tab-pane name="unread">
          <template #label>
            <span class="custom-tabs-label">
              <el-badge :value="msgCount" :show-zero="false" :offset="[-2, 4]"
                badge-style="background-color: #ff1b0b;width: 18px;">
                未读消息
              </el-badge>
            </span>
          </template>
        </el-tab-pane>
        <el-tab-pane label="已读消息" name="read" />
        <el-tab-pane label="全部消息" name="all" />
      </el-tabs>
      <el-button v-if="msgCount > 0" type="primary" link size="small" @click="markAllRead" style="position:absolute;right:10px;top:8px;z-index:10;">
        全部已读
      </el-button>
    </div>
    <el-scrollbar :height="400">
      <div class="msg-item" v-for="(item, index) in msgList" :key="item.id || index"
        :class="{ 'msg-unread': item.isRead === 0 }"
        @click="markRead(item)">
        <div class="title">
          <el-badge v-if="item.isRead === 0" is-dot class="unread-dot" />
          {{ item.title }}
        </div>
        <div class="desc">{{ item.content }}</div>
        <div class="bottom">
          <div class="tag">
            <el-tag :type="getTagType(item.messageType)" size="small">{{ getTypeLabel(item.messageType) }}</el-tag>
          </div>
          <div class="date">{{ formatDate(item.createDate) }}</div>
        </div>
      </div>
      <vol-empty v-if="msgList.length === 0" />
    </el-scrollbar>
  </vol-box>
</template>

<script setup>
import VolEmpty from "@/components/basic/VolEmpty.vue";
import { ref, getCurrentInstance, onMounted } from "vue";
import { ElNotification } from "element-plus";

const { proxy } = getCurrentInstance();
const model = ref(false);
const activeName = ref("unread");
const msgCount = ref(0);
const msgList = ref([]);

const showMsg = () => {
  model.value = true;
  loadMessages();
};

const loadMessages = async () => {
  try {
    const unreadOnly = activeName.value === "unread";
    const res = await proxy.http.post("api/message/list", {
      page: 1,
      pageSize: 50,
      unreadOnly: unreadOnly ? 1 : 0,
    }, true);
    if (res.status) {
      msgList.value = res.data || [];
    }
    // 同步未读数
    const countRes = await proxy.http.post("api/message/unread-count", {}, true);
    if (countRes.status) {
      msgCount.value = countRes.data || 0;
    }
  } catch (e) {
    console.error("加载消息失败", e);
  }
};

const markRead = async (item) => {
  if (item.isRead === 0) {
    try {
      await proxy.http.post(`api/message/read/${item.id}`, {}, true);
      item.isRead = 1;
      msgCount.value = Math.max(0, msgCount.value - 1);
    } catch (e) {
      console.error("标记已读失败", e);
    }
  }
};

const markAllRead = async () => {
  try {
    await proxy.http.post("api/message/read-all", {}, true);
    msgList.value.forEach(m => { m.isRead = 1; });
    msgCount.value = 0;
    proxy.$message.success("全部已读");
  } catch (e) {
    console.error("全部已读失败", e);
  }
};

const getTagType = (type) => {
  const map = { convert: "success", system: "info", task: "warning" };
  return map[type] || "info";
};

const getTypeLabel = (type) => {
  const map = { convert: "文件转换", system: "系统消息", task: "任务通知" };
  return map[type] || "消息";
};

const formatDate = (dateStr) => {
  if (!dateStr) return "";
  const d = new Date(dateStr);
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
};

// 暴露给 MessageConfig.js 调用
const onSignalRMessage = (data) => {
  if (data.value === "convert_progress" || data.value === "convert_cancelled") {
    msgCount.value++;
    // 如果弹窗打开，刷新列表
    if (model.value) loadMessages();
    // 显示桌面通知
    ElNotification({
      title: data.title || "文件转换",
      message: data.message || "",
      type: data.value === "convert_cancelled" ? "warning" : "success",
    });
  } else {
    msgCount.value++;
    if (model.value) loadMessages();
    ElNotification.success({
      title: data.title || "新消息",
      message: data.message || "",
    });
  }
};

defineExpose({ onSignalRMessage });

onMounted(() => {
  loadMessages();
});
</script>

<style scoped lang="less">
.notification {
  outline: none;
  color: #000;
}

.msg-header {
  position: relative;
}

.msg-item {
  border-bottom: 1px solid #eee;
  padding: 10px;
  cursor: pointer;

  .title {
    font-weight: bolder;
    font-size: 13px;
    color: #000;
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .desc {
    margin-top: 5px;
    line-height: 1.3;
    font-size: 12px;
    color: #676565;
  }

  .bottom {
    display: flex;
    margin-top: 5px;
    font-size: 12px;
    color: #676565;
  }

  .tag {
    flex: 1;
  }
}

.msg-item:hover {
  background: #f9f9f9;
}

.msg-unread {
  background: #f0f7ff;
}

.unread-dot {
  margin-right: 2px;
}

::v-deep(.el-tabs__header) {
  margin: 0;
}

::v-deep(.el-tabs__content) {
  min-height: 200px;
}

::v-deep(.el-tabs__nav) {
  width: 100%;
  padding: 0 10px;
}

::v-deep(.el-tabs__item) {
  padding: 0 6px;
  flex: 1;
}
</style>
