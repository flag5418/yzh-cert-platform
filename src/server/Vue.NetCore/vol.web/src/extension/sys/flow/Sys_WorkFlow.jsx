
let extension = {
  components: {
    //查询界面扩展组件
    gridHeader:'',
    gridBody: '',
    gridFooter: '',
    //新建、编辑弹出框扩展组件
    modelHeader: '',
    modelBody: '',
    modelFooter: ''
  },
  tableAction: '', //指定某张表的权限(这里填写表名,默认不用填写)
  methods: {
    //下面这些方法可以保留也可以删除
    onInit() {
    },
    onInited() {
    }
  }
};
export default extension;
