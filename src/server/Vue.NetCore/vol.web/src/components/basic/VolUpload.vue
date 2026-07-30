<template>
  <div class="upload-container">
    <div>
      <slot name="header"></slot>
      <div class="input-btns" style="display: flex">
        <input ref="input" type="file" style="display: none" @change="handleChange" :multiple="multiple"
          :accept="accept" />
        <div v-if="img" class="upload-img">
          <div v-for="(file, index) in files" :key="index"
            :style="{ height: imgInfo.height + 'px', width: imgInfo.width + 'px' }" class="img-item">
            <div class="operation">
              <div class="action">
                <i class="el-icon-view view" @click="previewImg(index)"></i>
                <i class="el-icon-delete remove" @click="removeFile(index)"></i>
              </div>
              <div class="mask"></div>
            </div>
            <img :src="getImgSrc(file, index) + access_token" @error="handleImageError" />
          </div>
          <div :style="{ height: imgInfo.height + 'px', width: imgInfo.width + 'px' }"
            v-show="!autoUpload || (autoUpload && files.length < maxFile)" class="img-selector" :class="getSelector()">
            <div class="selector" @click="handleClick">
              <i :style="{ 'font-size': imgInfo.iconSize + 'px' }" :class="imgInfo.icon"></i>
            </div>
            <div v-if="!autoUpload" class="s-btn" :class="{ readonly: changed }" @click="upload">
              <div>{{ loadText }}</div>
            </div>
          </div>
        </div>
        <el-button v-else @click="handleClick">{{
          $ts('选择' + (img ? '图片' : '文件'))
          }}</el-button>

        <el-button v-if="!autoUpload && !img" type="info" :disabled="changed" @click="upload(true)"
          :loading="loadingStatus">{{ $ts('上传文件') }}</el-button>
      </div>
      <slot></slot>
      <div v-if="desc">
        <el-alert :title="getText() + $ts('文件大小不超过') + (maxSize || 50) + 'M'" type="info" show-icon>
        </el-alert>
      </div>
      <slot name="content"></slot>
      <div v-if="!img">
        <ul class="upload-list" v-show="fileList">
          <li class="list-file" v-for="(file, index) in files" :key="index">
            <a>
              <span @click="fileOnClick(index, file)">
                <i :class="format(file)"></i>
                {{ file.name }}
              </span>
            </a>
            <span @click="removeFile(index)" class="file-remove">
              <i class="el-icon-close"></i>
            </span>
          </li>
        </ul>
      </div>
      <slot name="tip"></slot>
    </div>
    <VolImageViewer ref="viewer"></VolImageViewer>
  </div>
</template>
<script setup lang="jsx">
// import OSS from 'ali-oss'
import {
  ref,
  reactive,
  watch,
  onMounted,
  getCurrentInstance,
  defineAsyncComponent,
  computed,
} from "vue";

// 旧逻辑里用到 window.oss，这里仅保留占位
const OSS = {};

const VolImageViewer = defineAsyncComponent(() =>
  import("@/components/basic/VolImageViewer.vue")
);

