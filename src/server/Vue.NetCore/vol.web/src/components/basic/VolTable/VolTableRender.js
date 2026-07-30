import { h } from 'vue';
export default {
  name: "TableExpand",
  functional: true,
  props: {
    render: Function,
    row: {},//当前行的数据
    column: {},//当前行的配置信息
    index: { type: Number, default: 0 },//当前所在行
    editInfo:{ columnIndex: -1, rowIndex: -1 } //当前正在编辑的行
  },
  render: ({ render,row ,column,index,editInfo }) => {
    return render(h, {row ,column,index,editInfo}); //h();
  }
};