const props = defineProps({
  desc: {
    // 是否显示默认介绍
    type: Boolean,
    default: false,
  },
  fileInfo: {
    // 用于接收上传的文件，也可以加以默认值，显示已上传的文件，用户上传后会覆盖默认值
    type: Array,
    default: () => [],
    // 格式[{name:'1.jpg',path:'127.0.01/1.jpg'}]
  },
  downLoad: {
    // 是否可以点击文件下载
    type: Boolean,
    default: true,
  },
  multiple: {
    // 是否多选
    type: Boolean,
    default: false,
  },
  maxFile: {
    // 最多可选文件数量，必须multiple=true，才会生效
    type: Number,
    default: 5,
  },
  maxSize: {
    // 文件限制大小(M)
    type: Number,
    default: 50,
  },
  autoUpload: {
    // 选择文件后是否自动上传
    type: Boolean,
    default: true,
  },
  img: {
    // 图片类型  img>excel>fileTypes三种文件类型优先级
    type: Boolean,
    default: false,
  },
  excel: {
    // excel文件
    type: Boolean,
    default: false,
  },
  fileTypes: {
    // 指定上传文件的类型
    type: Array,
    default: () => [],
  },
  url: {
    // 上传的url
    type: String,
    default: "",
  },
  uploadBefore: {
    // 返回false会中止执行
    type: Function,
    default: () => true,
  },
  uploadAfter: {
    // 返回false会中止执行
    type: Function,
    default: () => true,
  },
  onChange: {
    // 选择文件时 //返回false会中止执行
    type: Function,
    default: () => true,
  },
  fileList: {
    // 是否显示选择的文件列表
    type: Boolean,
    default: true,
  },
  fileClick: {
    // 点击文件事件
    type: Function,
    default: () => true,
  },
  removeBefore: {
    // 移除文件事件
    type: Function,
    default: () => true,
  },
  append: {
    // 此属性已废弃，多文件上传，默认追加文件
    type: Boolean,
    default: false,
  },
  compress: {
    // 开启图片压缩
    type: Boolean,
    default: true,
  },
  compressMinSize: {
    // 压缩的最小比例
    type: Number,
    default: 0.7,
  },
  imgOption: {
    // 图片上传信息
    type: Object,
    default: () => ({}),
  },
});

const { proxy } = getCurrentInstance();

const input = ref();
const viewer = ref();

const defaultImg = new URL("@/assets/imgs/error-img.png", import.meta.url).href;

// 手动上传成功后禁止重复上传，必须重新选择
const changed = ref(false);
const files = ref([]);
const imgTypes = ["gif", "jpg", "jpeg", "png", "bmp", "webp", "jfif"];
const loadingStatus = ref(false);
const loadText = ref("上传文件");
const access_token = ref("");

const imgInfo = reactive({
  icon: "el-icon-camera-solid",
  iconSize: 35,
  height: 65,
  width: 65,
});

const accept = computed(() => {
  // 仅做选择时的过滤，最终仍以 checkFile 校验为准
  if (props.img) {
    return "image/*";
  }
  if (props.excel) {
    // 兼容旧逻辑支持的扩展名：numbers/csv/xls/xlsx
    return ".xls,.xlsx,.csv,.numbers,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv";
  }
  return undefined;
});

const cloneFile = (list) => {
  const arr = Array.isArray(list) ? list : [];
  files.value = arr.map((x) => {
    return {
      name: x.name || getFileName(x.path),
      path: x.path,
      size: x.size,
    };
  });
};

const getFileName = (path) => {
  if (!path) return "未定义文件名";
  return (path + "").split(/[\\/]/).pop();
};

onMounted(() => {
  Object.assign(imgInfo, props.imgOption || {});
  const tk = (proxy.$store.getters.getUserInfo() || { accessToken: "" })
    .accessToken;
  if (tk) {
    access_token.value = "?access_token=" + tk;
  }
  // 默认有文件的禁止上传操作
  if (props.fileInfo) {
    changed.value = true;
  }
  cloneFile(props.fileInfo);
});

watch(
  () => props.fileInfo,
  (list) => {
    cloneFile(list);
  },
  { deep: true }
);

const previewImg = (index) => {
  const imgs = files.value.map((x) => {
    return getImgSrc(x) + access_token.value;
  });
  viewer.value?.show?.(imgs, index);
};

const getSelector = () => {
  return props.autoUpload ? "auto-selector" : "submit-selector";
};

const getImgSrc = (file) => {
  if (file && Object.prototype.hasOwnProperty.call(file, "path")) {
    if (proxy.base.isUrl(file.path)) {
      return file.path;
    }
    //base64图片操作
    if ((file.path || "").indexOf("/9j/") !== -1) {
      return "data:image/jpeg;base64," + file.path;
    }
    let p = file.path;
    if (p && p.substr(0, 1) === "/") {
      p = p.substr(1);
      file.path = p;
    }
    return (proxy.$global.oss?.url || proxy.http.ipAddress) + p;
  }
  return window.URL.createObjectURL(file);
};

const fileOnClick = (index, file) => {
  if (props.fileClick(index, file, files.value) === false) return;
  if (!props.downLoad) return;
  if (!file.path) {
    proxy.$message.error("请先上传文件");
    return;
  }
  proxy.base.dowloadFile(
    file.path + access_token.value,
    file.name,
    {
      Authorization: proxy.$store.getters.getToken(),
    },
    proxy.http.ipAddress
  );
};

const getText = () => {
  if (props.img) return proxy.$ts("只能上传图片") + ',';
  if (props.excel) return proxy.$ts("只能上传excel文件") + ',';
  return "";
};

const handleClick = () => {
  input.value?.click?.();
};

const handleChange = (e) => {
  const inputFiles = e?.target?.files;
  const result = checkFile(inputFiles);
  if (!result) return;

  changed.value = false;
  // 如果传入了FileInfo需要自行处理移除FileInfo
  if (!props.onChange(inputFiles)) {
    return;
  }
  for (let index = 0; index < inputFiles.length; index++) {
    const element = inputFiles[index];
    element.input = true;
  }
  if (!props.multiple) {
    files.value.splice(0);
  }
  files.value.push(...inputFiles);

  if (input.value) {
    input.value.value = null;
  }
  if (props.autoUpload && result) {
    upload(false);
  }
};

const removeFile = (index) => {
  const remove = files.value[index];
  if (props.removeBefore(index, remove, props.fileInfo) === false) {
    return;
  }
  // 删除的还没上传的文件
  if (remove && remove.input) {
    files.value.splice(index, 1);
  } else {
    // 兼容旧版：直接修改传入的数组，回写到表单字段
    props.fileInfo.splice(index, 1);
  }
};

const clearFiles = () => {
  files.value.splice(0);
};

const getFiles = () => {
  return files.value;
};

const compressImg = async () => {
  if (!props.compress || !props.img) return;
  for (let index = 0; index < files.value.length; index++) {
    const originalFile = files.value[index];
    if (originalFile?.size > 300 * 1024) {
      try {
        const newFile = await proxy.base.compressImage(originalFile, {
          initialQuality: props.compressMinSize,
        });
        newFile.input = originalFile.input;
        files.value.splice(index, 1, newFile);
      } catch (error) {
        console.log("图片压缩异常", error);
      }
    }
  }
};

const uploadOSS = async () => {
  // 保留扩展点
};

const upload = async (vail) => {
  if (vail && !checkFile()) return false;
  if (!props.url) {
    proxy.$message.error("没有配置好Url");
    return;
  }
  if (!files.value || files.value.length === 0) {
    proxy.$message.error("请选择文件");
    return;
  }

  // 开启压缩
  await compressImg();

  // 过滤文件符号
  await proxy.base.resetFileName(files.value, (file) => {
    if (
      file.name?.includes(" ") ||
      file.name?.includes(",") ||
      file.name?.includes("+")
    ) {
      return file.name
        .replaceAll(" ", "")
        .replaceAll("+", "")
        .replaceAll(",", "");
    }
    return false;
  });

  // 增加上传时自定义参数，后台使用获取Utilities.HttpContext.Current.Request.Query["字段"]
  const params = {};
  if ((await props.uploadBefore(files.value, params)) === false) {
    return;
  }
  let paramText = "";
  if (Object.keys(params).length) {
    paramText = "?1=1";
    for (const key in params) {
      let value = params[key];
      if (typeof value === "object") {
        value = JSON.stringify(value);
      }
      paramText += `&${key}=${value}`;
    }
  }

  loadingStatus.value = true;
  loadText.value = "上传中..";

  if (window.oss && window.oss.ali.use) {
    await uploadOSS();
    loadingStatus.value = false;
    loadText.value = "上传文件";
    if (props.uploadAfter({ status: true }, files.value) === false) {
      changed.value = false;
      return;
    } else {
      changed.value = true;
    }
    proxy.$message.success("上传成功");
    return;
  }

  const forms = new FormData();
  for (let index = 0; index < files.value.length; index++) {
    const f = files.value[index];
    if (f.input) {
      forms.append("fileInput", f, f.name);
    }
  }

  proxy.http
    .post(
      props.url + paramText,
      forms,
      props.autoUpload ? proxy.$ts("正在上传文件") : "",
      {
        headers: { "Content-Type": "multipart/form-data" },
      }
    )
    .then(
      (x) => {
        loadingStatus.value = false;
        loadText.value = "上传文件";
        if (!props.uploadAfter(x, files.value)) {
          changed.value = false;
          return;
        } else {
          changed.value = true;
        }

        changed.value = !!x.status;
        if (!x.status) {
          proxy.$message.error(x.message);
          return;
        }
        proxy.$message.success(x.message);

        // 单选清除以前的数据
        props.fileInfo.splice(0);

        let _files = [];
        if (Array.isArray(x.data)) {
          _files = files.value
            .filter((f) => {
              return f.path;
            })
            .map((f) => {
              return { name: f.name, path: f.path, size: f.size };
            });
          _files.push(
            ...x.data.map((item) => {
              return { name: getFileName(item), path: item, size: 0 };
            })
          );
        } else {
          _files = files.value.map((f) => {
            return {
              name: f.name,
              path: x.data.startsWith("http") ? x.data : f.path || x.data + f.name,
              size: f.size,
            };
          });
        }

        props.fileInfo.push(..._files);
        // 2021.09.25修复文件上传后不能同时下载的问题
        files.value = _files;
      },
      () => {
        loadText.value = "上传文件";
        loadingStatus.value = false;
      }
    );
};

const format = (file, checkFileType) => {
  const ext = file.name.split(".").pop().toLocaleLowerCase() || "";
  let fileIcon = "el-icon-document";
  if (props.fileTypes.length > 0 && checkFileType !== undefined) {
    return props.fileTypes.indexOf(ext) !== -1;
  }
  if (
    checkFileType &&
    !(checkFileType instanceof Array) &&
    checkFileType !== "img" &&
    checkFileType !== "excel"
  ) {
    return checkFileType.indexOf(ext) > -1;
  }
  if (checkFileType === "img" || imgTypes.indexOf(ext) > -1) {
    if (checkFileType === "img") {
      return imgTypes.indexOf(ext) > -1;
    }
    fileIcon = "el-icon-picture-outline";
  }
  if (
    checkFileType === "excel" ||
    ["numbers", "csv", "xls", "xlsx"].indexOf(ext) > -1
  ) {
    if (checkFileType === "excel") {
      return ["numbers", "csv", "xls", "xlsx"].indexOf(ext) > -1;
    }
  }
  return fileIcon;
};

const checkFile = (inputFiles) => {
  const list = files.value;
  const inputList = inputFiles || [];

  if (props.multiple && list.length + inputList.length > (props.maxFile || 5)) {
    if (props.img) {
      proxy.$message.error(proxy.$tst("最多只能选【{$ts}】张图片", props.maxFile || 5));
    } else {
      proxy.$message.error(proxy.$tst("最多只能选【{$ts}】个文件", props.maxFile || 5));
    }
    return false;
  }

  let checkList = inputFiles;
  if (!checkList) {
    checkList = list.filter((x) => x.input);
  }
  const names = [];
  for (let index = 0; index < checkList.length; index++) {
    const f = checkList[index];
    if (names.indexOf(f.name) !== -1) {
      f.name = "(" + index + ")" + f.name;
    }
    names.push(f.name);
    if (props.img && !format(f, "img")) {
      proxy.$message.error(proxy.$tst("选择的文件【{$ts}】只能是图片格式", f.name));
      return false;
    }
    if (props.excel && !format(f, "excel")) {
      proxy.$message.error(
        proxy.$tst("选择的文件【{$ts}】只能是[.xls,.xlsx]格式", f.name)
      );
      return false;
    }
    if (props.fileTypes && props.fileTypes.length > 0 && !format(f, props.fileTypes)) {
      proxy.$message.error(
        proxy.$tst(
          "选择的文件【{$ts}】只能是[{$ts}]格式",
          [f.name, props.fileTypes.join(",")]
        )
      );
      return false;
    }
    if (f.size > (props.maxSize || 50) * 1024 * 1024) {
      proxy.$message.error(
        proxy.$tst("选择的文件【{$ts}】】不能超过【{$ts}】M", [f.name, props.maxSize || 50])
      );
      return false;
    }
  }
  return true;
};

const handleImageError = ($e) => {
  $e.target.src = defaultImg;
};

defineExpose({
  clearFiles,
  getFiles,
  upload,
});
</script>
<style lang="less" scoped>
.upload-list {
  padding-left: 0;
  list-style: none;
  margin: 6px 0;

  .list-file {
    line-height: 20px;
    padding: 2px;
    color: #515a6e;
    border-radius: 4px;
    transition: background-color 0.2s ease-in-out;
    overflow: hidden;
    position: relative;

    font-size: 13px;

    .file-remove {
      display: none;
      right: 0;
      //  margin-left: 50px;
      color: #0e9286;
    }
  }

  .list-file:hover {
    cursor: pointer;

    .file-remove {
      display: initial;
    }

    color: #2d8cf0;
  }
}

.upload-container {
  display: inline-block;
  width: 100%;
  // padding: 10px;

  // min-height: 250px;
  border-radius: 5px;

  .alert {
    margin-top: 43px;
  }

  .button-group>* {
    float: left;
    margin-right: 10px;
  }

  .file-info>span {
    margin-right: 20px;
  }
}

.upload-img {
  display: inline-block;

  .img-item:hover .operation {
    display: block;
  }

  .img-selector {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .img-item,
  .img-selector {
    position: relative;
    cursor: pointer;
    margin: 0 10px 10px 0;
    float: left;
    //width: 65px;
    //height: 65px;
    border: 1px solid #c7c7c7;
    overflow: hidden;
    border-radius: 3px;
    box-sizing: content-box;

    img {
      margin: 0;
      padding: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .operation {
      display: none;
      position: absolute;
      top: 0;
      bottom: 0;
      left: 0;
      right: 0;

      .action {
        opacity: 0.6;
        text-align: center;
        background: #151515de;
        font-size: 14px;
        position: absolute;
        z-index: 90;
        width: 100%;
        bottom: 3px;
        bottom: 0;
        color: #ded5d5;
        padding-right: 7px;
        padding-bottom: 3px;
        line-height: 20px;

        .el-icon-view {
          margin: 0 10px;
        }
      }

      .mask {
        opacity: 0.6;
        background: #9e9e9e;
        top: 0;
        width: 100%;
        height: 100%;
        position: absolute;
      }
    }
  }

  .img-selector {
    font-size: 50px;
    text-align: center;

    i {
      position: relative;
      // font-size: 40px;
      color: #6f6f6f;
    }
  }

  // .auto-selector {
  //   .selector {
  //     line-height: 64px;
  //   }
  // }

  .selector {
    color: #a0a0a0;
  }

  .submit-selector {
    display: flex;
    flex-direction: column;

    .s-btn {
      line-height: 22px;
      font-size: 12px;
      // top: -6px;
      // padding: 2px;
      position: relative;
      background: #2db7f5;
      color: white;
      text-align: center;
      width: 100%;
    }

    .selector {
      line-height: 50px;
    }

    .readonly {
      background: #8c8c8c;
    }
  }
}

.big-model {
  width: 100%;
  height: 100%;
  position: relative;

  .mask {
    position: absolute;
    opacity: 0.6;
    background: #eee;
    top: 0;
    width: 100%;
    height: 100%;
    position: absolute;
  }
}

.auto-upload {
  z-index: 9999999;
  width: 100%;
  height: 100%;
  position: fixed;
  top: 0;
  left: 0;

  .j-content {
    text-align: center;
    font-size: 17px;
    top: 40%;
    position: absolute;
    z-index: 999;
    left: 0;
    right: 0;
    width: 240px;
    /* height: 100%; */
    margin: auto;
    background: white;
    /* bottom: 30px; */
    line-height: 50px;
    border-radius: 6px;
    border: 1px solid #d2d2d2;
  }

  .mask {
    cursor: pointer;
    opacity: 0.6;
    width: 100%;
    height: 100%;
    background: #101010;
  }
}
</style>
