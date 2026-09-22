var yt = Object.defineProperty;
var gt = (a, o, e) => o in a ? yt(a, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[o] = e;
var P = (a, o, e) => gt(a, typeof o != "symbol" ? o + "" : o, e);
import { defineComponent as se, ref as k, computed as H, reactive as be, watch as Se, resolveComponent as A, openBlock as p, createBlock as z, withCtx as C, createVNode as L, createElementBlock as F, Fragment as ae, renderList as ue, mergeProps as we, createTextVNode as R, toDisplayString as O, renderSlot as q, createCommentVNode as W, createElementVNode as B, unref as Ae, normalizeStyle as Fe, withKeys as vt, normalizeClass as ke, createSlots as bt, resolveDynamicComponent as Ee, withModifiers as Ct, getCurrentInstance as wt, onMounted as Be, resolveDirective as kt, withDirectives as _t, nextTick as Te } from "vue";
import { ElInput as We, ElTree as Tt, ElMessageBox as xe, ElMessage as Z } from "element-plus";
const xt = {
  key: 0,
  class: "yzh-form__actions"
}, St = /* @__PURE__ */ se({
  __name: "YzhForm",
  props: {
    modelValue: {},
    fields: {},
    rules: {},
    labelWidth: { default: "100px" },
    labelPosition: { default: "right" },
    size: { default: "default" },
    showActions: { type: Boolean, default: !0 },
    cols: { default: 2 },
    submitText: { default: "保存" },
    resetText: { default: "取消" },
    loading: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue", "submit", "reset", "validate"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(), r = H(() => 24 / t.cols), s = H(() => {
      if (t.rules) return t.rules;
      const u = {};
      return t.fields.forEach((x) => {
        if (x.hidden) return;
        const G = [];
        x.required && G.push({
          required: !0,
          message: `请${x.type === "select" || x.type === "radio" || x.type === "switch" ? "选择" : "输入"}${x.label}`,
          trigger: x.trigger || (x.type === "select" || x.type === "switch" ? "change" : "blur")
        }), x.validator && G.push({ validator: x.validator, trigger: x.trigger || "blur" }), G.length && (u[x.prop] = G);
      }), u;
    }), c = be({});
    async function f(u) {
      if (u.options) return u.options;
      if (!u.loadOptions) return [];
      if (c[u.prop]) return c[u.prop];
      const x = await u.loadOptions();
      return c[u.prop] = x, x;
    }
    (async () => {
      for (const u of t.fields)
        if (u.loadOptions && !u.options)
          try {
            await f(u);
          } catch {
          }
    })();
    const i = be({});
    function b() {
      Object.keys(i).forEach((u) => delete i[u]), Object.assign(i, t.modelValue || {}), t.fields.forEach((u) => {
        i[u.prop] === void 0 && u.defaultValue !== void 0 && (i[u.prop] = u.defaultValue);
      });
    }
    b(), Se(
      () => t.modelValue,
      () => b(),
      { deep: !0 }
    ), Se(
      i,
      (u) => {
        n("update:modelValue", { ...u });
      },
      { deep: !0 }
    );
    async function w() {
      if (l.value)
        try {
          await l.value.validate(), n("submit", { ...i }), n("validate", !0);
        } catch (u) {
          n("validate", !1, u);
        }
    }
    function T() {
      var u;
      b(), (u = l.value) == null || u.clearValidate(), n("reset");
    }
    async function g() {
      var u;
      return (u = l.value) == null ? void 0 : u.validate();
    }
    async function U() {
      var u;
      (u = l.value) == null || u.resetFields();
    }
    return o({ validate: g, resetFields: U, formRef: l }), (u, x) => {
      const G = A("el-input"), ee = A("el-input-number"), ne = A("el-option"), re = A("el-select"), le = A("el-radio"), S = A("el-radio-group"), E = A("el-checkbox"), Y = A("el-checkbox-group"), j = A("el-switch"), J = A("el-date-picker"), te = A("el-tree-select"), Q = A("el-cascader"), me = A("el-form-item"), fe = A("el-col"), pe = A("el-row"), Ce = A("el-button"), v = A("el-form");
      return p(), z(v, {
        ref_key: "formRef",
        ref: l,
        model: i,
        rules: s.value,
        "label-width": a.labelWidth,
        "label-position": a.labelPosition,
        size: a.size,
        class: "yzh-form"
      }, {
        default: C(() => [
          L(pe, { gutter: 20 }, {
            default: C(() => [
              (p(!0), F(ae, null, ue(a.fields, (d) => (p(), F(ae, {
                key: d.prop
              }, [
                d.hidden ? W("", !0) : (p(), z(fe, {
                  key: 0,
                  span: d.span || r.value
                }, {
                  default: C(() => [
                    L(me, {
                      label: d.label,
                      prop: d.prop
                    }, {
                      default: C(() => [
                        !d.type || d.type === "text" || d.type === "textarea" || d.type === "password" ? (p(), z(G, we({
                          key: 0,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          type: d.type === "textarea" ? "textarea" : d.type === "password" ? "password" : "text",
                          placeholder: d.placeholder || `请输入${d.label}`,
                          disabled: d.disabled,
                          rows: d.type === "textarea" ? 3 : void 0,
                          autocomplete: d.type === "password" ? "new-password" : "off"
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "type", "placeholder", "disabled", "rows", "autocomplete"])) : d.type === "number" ? (p(), z(ee, we({
                          key: 1,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          placeholder: d.placeholder,
                          disabled: d.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : d.type === "select" ? (p(), z(re, we({
                          key: 2,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          placeholder: d.placeholder || `请选择${d.label}`,
                          multiple: d.multiple,
                          filterable: d.filterable,
                          disabled: d.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), {
                          default: C(() => [
                            (p(!0), F(ae, null, ue(d.options || c[d.prop] || [], (h) => (p(), z(ne, {
                              key: h.value,
                              label: h.label,
                              value: h.value,
                              disabled: h.disabled
                            }, null, 8, ["label", "value", "disabled"]))), 128))
                          ]),
                          _: 2
                        }, 1040, ["modelValue", "onUpdate:modelValue", "placeholder", "multiple", "filterable", "disabled"])) : d.type === "radio" ? (p(), z(S, {
                          key: 3,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          disabled: d.disabled
                        }, {
                          default: C(() => [
                            (p(!0), F(ae, null, ue(d.options || [], (h) => (p(), z(le, {
                              key: h.value,
                              value: h.value
                            }, {
                              default: C(() => [
                                R(O(h.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : d.type === "checkbox" ? (p(), z(Y, {
                          key: 4,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          disabled: d.disabled
                        }, {
                          default: C(() => [
                            (p(!0), F(ae, null, ue(d.options || [], (h) => (p(), z(E, {
                              key: h.value,
                              value: h.value
                            }, {
                              default: C(() => [
                                R(O(h.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : d.type === "switch" ? (p(), z(j, we({
                          key: 5,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          disabled: d.disabled,
                          "active-value": 1,
                          "inactive-value": 0
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "disabled"])) : d.type === "date" ? (p(), z(J, we({
                          key: 6,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          type: "date",
                          placeholder: d.placeholder || `请选择${d.label}`,
                          disabled: d.disabled,
                          "value-format": "YYYY-MM-DD",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : d.type === "datetime" ? (p(), z(J, we({
                          key: 7,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          type: "datetime",
                          placeholder: d.placeholder || `请选择${d.label}`,
                          disabled: d.disabled,
                          "value-format": "YYYY-MM-DD HH:mm:ss",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : d.type === "dateRange" ? (p(), z(J, we({
                          key: 8,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          type: "daterange",
                          placeholder: d.placeholder || `请选择${d.label}`,
                          disabled: d.disabled,
                          "value-format": "YYYY-MM-DD",
                          "range-separator": "至",
                          "start-placeholder": "开始日期",
                          "end-placeholder": "结束日期",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : d.type === "treeSelect" ? (p(), z(te, we({
                          key: 9,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          data: d.options || [],
                          placeholder: d.placeholder || `请选择${d.label}`,
                          disabled: d.disabled,
                          "check-strictly": "",
                          clearable: "",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "data", "placeholder", "disabled"])) : d.type === "cascader" ? (p(), z(Q, we({
                          key: 10,
                          modelValue: i[d.prop],
                          "onUpdate:modelValue": (h) => i[d.prop] = h,
                          options: d.options || [],
                          placeholder: d.placeholder || `请选择${d.label}`,
                          disabled: d.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, d.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "options", "placeholder", "disabled"])) : d.type === "custom" && d.slot ? q(u.$slots, d.slot, {
                          value: i[d.prop],
                          field: d,
                          data: i
                        }, void 0, !0, 11) : q(u.$slots, `field-${d.prop}`, {
                          value: i[d.prop],
                          field: d,
                          data: i
                        }, void 0, !0, 12)
                      ]),
                      _: 2
                    }, 1032, ["label", "prop"])
                  ]),
                  _: 2
                }, 1032, ["span"]))
              ], 64))), 128))
            ]),
            _: 3
          }),
          a.showActions ? (p(), F("div", xt, [
            q(u.$slots, "actions", {
              submit: w,
              reset: T
            }, () => [
              L(Ce, { onClick: T }, {
                default: C(() => [
                  R(O(a.resetText), 1)
                ]),
                _: 1
              }),
              L(Ce, {
                type: "primary",
                loading: a.loading,
                onClick: w
              }, {
                default: C(() => [
                  R(O(a.submitText), 1)
                ]),
                _: 1
              }, 8, ["loading"])
            ], !0)
          ])) : W("", !0)
        ]),
        _: 3
      }, 8, ["model", "rules", "label-width", "label-position", "size"]);
    };
  }
}), he = (a, o) => {
  const e = a.__vccOpts || a;
  for (const [t, n] of o)
    e[t] = n;
  return e;
}, zt = /* @__PURE__ */ he(St, [["__scopeId", "data-v-0464ba24"]]), Ba = /* @__PURE__ */ se({
  __name: "YzhFormDialog",
  props: {
    visible: { type: Boolean, default: !1 },
    mode: { default: "add" },
    entityName: { default: "" },
    title: { default: void 0 },
    width: { default: "640px" },
    fields: { default: () => [] },
    modelValue: { default: () => ({}) },
    loading: { type: Boolean, default: !1 },
    cols: { default: 2 },
    labelWidth: { default: "100px" },
    submitText: { default: void 0 },
    destroyOnClose: { type: Boolean, default: !0 }
  },
  emits: ["update:visible", "update:modelValue", "submit", "cancel", "closed"],
  setup(a, { emit: o }) {
    const e = a, t = o, n = H({
      get: () => e.visible,
      set: (i) => t("update:visible", i)
    }), l = H({
      get: () => e.modelValue,
      set: (i) => t("update:modelValue", i)
    }), r = H(() => {
      if (e.title) return e.title;
      const i = e.entityName || "";
      return e.mode === "add" ? i ? `新增${i}` : "新增" : e.mode === "detail" ? i ? `${i}详情` : "详情" : i ? `编辑${i}` : "编辑";
    }), s = H(() => e.submitText ?? (e.mode === "detail" ? "关闭" : "保存"));
    function c() {
      t("submit");
    }
    function f() {
      t("cancel"), t("update:visible", !1);
    }
    return (i, b) => {
      const w = A("el-button"), T = A("el-dialog");
      return p(), z(T, {
        modelValue: n.value,
        "onUpdate:modelValue": b[1] || (b[1] = (g) => n.value = g),
        title: r.value,
        width: a.width,
        "close-on-click-modal": !1,
        "destroy-on-close": a.destroyOnClose,
        onClosed: b[2] || (b[2] = (g) => t("closed"))
      }, {
        footer: C(() => [
          q(i.$slots, "footer", {}, () => [
            L(w, { onClick: f }, {
              default: C(() => [...b[3] || (b[3] = [
                R("取消", -1)
              ])]),
              _: 1
            }),
            L(w, {
              type: "primary",
              loading: a.loading,
              onClick: c
            }, {
              default: C(() => [
                R(O(s.value), 1)
              ]),
              _: 1
            }, 8, ["loading"])
          ])
        ]),
        default: C(() => [
          q(i.$slots, "default", {}, () => [
            q(i.$slots, "prepend"),
            L(zt, {
              modelValue: l.value,
              "onUpdate:modelValue": b[0] || (b[0] = (g) => l.value = g),
              fields: a.fields,
              loading: a.loading,
              cols: a.cols,
              "label-width": a.labelWidth,
              onSubmit: c,
              onReset: f
            }, null, 8, ["modelValue", "fields", "loading", "cols", "label-width"])
          ])
        ]),
        _: 3
      }, 8, ["modelValue", "title", "width", "destroy-on-close"]);
    };
  }
}), At = { class: "yzh-search-bar" }, Ft = { class: "yzh-search-bar__inner" }, Nt = { class: "yzh-search-bar__fields" }, Dt = { class: "yzh-search-bar__field-row" }, $t = { class: "yzh-search-bar__label" }, Vt = { class: "yzh-search-bar__actions" }, Bt = /* @__PURE__ */ se({
  __name: "YzhSearchBar",
  props: {
    fields: {},
    defaultValues: {},
    cols: { default: 2 },
    maxFields: { default: 2 },
    inputWidth: { default: "200px" }
  },
  emits: ["search", "reset"],
  setup(a, { emit: o }) {
    const e = a, t = o, n = be({});
    Se(
      () => e.defaultValues,
      (c) => {
        c && (Object.keys(n).forEach((f) => delete n[f]), Object.assign(n, c));
      },
      { immediate: !0, deep: !0 }
    );
    const l = e.fields.slice(0, e.maxFields);
    function r() {
      const c = {};
      l.forEach((f) => {
        const i = n[f.prop];
        i !== void 0 && i !== "" && !(Array.isArray(i) && i.length === 0) && (c[f.prop] = i);
      }), t("search", c);
    }
    function s() {
      l.forEach((c) => {
        delete n[c.prop];
      }), t("reset");
    }
    return (c, f) => {
      const i = A("el-input"), b = A("el-input-number"), w = A("el-option"), T = A("el-select"), g = A("el-date-picker"), U = A("el-button");
      return p(), F("div", At, [
        B("div", Ft, [
          B("div", Nt, [
            (p(!0), F(ae, null, ue(Ae(l), (u) => (p(), F("div", {
              key: u.prop,
              class: "yzh-search-bar__field"
            }, [
              B("div", Dt, [
                B("label", $t, O(u.label), 1),
                B("div", {
                  class: "yzh-search-bar__input-wrap",
                  style: Fe({ width: a.inputWidth })
                }, [
                  !u.type || u.type === "text" ? (p(), z(i, {
                    key: 0,
                    modelValue: n[u.prop],
                    "onUpdate:modelValue": (x) => n[u.prop] = x,
                    placeholder: u.placeholder || `请输入${u.label}`,
                    clearable: "",
                    onKeyup: vt(r, ["enter"])
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : u.type === "number" ? (p(), z(b, {
                    key: 1,
                    modelValue: n[u.prop],
                    "onUpdate:modelValue": (x) => n[u.prop] = x,
                    placeholder: u.placeholder || `请输入${u.label}`
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : u.type === "select" ? (p(), z(T, {
                    key: 2,
                    modelValue: n[u.prop],
                    "onUpdate:modelValue": (x) => n[u.prop] = x,
                    placeholder: u.placeholder || `请选择${u.label}`,
                    clearable: "",
                    filterable: ""
                  }, {
                    default: C(() => [
                      (p(!0), F(ae, null, ue(u.options || [], (x) => (p(), z(w, {
                        key: x.value,
                        label: x.label,
                        value: x.value
                      }, null, 8, ["label", "value"]))), 128))
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "placeholder"])) : u.type === "date" ? (p(), z(g, {
                    key: 3,
                    modelValue: n[u.prop],
                    "onUpdate:modelValue": (x) => n[u.prop] = x,
                    type: "date",
                    placeholder: u.placeholder || `请选择${u.label}`,
                    "value-format": "YYYY-MM-DD"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : u.type === "dateRange" ? (p(), z(g, {
                    key: 4,
                    modelValue: n[u.prop],
                    "onUpdate:modelValue": (x) => n[u.prop] = x,
                    type: "daterange",
                    placeholder: u.placeholder || `请选择${u.label}`,
                    "value-format": "YYYY-MM-DD",
                    "range-separator": "至",
                    "start-placeholder": "开始日期",
                    "end-placeholder": "结束日期"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : W("", !0)
                ], 4)
              ])
            ]))), 128)),
            f[0] || (f[0] = B("div", { class: "yzh-search-bar__spacer" }, null, -1))
          ]),
          B("div", Vt, [
            L(U, {
              type: "primary",
              onClick: r
            }, {
              default: C(() => [...f[1] || (f[1] = [
                B("i", { class: "bi bi-search" }, null, -1),
                R(" 查询 ", -1)
              ])]),
              _: 1
            }),
            L(U, { onClick: s }, {
              default: C(() => [...f[2] || (f[2] = [
                B("i", { class: "bi bi-arrow-counterclockwise" }, null, -1),
                R(" 重置 ", -1)
              ])]),
              _: 1
            })
          ])
        ])
      ]);
    };
  }
}), Pt = /* @__PURE__ */ he(Bt, [["__scopeId", "data-v-d0360654"]]), Rt = { class: "yzh-toolbar" }, Lt = { class: "yzh-toolbar__left" }, Et = { class: "yzh-toolbar__right" }, Ut = /* @__PURE__ */ se({
  __name: "YzhToolbar",
  props: {
    buttons: { default: () => [] }
  },
  emits: ["action"],
  setup(a, { emit: o }) {
    const e = o;
    function t(n) {
      n.disabled || e("action", n.key, n);
    }
    return (n, l) => {
      const r = A("el-button");
      return p(), F("div", Rt, [
        B("div", Lt, [
          (p(!0), F(ae, null, ue(a.buttons.filter((s) => s.group !== "right"), (s) => (p(), z(r, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (c) => t(s)
          }, {
            default: C(() => [
              R(O(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          q(n.$slots, "left", {}, void 0, !0)
        ]),
        B("div", Et, [
          (p(!0), F(ae, null, ue(a.buttons.filter((s) => s.group === "right"), (s) => (p(), z(r, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (c) => t(s)
          }, {
            default: C(() => [
              R(O(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          q(n.$slots, "right", {}, void 0, !0)
        ])
      ]);
    };
  }
}), Mt = /* @__PURE__ */ he(Ut, [["__scopeId", "data-v-95dfd97a"]]), It = /* @__PURE__ */ se({
  __name: "YzhPagination",
  props: {
    page: {},
    pageSize: {},
    total: {},
    pageSizes: { default: () => [10, 20, 50, 100] },
    layout: { default: "total, sizes, prev, pager, next, jumper" },
    background: { type: Boolean, default: !0 },
    size: { default: "default" }
  },
  emits: ["update:page", "update:pageSize"],
  setup(a, { emit: o }) {
    const e = a, t = o, n = H({
      get: () => e.page,
      set: (r) => t("update:page", r)
    }), l = H({
      get: () => e.pageSize,
      set: (r) => t("update:pageSize", r)
    });
    return (r, s) => {
      const c = A("el-pagination");
      return p(), z(c, {
        "current-page": n.value,
        "onUpdate:currentPage": s[0] || (s[0] = (f) => n.value = f),
        "page-size": l.value,
        "onUpdate:pageSize": s[1] || (s[1] = (f) => l.value = f),
        total: a.total,
        "page-sizes": a.pageSizes,
        layout: a.layout,
        background: a.background,
        size: a.size
      }, null, 8, ["current-page", "page-size", "total", "page-sizes", "layout", "background", "size"]);
    };
  }
}), Ot = /* @__PURE__ */ he(It, [["__scopeId", "data-v-13879d57"]]), Kt = { class: "yzh-page-layout" }, Yt = {
  key: 0,
  class: "yzh-page-layout__search"
}, jt = {
  key: 1,
  class: "yzh-page-layout__toolbar"
}, Wt = { class: "yzh-page-layout__toolbar-left" }, qt = { class: "yzh-page-layout__toolbar-right" }, Gt = {
  key: 2,
  class: "yzh-page-layout__footer"
}, Ht = /* @__PURE__ */ se({
  __name: "YzhPageLayout",
  props: {
    pageTitle: {},
    helpText: {},
    showTitle: { type: Boolean },
    noPadding: { type: Boolean },
    hideToolbar: { type: Boolean }
  },
  setup(a) {
    return (o, e) => (p(), F("div", Kt, [
      o.$slots.search ? (p(), F("div", Yt, [
        q(o.$slots, "search", {}, void 0, !0)
      ])) : W("", !0),
      !a.hideToolbar && (o.$slots.toolbar || o.$slots["toolbar-left"] || o.$slots["toolbar-right"]) ? (p(), F("div", jt, [
        q(o.$slots, "toolbar", {}, () => [
          B("div", Wt, [
            q(o.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          B("div", qt, [
            q(o.$slots, "toolbar-right", {}, void 0, !0)
          ])
        ], !0)
      ])) : W("", !0),
      B("div", {
        class: ke(["yzh-page-layout__content", { "yzh-page-layout__content--no-padding": a.noPadding }])
      }, [
        q(o.$slots, "default", {}, void 0, !0)
      ], 2),
      o.$slots.pagination ? (p(), F("div", Gt, [
        q(o.$slots, "pagination", {}, void 0, !0)
      ])) : W("", !0)
    ]));
  }
}), Pa = /* @__PURE__ */ he(Ht, [["__scopeId", "data-v-756b5466"]]), Xt = { class: "yzh-dialog__body" }, Jt = { class: "yzh-dialog__footer" }, Zt = /* @__PURE__ */ se({
  __name: "YzhDialog",
  props: {
    modelValue: { type: Boolean },
    title: { default: "提示" },
    width: { default: "600px" },
    fullscreen: { type: Boolean, default: !1 },
    showFooter: { type: Boolean, default: !0 },
    confirmText: { default: "确定" },
    cancelText: { default: "取消" },
    confirmType: { default: "primary" },
    confirmDisabled: { type: Boolean, default: !1 },
    confirmLoading: { type: Boolean, default: !1 },
    closeOnClickModal: { type: Boolean, default: !1 },
    showClose: { type: Boolean, default: !0 },
    zIndex: {},
    customClass: { default: "" },
    destroyOnClose: { type: Boolean, default: !1 },
    top: { default: "15vh" }
  },
  emits: ["update:modelValue", "confirm", "cancel", "open", "close"],
  setup(a, { emit: o }) {
    const e = a, t = o, n = H(() => typeof e.width == "number" ? `${e.width}px` : e.width);
    function l() {
      t("update:modelValue", !1), t("close");
    }
    function r() {
      e.confirmDisabled || e.confirmLoading || t("confirm");
    }
    function s() {
      t("cancel"), l();
    }
    return Se(
      () => e.modelValue,
      (c) => {
        c && t("open");
      }
    ), (c, f) => {
      const i = A("el-button"), b = A("el-dialog");
      return p(), z(b, {
        "model-value": a.modelValue,
        title: a.title,
        width: a.fullscreen ? "100%" : n.value,
        fullscreen: a.fullscreen,
        "show-close": a.showClose,
        "close-on-click-modal": a.closeOnClickModal,
        "z-index": a.zIndex,
        class: ke(a.customClass),
        top: a.fullscreen ? "0" : a.top,
        "destroy-on-close": a.destroyOnClose,
        "onUpdate:modelValue": f[0] || (f[0] = (w) => t("update:modelValue", w))
      }, bt({
        default: C(() => [
          B("div", Xt, [
            q(c.$slots, "default", {}, void 0, !0)
          ])
        ]),
        _: 2
      }, [
        a.showFooter ? {
          name: "footer",
          fn: C(() => [
            q(c.$slots, "footer", {
              confirm: r,
              cancel: s
            }, () => [
              B("div", Jt, [
                L(i, { onClick: s }, {
                  default: C(() => [
                    R(O(a.cancelText), 1)
                  ]),
                  _: 1
                }),
                L(i, {
                  type: a.confirmType,
                  disabled: a.confirmDisabled,
                  loading: a.confirmLoading,
                  onClick: r
                }, {
                  default: C(() => [
                    R(O(a.confirmText), 1)
                  ]),
                  _: 1
                }, 8, ["type", "disabled", "loading"])
              ])
            ], !0)
          ]),
          key: "0"
        } : void 0
      ]), 1032, ["model-value", "title", "width", "fullscreen", "show-close", "close-on-click-modal", "z-index", "class", "top", "destroy-on-close"]);
    };
  }
}), Ra = /* @__PURE__ */ he(Zt, [["__scopeId", "data-v-dbdcca59"]]);
/*! Element Plus Icons Vue v2.3.2 */
var Qt = /* @__PURE__ */ se({
  name: "Document",
  __name: "document",
  setup(a) {
    return (o, e) => (p(), F("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      B("path", {
        fill: "currentColor",
        d: "M832 384H576V128H192v768h640zm-26.496-64L640 154.496V320zM160 64h480l256 256v608a32 32 0 0 1-32 32H160a32 32 0 0 1-32-32V96a32 32 0 0 1 32-32m160 448h384v64H320zm0-192h160v64H320zm0 384h384v64H320z"
      })
    ]));
  }
}), eo = Qt, to = /* @__PURE__ */ se({
  name: "Folder",
  __name: "folder",
  setup(a) {
    return (o, e) => (p(), F("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      B("path", {
        fill: "currentColor",
        d: "M128 192v640h768V320H485.76L357.504 192zm-32-64h287.872l128.384 128H928a32 32 0 0 1 32 32v576a32 32 0 0 1-32 32H96a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32"
      })
    ]));
  }
}), oo = to;
const ao = { class: "yzh-tree" }, no = {
  key: 0,
  class: "yzh-tree__search"
}, lo = ["onMouseenter"], so = {
  key: 1,
  class: "yzh-tree__icon"
}, ro = {
  key: 4,
  class: "yzh-tree__badge"
}, io = /* @__PURE__ */ se({
  __name: "YzhTree",
  props: {
    data: {},
    nodeKey: { default: "Code" },
    labelField: { default: "Name" },
    childrenField: { default: "Children" },
    isLeafField: { default: "IsLeaf" },
    extraField: { default: "Extra" },
    showCheckbox: { type: Boolean, default: !1 },
    checkStrictly: { type: Boolean, default: !1 },
    lazy: { type: Boolean, default: !1 },
    loadData: {},
    defaultExpandAll: { type: Boolean, default: !1 },
    expandOnClickNode: { type: Boolean, default: !0 },
    highlightCurrent: { type: Boolean, default: !0 },
    currentKey: {},
    searchable: { type: Boolean, default: !1 },
    searchPlaceholder: { default: "搜索节点" },
    highlightKeyword: { type: Boolean, default: !0 },
    nodeActions: { type: [Array, Function], default: () => [] },
    legacyNodeActions: { default: () => ({}) },
    getActionLabel: { type: Function, default: void 0 }
  },
  emits: ["node-click", "check-change", "node-expand", "node-collapse", "node-action"],
  setup(a, { expose: o, emit: e }) {
    function t(v) {
      return /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u.test(v);
    }
    function n(v, d) {
      if (!v) return;
      const h = d.charAt(0).toLowerCase() + d.slice(1);
      return v[d] ?? v[h];
    }
    const l = a, r = e, s = k(), c = k(""), f = k(null);
    function i(v) {
      return String(n(v, l.nodeKey) ?? "");
    }
    function b(v) {
      return String(n(v, l.labelField) ?? "");
    }
    function w(v) {
      return n(v, l.childrenField) ?? [];
    }
    function T(v) {
      return n(v, l.isLeafField) === !0;
    }
    function g(v) {
      return n(v, l.extraField) ?? {};
    }
    const U = H(() => ({
      label: l.labelField,
      children: l.childrenField,
      // 必须读叶子字段（后端 TreeControllerBase.FillIsLeafBatch 批量计算）。
      // 读错字段会让末端节点也长出展开箭头并白跑一次 tree/children。
      isLeaf: (v) => T(v),
      disabled: (v) => g(v).disabled ?? !1
    }));
    function u(v, d) {
      return v ? (b(d) || "").toLowerCase().includes(String(v).toLowerCase()) : !0;
    }
    function x(v) {
      return c.value ? (b(v) || "").toLowerCase().includes(c.value.toLowerCase()) : !1;
    }
    let G = null;
    Se(c, (v) => {
      G && clearTimeout(G), G = setTimeout(() => {
        var d;
        (d = s.value) == null || d.filter(v);
      }, 200);
    });
    function ee(v) {
      let d;
      typeof l.nodeActions == "function" ? d = l.nodeActions(v) || [] : d = l.nodeActions;
      const h = Object.entries(l.legacyNodeActions || {}).map(([m, _]) => ({
        key: m,
        text: l.getActionLabel ? l.getActionLabel(m, v) : _
      }));
      return [...d, ...h].filter((m) => m.visible !== !1);
    }
    function ne(v) {
      return v.danger ? "yzh-tree__action-danger" : v.type === "warning" ? "yzh-tree__action-toggle" : "";
    }
    function re(v) {
      r("node-click", v);
    }
    function le() {
      if (!s.value) return;
      const v = s.value.getCheckedNodes();
      r("check-change", v);
    }
    function S(v) {
      r("node-expand", v);
    }
    function E(v) {
      r("node-collapse", v);
    }
    function Y(v, d) {
      r("node-action", v, d);
    }
    function j() {
      var v;
      return ((v = s.value) == null ? void 0 : v.getCheckedNodes()) ?? [];
    }
    function J(v) {
      var d;
      (d = s.value) == null || d.setCheckedNodes(v);
    }
    function te(v, d) {
      var h;
      (h = s.value) == null || h.setChecked(v, d, !1);
    }
    function Q() {
      const v = (d) => {
        var h;
        for (const m of d) {
          const _ = (h = s.value) == null ? void 0 : h.store;
          _ && _.nodesMap[i(m)] && (_.nodesMap[i(m)].expanded = !0), w(m).length && v(w(m));
        }
      };
      v(l.data);
    }
    function me() {
      const v = (d) => {
        var h;
        for (const m of d) {
          const _ = (h = s.value) == null ? void 0 : h.store;
          _ && _.nodesMap[i(m)] && (_.nodesMap[i(m)].expanded = !1), w(m).length && v(w(m));
        }
      };
      v(l.data);
    }
    function fe(v) {
      var d;
      (d = s.value) == null || d.setCurrentKey(v);
    }
    function pe(v, d) {
      var h;
      if (s.value) {
        if (v) {
          try {
            s.value.append(d, v);
            return;
          } catch {
          }
          const m = s.value.store, _ = (h = m == null ? void 0 : m.nodesMap) == null ? void 0 : h[v];
          if (_ && typeof _.append == "function") {
            _.append(d);
            return;
          }
          if (Ce(l.data, v, d)) return;
        }
        l.data.push(d);
      }
    }
    function Ce(v, d, h) {
      for (const m of v) {
        if (i(m) === d) {
          const _ = w(m);
          return _.push(h), m[l.childrenField] = _, m[l.isLeafField] = !1, !0;
        }
        if (w(m).length && Ce(w(m), d, h))
          return !0;
      }
      return !1;
    }
    return o({
      getCheckedNodes: j,
      setCheckedNodes: J,
      setChecked: te,
      expandAll: Q,
      collapseAll: me,
      setCurrentNode: fe,
      appendNode: pe
    }), (v, d) => {
      const h = A("el-icon"), m = A("el-button"), _ = A("el-dropdown-item"), V = A("el-dropdown-menu"), I = A("el-dropdown");
      return p(), F("div", ao, [
        a.searchable ? (p(), F("div", no, [
          L(Ae(We), {
            modelValue: c.value,
            "onUpdate:modelValue": d[0] || (d[0] = (M) => c.value = M),
            placeholder: a.searchPlaceholder,
            clearable: "",
            "prefix-icon": "Search",
            size: "small"
          }, null, 8, ["modelValue", "placeholder"])
        ])) : W("", !0),
        L(Ae(Tt), {
          ref_key: "treeRef",
          ref: s,
          data: a.data,
          props: U.value,
          "show-checkbox": a.showCheckbox,
          "check-strictly": a.checkStrictly,
          lazy: a.lazy,
          load: a.loadData,
          "default-expand-all": a.defaultExpandAll,
          "expand-on-click-node": a.expandOnClickNode,
          "highlight-current": a.highlightCurrent,
          "node-key": a.nodeKey,
          "current-node-key": a.currentKey,
          "filter-node-method": u,
          "empty-text": "暂无数据",
          class: "yzh-tree__inner",
          onNodeClick: re,
          onCheckChange: le,
          onNodeExpand: S,
          onNodeCollapse: E
        }, {
          default: C(({ data: M }) => [
            B("div", {
              class: "yzh-tree__node",
              onMouseenter: ($) => f.value = i(M),
              onMouseleave: d[2] || (d[2] = ($) => f.value = null)
            }, [
              g(M).icon && !t(g(M).icon) ? (p(), z(h, {
                key: 0,
                class: "yzh-tree__icon"
              }, {
                default: C(() => [
                  (p(), z(Ee(g(M).icon)))
                ]),
                _: 2
              }, 1024)) : g(M).icon ? (p(), F("span", so, O(g(M).icon), 1)) : T(M) ? (p(), z(h, {
                key: 2,
                class: "yzh-tree__icon yzh-tree__icon--leaf"
              }, {
                default: C(() => [
                  L(Ae(eo))
                ]),
                _: 1
              })) : (p(), z(h, {
                key: 3,
                class: "yzh-tree__icon yzh-tree__icon--folder"
              }, {
                default: C(() => [
                  L(Ae(oo))
                ]),
                _: 1
              })),
              B("span", {
                class: ke(["yzh-tree__label", { "is-highlight": a.highlightKeyword && x(M) }])
              }, O(b(M)), 3),
              g(M).badge ? (p(), F("span", ro, O(g(M).badge), 1)) : W("", !0),
              ee(M).length ? (p(), z(I, {
                key: 5,
                trigger: "click",
                onCommand: ($) => Y($, M),
                onClick: d[1] || (d[1] = Ct(() => {
                }, ["stop"]))
              }, {
                dropdown: C(() => [
                  L(V, null, {
                    default: C(() => [
                      (p(!0), F(ae, null, ue(ee(M), ($) => (p(), z(_, {
                        key: $.key,
                        command: $.key,
                        disabled: $.disabled,
                        class: ke(ne($))
                      }, {
                        default: C(() => [
                          R(O($.text), 1)
                        ]),
                        _: 2
                      }, 1032, ["command", "disabled", "class"]))), 128))
                    ]),
                    _: 2
                  }, 1024)
                ]),
                default: C(() => [
                  L(m, {
                    link: "",
                    size: "small",
                    class: "yzh-tree__more-btn"
                  }, {
                    default: C(() => [...d[3] || (d[3] = [
                      R(" ⋯ ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                _: 2
              }, 1032, ["onCommand"])) : W("", !0)
            ], 40, lo)
          ]),
          _: 1
        }, 8, ["data", "props", "show-checkbox", "check-strictly", "lazy", "load", "default-expand-all", "expand-on-click-node", "highlight-current", "node-key", "current-node-key"])
      ]);
    };
  }
}), Qe = /* @__PURE__ */ he(io, [["__scopeId", "data-v-0970dd22"]]), co = { class: "yzh-tree-table" }, uo = { class: "yzh-tree-table__main" }, ho = {
  key: 0,
  class: "yzh-tree-table__tree-toolbar"
}, fo = {
  key: 1,
  class: "yzh-tree-table__tree-footer"
}, po = { class: "yzh-tree-table__table-panel" }, mo = /* @__PURE__ */ se({
  __name: "YzhTreeTable",
  props: {
    treeData: { default: () => [] },
    nodeKey: { default: "Code" },
    labelField: { default: "Name" },
    childrenField: { default: "Children" },
    isLeafField: { default: "IsLeaf" },
    extraField: { default: "Extra" },
    treeWidth: { default: 260 },
    treeToolbar: { type: Boolean, default: !0 },
    treeSearchable: { type: Boolean, default: !0 },
    treeCheckable: { type: Boolean, default: !1 },
    treeCheckStrictly: { type: Boolean, default: !1 },
    treeLazy: { type: Boolean, default: !1 },
    treeLoadData: {},
    treeDefaultExpandAll: { type: Boolean, default: !1 },
    nodeActions: { type: [Array, Function], default: () => [] },
    legacyNodeActions: { default: () => ({}) },
    getActionLabel: { type: Function, default: void 0 }
  },
  emits: ["tree-node-click", "tree-check-change", "tree-node-action"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(), r = k(""), s = H(() => r.value ? T(t.treeData, r.value) : t.treeData);
    function c(g) {
      n("tree-node-click", g);
    }
    function f(g) {
      n("tree-check-change", g);
    }
    function i(g, U) {
      n("tree-node-action", g, U);
    }
    function b() {
      var g;
      (g = l.value) == null || g.expandAll();
    }
    function w() {
      var g;
      (g = l.value) == null || g.collapseAll();
    }
    function T(g, U) {
      const u = U.toLowerCase(), x = [];
      for (const G of g) {
        const ne = String(G[t.labelField] ?? "").toLowerCase().includes(u), re = G[t.childrenField] ?? [], le = T(re, U);
        (ne || le.length > 0) && x.push({ ...G, [t.childrenField]: le });
      }
      return x;
    }
    return o({
      treeRef: l,
      getCheckedNodes: () => {
        var g;
        return ((g = l.value) == null ? void 0 : g.getCheckedNodes()) ?? [];
      },
      expandAll: b,
      collapseAll: w,
      appendNode: (g, U) => {
        var u;
        return (u = l.value) == null ? void 0 : u.appendNode(g, U);
      }
    }), (g, U) => (p(), F("div", co, [
      B("div", uo, [
        B("div", {
          class: "yzh-tree-table__tree-panel",
          style: Fe({ width: a.treeWidth + "px" })
        }, [
          a.treeToolbar ? (p(), F("div", ho, [
            a.treeSearchable ? (p(), z(Ae(We), {
              key: 0,
              modelValue: r.value,
              "onUpdate:modelValue": U[0] || (U[0] = (u) => r.value = u),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue"])) : W("", !0)
          ])) : W("", !0),
          L(Qe, {
            ref_key: "treeRef",
            ref: l,
            data: s.value,
            "node-key": a.nodeKey,
            "label-field": a.labelField,
            "children-field": a.childrenField,
            "is-leaf-field": a.isLeafField,
            "extra-field": a.extraField,
            "show-checkbox": a.treeCheckable,
            "check-strictly": a.treeCheckStrictly,
            lazy: a.treeLazy,
            "load-data": a.treeLoadData,
            "default-expand-all": a.treeDefaultExpandAll,
            "node-actions": a.nodeActions,
            "legacy-node-actions": a.legacyNodeActions,
            "get-action-label": a.getActionLabel,
            onNodeClick: c,
            onCheckChange: f,
            onNodeAction: i
          }, null, 8, ["data", "node-key", "label-field", "children-field", "is-leaf-field", "extra-field", "show-checkbox", "check-strictly", "lazy", "load-data", "default-expand-all", "node-actions", "legacy-node-actions", "get-action-label"]),
          g.$slots.treeFooter ? (p(), F("div", fo, [
            q(g.$slots, "treeFooter", {}, void 0, !0)
          ])) : W("", !0)
        ], 4),
        B("div", po, [
          q(g.$slots, "default", {}, void 0, !0)
        ])
      ])
    ]));
  }
}), La = /* @__PURE__ */ he(mo, [["__scopeId", "data-v-a51a3391"]]), yo = { class: "yzh-table" }, go = { class: "yzh-column-settings" }, vo = { class: "yzh-column-settings__body" }, bo = { class: "yzh-column-settings__footer" }, Co = { key: 1 }, wo = { class: "yzh-table__empty" }, ko = {
  key: 1,
  class: "yzh-table__error"
}, _o = {
  key: 2,
  class: "yzh-table__pagination"
}, To = /* @__PURE__ */ se({
  __name: "YzhTable",
  props: {
    columns: {},
    dataLoader: {},
    searchFields: {},
    selectable: { type: Boolean, default: !1 },
    selectMode: { default: void 0 },
    showPagination: { type: Boolean, default: !0 },
    pageSize: { default: 20 },
    defaultSort: {},
    height: {},
    rowKey: { default: "Code" },
    emptyText: { default: "暂无数据" },
    toolbar: { type: [Boolean, Object], default: !0 },
    toolbarActions: { default: () => [] },
    searchMaxFields: { default: 2 },
    noPadding: { type: Boolean, default: !1 },
    rowActionButtons: { type: [Object, Array, Function], default: () => [] },
    rowActionLink: { type: Boolean, default: !0 },
    actionMaxInline: { default: 0 }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(!1), r = k(""), s = k([]), c = k(0), f = k([]), i = k(1), b = k(t.pageSize), w = k(t.defaultSort || null), T = be({}), g = k(/* @__PURE__ */ new Set()), U = H(() => t.selectMode ? t.selectMode : t.selectable ? "multiple" : "none"), u = H(() => U.value === "multiple"), x = H(
      () => t.columns.filter((y) => y.label && y.prop !== "__yzh_action")
    ), G = H(
      () => t.columns.filter((y) => !(y.hidden || g.value.has(y.prop)))
    );
    function ee(y) {
      return Object.entries(y).map(([D, X]) => ({ key: D, text: X }));
    }
    function ne(y) {
      const D = typeof t.rowActionButtons == "function" ? t.rowActionButtons(y) : t.rowActionButtons;
      return (Array.isArray(D) ? D : ee(D || {})).filter((de) => de.visible !== !1);
    }
    const re = H(() => {
      const y = t.columns.some((X) => X.prop === "actions");
      return (typeof t.rowActionButtons == "function" || (Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length) > 0) && !y;
    }), le = H(() => t.actionMaxInline > 0);
    function S(y) {
      return !le.value || y.length <= t.actionMaxInline ? { inline: y, overflow: [] } : { inline: y.slice(0, t.actionMaxInline), overflow: y.slice(t.actionMaxInline) };
    }
    const E = H(
      () => t.toolbarActions.filter((y) => y.visible !== !1)
    );
    async function Y(y, D) {
      if (!y.disabled) {
        if (y.confirm)
          try {
            await xe.confirm(y.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        n("row-action", y.key, D, y);
      }
    }
    async function j(y) {
      if (!y.disabled) {
        if (y.confirm)
          try {
            await xe.confirm(y.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        n("toolbar-action", y.key, y);
      }
    }
    function J(y, D) {
      D ? g.value.delete(y.prop) : g.value.add(y.prop), g.value = new Set(g.value);
    }
    function te(y) {
      if (y.sortable === !1) return;
      const D = y.prop;
      w.value && w.value.prop === D ? w.value = { ...w.value, order: w.value.order === "asc" ? "desc" : "asc" } : w.value = { prop: D, order: "asc" };
    }
    function Q(y) {
      const D = y.prop;
      return !w.value || w.value.prop !== D ? "排序" : w.value.order === "asc" ? "↑ 升序" : "↓ 降序";
    }
    function me() {
      g.value = /* @__PURE__ */ new Set(), w.value = t.defaultSort || null;
    }
    function fe() {
      v();
    }
    const pe = H(() => t.toolbar === !1 ? {} : t.toolbar === !0 ? { columnSetting: !0 } : t.toolbar), Ce = H(() => Object.keys(pe.value).length > 0 || E.value.length > 0);
    async function v() {
      l.value = !0, r.value = "";
      try {
        const y = new Set(f.value.map((de) => de[t.rowKey])), D = {
          page: i.value,
          rows: b.value,
          ...w.value ? { sort: w.value.prop, order: w.value.order } : {},
          ...T
        }, X = await t.dataLoader(D);
        if (s.value = X.rows || [], c.value = X.total || 0, y.size > 0) {
          const de = [];
          for (const Re of s.value)
            y.has(Re[t.rowKey]) && de.push(Re);
          f.value = de;
        }
      } catch (y) {
        r.value = (y == null ? void 0 : y.message) || "数据加载失败", s.value = [], c.value = 0, Z.error(r.value);
      } finally {
        l.value = !1;
      }
    }
    function d({ prop: y, order: D }) {
      D ? w.value = {
        prop: y,
        order: D === "ascending" ? "asc" : "desc"
      } : w.value = null, v();
    }
    function h(y) {
      i.value = y, v();
    }
    function m(y) {
      b.value = y, i.value = 1, v();
    }
    function _(y) {
      Object.assign(T, y), i.value = 1, v();
    }
    function V() {
      Object.keys(T).forEach((y) => delete T[y]), t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((y) => {
        y.defaultValue !== void 0 && (T[y.prop] = y.defaultValue);
      }), i.value = 1, v();
    }
    function I(y) {
      f.value = y, n("selection-change", y);
    }
    function M(y, D) {
      n("row-click", y, D);
    }
    wt();
    let $ = !1;
    const oe = H(() => {
      if (typeof t.rowActionButtons == "function")
        return 4 * 70 + 40;
      const y = Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length;
      return y > 0 ? y * 70 + 40 : 140;
    });
    Se(
      () => typeof t.rowActionButtons == "function" ? 1 : Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length,
      (y) => {
      },
      { immediate: !0 }
    );
    function ye() {
      i.value = 1, v(), n("refresh");
    }
    Be(() => {
      t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((y) => {
        y.defaultValue !== void 0 && (T[y.prop] = y.defaultValue);
      }), v();
    });
    function $e(y, D = "top") {
      D === "top" ? s.value.unshift(y) : s.value.push(y), c.value++;
    }
    function ie(y, D) {
      const X = s.value.findIndex((de) => y(de));
      X >= 0 && s.value.splice(X, 1, D);
    }
    function ze(y) {
      const D = s.value.findIndex((X) => y(X));
      D >= 0 && (s.value.splice(D, 1), c.value = Math.max(0, c.value - 1));
    }
    function _e() {
      return s.value.length;
    }
    function Pe(y, D) {
      if (D) {
        const X = new Set(f.value);
        for (const de of s.value)
          y(de) && !X.has(de) && f.value.push(de);
      } else
        f.value = f.value.filter((X) => !y(X));
      n("selection-change", [...f.value]);
    }
    return o({
      refresh: ye,
      loadData: v,
      insertRow: $e,
      replaceRow: ie,
      removeRow: ze,
      getRowCount: _e,
      getSelectedRows: () => f.value,
      setCheckedRows: Pe,
      clearSelection: () => {
        f.value = [], n("selection-change", []);
      }
    }), (y, D) => {
      const X = A("el-button"), de = A("el-checkbox"), Re = A("el-popover"), Ie = A("el-table-column"), Xe = A("el-tag"), ct = A("el-dropdown-item"), ut = A("el-dropdown-menu"), ht = A("el-dropdown"), ft = A("el-empty"), pt = A("el-table"), mt = kt("loading");
      return p(), F("div", yo, [
        a.searchFields && a.searchFields.length ? (p(), z(Pt, {
          key: 0,
          fields: a.searchFields,
          "default-values": T,
          cols: 2,
          "max-fields": a.searchMaxFields,
          onSearch: _,
          onReset: V
        }, null, 8, ["fields", "default-values", "max-fields"])) : W("", !0),
        Ce.value ? (p(), z(Mt, {
          key: 1,
          buttons: E.value,
          onAction: D[0] || (D[0] = (N, K) => j(K))
        }, {
          left: C(() => [
            q(y.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          right: C(() => [
            q(y.$slots, "toolbar-right", {
              selected: f.value,
              refresh: ye
            }, () => [
              pe.value.columnSetting ? (p(), z(Re, {
                key: 0,
                trigger: "click",
                placement: "bottom-end",
                width: 200
              }, {
                reference: C(() => [
                  L(X, { text: "" }, {
                    default: C(() => [...D[1] || (D[1] = [
                      B("i", { class: "bi bi-columns" }, null, -1),
                      R(" 列设置 ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                default: C(() => [
                  B("div", go, [
                    D[4] || (D[4] = B("div", { class: "yzh-column-settings__header" }, "列筛选与排序", -1)),
                    B("div", vo, [
                      (p(!0), F(ae, null, ue(x.value, (N) => {
                        var K;
                        return p(), F("div", {
                          key: N.prop,
                          class: "yzh-column-settings__item"
                        }, [
                          L(de, {
                            "model-value": !g.value.has(N.prop) && !N.hidden,
                            onChange: (ge) => J(N, ge)
                          }, {
                            default: C(() => [
                              R(O(N.label), 1)
                            ]),
                            _: 2
                          }, 1032, ["model-value", "onChange"]),
                          L(X, {
                            class: ke(["yzh-column-settings__sort-btn", { "is-active": ((K = w.value) == null ? void 0 : K.prop) === N.prop }]),
                            disabled: N.sortable === !1,
                            onClick: (ge) => te(N)
                          }, {
                            default: C(() => [
                              R(O(Q(N)), 1)
                            ]),
                            _: 2
                          }, 1032, ["class", "disabled", "onClick"])
                        ]);
                      }), 128))
                    ]),
                    B("div", bo, [
                      L(X, {
                        size: "small",
                        onClick: me
                      }, {
                        default: C(() => [...D[2] || (D[2] = [
                          R("重置", -1)
                        ])]),
                        _: 1
                      }),
                      L(X, {
                        size: "small",
                        type: "primary",
                        onClick: fe
                      }, {
                        default: C(() => [...D[3] || (D[3] = [
                          R("确定", -1)
                        ])]),
                        _: 1
                      })
                    ])
                  ])
                ]),
                _: 1
              })) : W("", !0)
            ], !0)
          ]),
          _: 3
        }, 8, ["buttons"])) : W("", !0),
        B("div", {
          class: ke(["yzh-table__wrapper", { "yzh-table__wrapper--no-padding": a.noPadding }])
        }, [
          B("div", {
            class: "yzh-table__body",
            style: Fe(a.height ? { height: typeof a.height == "number" ? a.height + "px" : a.height } : {})
          }, [
            _t((p(), z(pt, {
              data: s.value,
              "row-key": a.rowKey,
              height: a.height !== void 0 && a.height !== null ? a.height : "100%",
              "highlight-current-row": U.value === "single",
              stripe: "",
              border: "",
              onSelectionChange: I,
              onSortChange: d,
              onRowClick: M
            }, {
              empty: C(() => [
                B("div", wo, [
                  !l.value && !r.value ? (p(), z(ft, {
                    key: 0,
                    description: a.emptyText
                  }, null, 8, ["description"])) : r.value ? (p(), F("div", ko, [
                    D[7] || (D[7] = B("i", { class: "bi bi-exclamation-triangle" }, null, -1)),
                    B("span", null, O(r.value), 1),
                    L(X, {
                      text: "",
                      type: "primary",
                      onClick: ye
                    }, {
                      default: C(() => [...D[6] || (D[6] = [
                        R("重试", -1)
                      ])]),
                      _: 1
                    })
                  ])) : W("", !0)
                ])
              ]),
              default: C(() => [
                u.value ? (p(), z(Ie, {
                  key: 0,
                  type: "selection",
                  width: "48",
                  "reserve-selection": !1
                })) : W("", !0),
                (p(!0), F(ae, null, ue(G.value, (N) => (p(), z(Ie, {
                  key: N.prop,
                  prop: N.prop,
                  label: N.label,
                  width: N.width,
                  "min-width": N.minWidth,
                  fixed: N.fixed,
                  sortable: N.sortable,
                  align: N.align || "left",
                  "show-overflow-tooltip": !N.slot,
                  "class-name": N.className
                }, {
                  default: C(({ row: K, $index: ge }) => {
                    var Le;
                    return [
                      N.slot ? q(y.$slots, `column-${String(N.prop)}`, {
                        row: K,
                        index: ge,
                        value: K[N.prop]
                      }, () => [
                        R(O(N.formatter ? N.formatter(K[N.prop], K, ge) : K[N.prop]), 1)
                      ], !0, 0) : N.dictCode ? (p(), F(ae, { key: 1 }, [
                        N.tagType ? (p(), z(Xe, {
                          key: 0,
                          type: N.tagType,
                          "disable-transitions": ""
                        }, {
                          default: C(() => [
                            R(O(K[N.prop]), 1)
                          ]),
                          _: 2
                        }, 1032, ["type"])) : (p(), F("span", Co, O(K[N.prop]), 1))
                      ], 64)) : N.tagMap ? (p(), z(Xe, {
                        key: 2,
                        type: ((Le = N.tagTypeMap) == null ? void 0 : Le[K[N.prop]]) ?? "info",
                        size: "small",
                        "disable-transitions": ""
                      }, {
                        default: C(() => [
                          R(O(N.tagMap[K[N.prop]] ?? K[N.prop]), 1)
                        ]),
                        _: 2
                      }, 1032, ["type"])) : (p(), F(ae, { key: 3 }, [
                        R(O(N.formatter ? N.formatter(K[N.prop], K, ge) : K[N.prop]), 1)
                      ], 64))
                    ];
                  }),
                  _: 2
                }, 1032, ["prop", "label", "width", "min-width", "fixed", "sortable", "align", "show-overflow-tooltip", "class-name"]))), 128)),
                re.value ? (p(), z(Ie, {
                  key: 1,
                  label: "操作",
                  width: oe.value,
                  fixed: "right",
                  align: "center"
                }, {
                  default: C(({ row: N }) => [
                    (p(!0), F(ae, null, ue(S(ne(N)).inline, (K) => (p(), z(X, {
                      key: K.key,
                      link: a.rowActionLink,
                      size: "small",
                      type: K.type ?? "primary",
                      disabled: K.disabled,
                      onClick: (ge) => Y(K, N)
                    }, {
                      default: C(() => [
                        R(O(K.text), 1)
                      ]),
                      _: 2
                    }, 1032, ["link", "type", "disabled", "onClick"]))), 128)),
                    S(ne(N)).overflow.length > 0 ? (p(), z(ht, {
                      key: 0,
                      trigger: "click",
                      onCommand: (K) => {
                        const ge = S(ne(N)).overflow.find((Le) => Le.key === K);
                        ge && Y(ge, N);
                      }
                    }, {
                      dropdown: C(() => [
                        L(ut, null, {
                          default: C(() => [
                            (p(!0), F(ae, null, ue(S(ne(N)).overflow, (K) => (p(), z(ct, {
                              key: K.key,
                              command: K.key,
                              disabled: K.disabled,
                              class: ke({ "yzh-row-action-danger": K.type === "danger" })
                            }, {
                              default: C(() => [
                                R(O(K.text), 1)
                              ]),
                              _: 2
                            }, 1032, ["command", "disabled", "class"]))), 128))
                          ]),
                          _: 2
                        }, 1024)
                      ]),
                      default: C(() => [
                        L(X, {
                          link: "",
                          size: "small"
                        }, {
                          default: C(() => [...D[5] || (D[5] = [
                            R("更多", -1)
                          ])]),
                          _: 1
                        })
                      ]),
                      _: 2
                    }, 1032, ["onCommand"])) : W("", !0)
                  ]),
                  _: 1
                }, 8, ["width"])) : W("", !0)
              ]),
              _: 3
            }, 8, ["data", "row-key", "height", "highlight-current-row"])), [
              [mt, l.value]
            ])
          ], 4)
        ], 2),
        a.showPagination ? (p(), F("div", _o, [
          L(Ot, {
            page: i.value,
            "page-size": b.value,
            total: c.value,
            "onUpdate:page": h,
            "onUpdate:pageSize": m
          }, null, 8, ["page", "page-size", "total"])
        ])) : W("", !0)
      ]);
    };
  }
}), xo = /* @__PURE__ */ he(To, [["__scopeId", "data-v-53e6fb2a"]]), So = { class: "yzh-tree-table-selector" }, zo = {
  key: 0,
  class: "yzh-tree-table-selector__tree-search"
}, Ao = { class: "yzh-tree-table-selector__tree-actions" }, Fo = {
  key: 1,
  class: "yzh-tree-table-selector__tree-footer"
}, No = { class: "yzh-tree-table-selector__table-panel" }, Do = { class: "yzh-tree-table-selector__table-toolbar" }, $o = { class: "yzh-tree-table-selector__selection-info" }, Vo = /* @__PURE__ */ se({
  __name: "YzhTreeTableSelector",
  props: {
    treeData: {},
    treeWidth: { default: 260 },
    treeSearchable: { type: Boolean, default: !0 },
    treeDefaultExpandAll: { type: Boolean, default: !1 },
    treeLazy: { type: Boolean, default: !1 },
    treeLoadData: {},
    nodeKey: { default: "code" },
    checkStrictly: { type: Boolean, default: !0 },
    tableColumns: {},
    loadTableData: {},
    showPagination: { type: Boolean, default: !0 },
    pageSize: { default: 20 },
    rowKey: { default: "Code" }
  },
  emits: ["update:checkedTreeNodes", "update:checkedTableRows", "tree-check-change", "selection-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(), r = k(), s = k(""), c = k([]), f = k([]), i = k(/* @__PURE__ */ new Map()), b = H(() => s.value ? le(t.treeData, s.value) : t.treeData);
    function w() {
      var S;
      (S = l.value) == null || S.expandAll();
    }
    function T() {
      var S;
      (S = l.value) == null || S.collapseAll();
    }
    function g() {
      const S = (E) => {
        var Y;
        for (const j of E)
          (Y = l.value) == null || Y.setChecked(j.Code, !0), j.Children && j.Children.length > 0 && S(j.Children);
      };
      S(t.treeData);
    }
    function U() {
      var S;
      (S = l.value) == null || S.setCheckedNodes([]);
    }
    function u(S) {
      G(S.Code);
    }
    async function x() {
      if (!l.value) return;
      const S = l.value.getCheckedNodes();
      c.value = S;
      const E = S.map((te) => te.Code), Y = [];
      for (const te of E) {
        const Q = await G(te);
        Q && Y.push(...Q);
      }
      const j = /* @__PURE__ */ new Set(), J = Y.filter((te) => {
        const Q = te[t.rowKey];
        return j.has(Q) ? !1 : (j.add(Q), !0);
      });
      f.value = J, r.value && r.value.setCheckedRows(
        (te) => J.some((Q) => Q[t.rowKey] === te[t.rowKey]),
        !0
      ), n("update:checkedTreeNodes", S), n("update:checkedTableRows", J), n("tree-check-change", S);
    }
    async function G(S) {
      if (i.value.has(S))
        return i.value.get(S);
      try {
        const Y = (await t.loadTableData(S)).rows ?? [];
        return i.value.set(S, Y), Y;
      } catch (E) {
        return Z.error(E.message || "加载表格数据失败"), null;
      }
    }
    async function ee(S) {
      if (c.value.length === 0)
        return { rows: [], total: 0 };
      const E = [];
      for (const me of c.value) {
        const fe = await G(me.Code);
        fe && E.push(...fe);
      }
      const Y = /* @__PURE__ */ new Set(), j = E.filter((me) => {
        const fe = me[t.rowKey];
        return Y.has(fe) ? !1 : (Y.add(fe), !0);
      }), J = (S.page - 1) * S.rows, te = J + S.rows;
      return { rows: j.slice(J, te), total: j.length };
    }
    function ne(S) {
      f.value = S, n("update:checkedTableRows", S), n("selection-change", S);
    }
    function re() {
      var S;
      (S = r.value) == null || S.clearSelection(), U(), c.value = [], f.value = [], i.value.clear(), n("update:checkedTreeNodes", []), n("update:checkedTableRows", []);
    }
    function le(S, E) {
      const Y = E.toLowerCase(), j = [];
      for (const J of S) {
        const te = (J.Name || "").toLowerCase().includes(Y), Q = le(J.Children ?? [], E);
        (te || Q.length > 0) && j.push({ ...J, Children: Q });
      }
      return j;
    }
    return o({
      getCheckedTreeNodes: () => c.value,
      getCheckedTableRows: () => f.value,
      clearSelection: re,
      refreshTable: () => {
        var S;
        return (S = r.value) == null ? void 0 : S.refresh();
      }
    }), (S, E) => {
      const Y = A("el-input"), j = A("el-button");
      return p(), F("div", So, [
        B("div", {
          class: "yzh-tree-table-selector__tree-panel",
          style: Fe({ width: a.treeWidth + "px" })
        }, [
          a.treeSearchable ? (p(), F("div", zo, [
            L(Y, {
              modelValue: s.value,
              "onUpdate:modelValue": E[0] || (E[0] = (J) => s.value = J),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search",
              size: "small"
            }, null, 8, ["modelValue"])
          ])) : W("", !0),
          B("div", Ao, [
            L(j, {
              size: "small",
              onClick: w
            }, {
              default: C(() => [...E[1] || (E[1] = [
                R("展开全部", -1)
              ])]),
              _: 1
            }),
            L(j, {
              size: "small",
              onClick: T
            }, {
              default: C(() => [...E[2] || (E[2] = [
                R("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(j, {
              size: "small",
              onClick: g
            }, {
              default: C(() => [...E[3] || (E[3] = [
                R("全选", -1)
              ])]),
              _: 1
            }),
            L(j, {
              size: "small",
              onClick: U
            }, {
              default: C(() => [...E[4] || (E[4] = [
                R("取消全选", -1)
              ])]),
              _: 1
            })
          ]),
          L(Qe, {
            ref_key: "treeRef",
            ref: l,
            data: b.value,
            "show-checkbox": !0,
            "check-strictly": a.checkStrictly,
            lazy: a.treeLazy,
            "load-data": a.treeLoadData,
            "default-expand-all": a.treeDefaultExpandAll,
            "node-key": a.nodeKey,
            onCheckChange: x,
            onNodeClick: u
          }, null, 8, ["data", "check-strictly", "lazy", "load-data", "default-expand-all", "node-key"]),
          S.$slots.treeFooter ? (p(), F("div", Fo, [
            q(S.$slots, "treeFooter", {}, void 0, !0)
          ])) : W("", !0)
        ], 4),
        B("div", No, [
          B("div", Do, [
            B("div", $o, [
              E[5] || (E[5] = R(" 已选择 ", -1)),
              B("strong", null, O(f.value.length), 1),
              E[6] || (E[6] = R(" 条记录 ", -1))
            ]),
            L(j, {
              size: "small",
              type: "danger",
              onClick: re,
              disabled: f.value.length === 0
            }, {
              default: C(() => [...E[7] || (E[7] = [
                R(" 清空选择 ", -1)
              ])]),
              _: 1
            }, 8, ["disabled"])
          ]),
          L(xo, {
            ref_key: "tableRef",
            ref: r,
            columns: a.tableColumns,
            "data-loader": ee,
            selectable: !0,
            "show-pagination": a.showPagination,
            "page-size": a.pageSize,
            "row-key": a.rowKey,
            onSelectionChange: ne
          }, null, 8, ["columns", "show-pagination", "page-size", "row-key"])
        ])
      ]);
    };
  }
}), Ea = /* @__PURE__ */ he(Vo, [["__scopeId", "data-v-8e5e846d"]]), Bo = { class: "yzh-tree-table-check-selector" }, Po = { class: "yzh-tree-table-check-selector__toolbar" }, Ro = { class: "yzh-tree-table-check-selector__selection-info" }, Lo = {
  key: 0,
  class: "yzh-tree-table-check-selector__search"
}, Eo = { class: "yzh-tree-table-check-selector__toolbar-actions" }, Uo = /* @__PURE__ */ se({
  __name: "YzhTreeTableCheckSelector",
  props: {
    flatData: {},
    nodeKey: { default: "Code" },
    parentKey: { default: "ParentCode" },
    nodeTypeField: { default: "NodeType" },
    checkField: { default: "CheckFlag" },
    columns: { default: () => [] },
    showTypeColumn: { type: Boolean, default: !0 },
    defaultExpandAll: { type: Boolean, default: !1 },
    typeLabels: { default: void 0 },
    typeTagTypes: { default: void 0 },
    checkAllExcludeTypes: { default: () => [] },
    cascade: { type: Boolean, default: !0 },
    searchable: { type: Boolean, default: !1 },
    searchFields: { default: () => ["Name"] },
    countType: { default: void 0 },
    searchPlaceholder: { default: "搜索" }
  },
  emits: ["check-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a;
    function n(h) {
      var m;
      return ((m = t.typeLabels) == null ? void 0 : m[h]) ?? h;
    }
    function l(h) {
      var m;
      return ((m = t.typeTagTypes) == null ? void 0 : m[h]) ?? "info";
    }
    const r = e, s = k(), c = k([]), f = k(""), i = k(/* @__PURE__ */ new Set()), b = k(/* @__PURE__ */ new Map()), w = k(/* @__PURE__ */ new Set()), T = k(!1), g = k(/* @__PURE__ */ new Set()), U = H(() => {
      var m;
      if (!t.countType) return i.value.size;
      let h = 0;
      for (const _ of i.value)
        ((m = b.value.get(_)) == null ? void 0 : m[t.nodeTypeField]) === t.countType && h++;
      return h;
    }), u = H(() => {
      var I, M;
      const h = f.value.trim().toLowerCase();
      if (!h) return t.flatData;
      const m = /* @__PURE__ */ new Set();
      for (const $ of t.flatData)
        t.searchFields.some(
          (ye) => String($[ye] ?? "").toLowerCase().includes(h)
        ) && m.add(String($[t.nodeKey]));
      const _ = new Map(t.flatData.map(($) => [String($[t.nodeKey]), $])), V = new Set(m);
      for (const $ of m) {
        let oe = (I = _.get($)) == null ? void 0 : I[t.parentKey];
        for (; oe && !V.has(String(oe)); )
          V.add(String(oe)), oe = (M = _.get(String(oe))) == null ? void 0 : M[t.parentKey];
      }
      return t.flatData.filter(($) => V.has(String($[t.nodeKey])));
    });
    function x(h) {
      const m = /* @__PURE__ */ new Map(), _ = [];
      for (const V of h) {
        const I = {
          ...V,
          children: []
        };
        m.set(V[t.nodeKey], I), b.value.set(V[t.nodeKey], I);
      }
      for (const V of h) {
        const I = m.get(V[t.nodeKey]), M = V[t.parentKey];
        M && m.has(M) ? m.get(M).children.push(I) : _.push(I);
      }
      return _;
    }
    function G(h) {
      const m = /* @__PURE__ */ new Set();
      function _(V) {
        for (const I of V)
          I[t.checkField] && m.add(I[t.nodeKey]), I.children && I.children.length > 0 && _(I.children);
      }
      _(h), i.value = m;
    }
    function ee() {
      if (!s.value) return;
      T.value = !0, s.value.clearSelection();
      const h = /* @__PURE__ */ new Set();
      for (const m of i.value) {
        const _ = b.value.get(m);
        _ && (s.value.toggleRowSelection(_, !0), h.add(m));
      }
      g.value = h, Te(() => {
        T.value = !1;
      });
    }
    function ne(h, m) {
      if (T.value = !0, b.value.clear(), !h || h.length === 0) {
        c.value = [], m && (i.value = /* @__PURE__ */ new Set()), Te(() => {
          ee(), re();
        });
        return;
      }
      c.value = x(h), m && G(c.value), t.defaultExpandAll && (w.value.clear(), le(c.value)), Te(() => {
        ee(), re();
      });
    }
    function re() {
      Te(() => {
        T.value = !1;
      });
    }
    Se(
      () => t.flatData,
      (h) => {
        ne(h, !0);
      },
      { immediate: !0 }
    ), Se(f, () => {
      ne(u.value, !1);
    });
    function le(h) {
      for (const m of h)
        m.children && m.children.length > 0 && (w.value.add(m[t.nodeKey]), le(m.children));
    }
    function S() {
      le(c.value);
    }
    function E() {
      w.value.clear(), T.value = !0;
      const h = c.value;
      c.value = [], Te(() => {
        c.value = h, re();
      });
    }
    function Y() {
      if (!s.value) return;
      T.value = !0;
      const h = J(c.value), m = new Set(i.value);
      for (const _ of h)
        m.add(_[t.nodeKey]), s.value.toggleRowSelection(_, !0);
      i.value = m, g.value = new Set(m), T.value = !1, pe([], h.map((_) => _[t.nodeKey]));
    }
    function j() {
      if (!s.value) return;
      T.value = !0;
      const h = Array.from(i.value);
      i.value = /* @__PURE__ */ new Set(), g.value = /* @__PURE__ */ new Set(), s.value.clearSelection(), T.value = !1, pe(h, []);
    }
    function J(h) {
      const m = t.checkAllExcludeTypes ?? [], _ = [];
      for (const V of h)
        m.includes(V[t.nodeTypeField]) || _.push(V), V.children && V.children.length > 0 && _.push(...J(V.children));
      return _;
    }
    function te(h) {
      const m = [], _ = (V) => {
        var I;
        for (const M of V)
          m.push(M), (I = M.children) != null && I.length && _(M.children);
      };
      return _(h.children ?? []), m;
    }
    function Q(h, m) {
      const _ = [];
      for (const V of m) h.has(V) || _.push(V);
      return _;
    }
    function me(h, m, _) {
      const V = new Set(_), I = (ie, ze) => {
        var Pe;
        const _e = String(ie[t.nodeKey]);
        ze ? V.add(_e) : V.delete(_e), (Pe = s.value) == null || Pe.toggleRowSelection(ie, ze);
      };
      T.value = !0, I(h, m);
      for (const ie of te(h)) I(ie, m);
      const M = t.checkAllExcludeTypes ?? [];
      let $ = h[t.parentKey];
      for (; $; ) {
        const ie = b.value.get(String($));
        if (!ie) break;
        const ze = ie.children.filter(
          (_e) => !M.includes(String(_e[t.nodeTypeField]))
        );
        I(ie, ze.length > 0 && ze.every((_e) => V.has(String(_e[t.nodeKey])))), $ = ie[t.parentKey];
      }
      T.value = !1;
      const oe = Q(V, _), ye = Q(_, V), $e = new Set(i.value);
      for (const ie of ye) $e.add(ie);
      for (const ie of oe) $e.delete(ie);
      i.value = $e, g.value = new Set(V), pe(oe, ye);
    }
    function fe(h) {
      if (T.value) return;
      const m = new Set(h.map(($) => String($[t.nodeKey]))), _ = g.value, V = Q(_, m), I = Q(m, _);
      if (V.length === 0 && I.length === 0) return;
      if (t.cascade) {
        const oe = V.length + I.length === 1 ? V[0] ?? I[0] : void 0, ye = oe ? b.value.get(oe) : void 0;
        if (ye) {
          me(ye, V.length > 0, _);
          return;
        }
      }
      g.value = m;
      const M = new Set(i.value);
      for (const $ of V) M.add($);
      for (const $ of I) M.delete($);
      i.value = M, V.length > 0 && pe([], V), I.length > 0 && pe(I, []);
    }
    function pe(h, m) {
      r("check-change", { added: m, removed: h });
    }
    function Ce() {
      return Array.from(i.value);
    }
    function v(h) {
      i.value = new Set(h), Te(() => {
        ee();
      });
    }
    function d() {
      const h = [];
      for (const m of i.value) {
        const _ = b.value.get(m);
        _ && h.push(_);
      }
      return h;
    }
    return o({
      getCheckedKeys: Ce,
      setCheckedKeys: v,
      getCheckedNodes: d,
      expandAll: S,
      collapseAll: E,
      checkAll: Y,
      uncheckAll: j
    }), (h, m) => {
      const _ = A("el-button"), V = A("el-table-column"), I = A("el-tag"), M = A("el-table");
      return p(), F("div", Bo, [
        B("div", Po, [
          B("div", Ro, [
            m[1] || (m[1] = R(" 已选择 ", -1)),
            B("strong", null, O(U.value), 1),
            m[2] || (m[2] = R(" 条记录 ", -1))
          ]),
          a.searchable ? (p(), F("div", Lo, [
            L(Ae(We), {
              modelValue: f.value,
              "onUpdate:modelValue": m[0] || (m[0] = ($) => f.value = $),
              placeholder: a.searchPlaceholder,
              clearable: "",
              size: "small",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue", "placeholder"])
          ])) : W("", !0),
          B("div", Eo, [
            L(_, {
              size: "small",
              onClick: S
            }, {
              default: C(() => [...m[3] || (m[3] = [
                R("展开全部", -1)
              ])]),
              _: 1
            }),
            L(_, {
              size: "small",
              onClick: E
            }, {
              default: C(() => [...m[4] || (m[4] = [
                R("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(_, {
              size: "small",
              onClick: Y
            }, {
              default: C(() => [...m[5] || (m[5] = [
                R("全选", -1)
              ])]),
              _: 1
            }),
            L(_, {
              size: "small",
              onClick: j
            }, {
              default: C(() => [...m[6] || (m[6] = [
                R("取消全选", -1)
              ])]),
              _: 1
            })
          ])
        ]),
        L(M, {
          ref_key: "tableRef",
          ref: s,
          data: c.value,
          "row-key": a.nodeKey,
          "tree-props": { children: "children", checkStrictly: !0 },
          onSelectionChange: fe,
          "default-expand-all": a.defaultExpandAll,
          style: { width: "100%" },
          class: "yzh-tree-table-check-selector__table"
        }, {
          default: C(() => [
            L(V, {
              type: "selection",
              width: "50"
            }),
            (p(!0), F(ae, null, ue(a.columns, ($) => (p(), z(V, {
              key: $.prop,
              prop: $.prop,
              label: $.label,
              width: $.width,
              "min-width": $.minWidth,
              fixed: $.fixed,
              "show-overflow-tooltip": $.showOverflowTooltip !== !1
            }, {
              default: C(({ row: oe }) => [
                q(h.$slots, `column-${$.prop}`, {
                  row: oe,
                  column: $
                }, () => [
                  $.prop === a.nodeTypeField ? (p(), z(I, {
                    key: 0,
                    type: l(oe[a.nodeTypeField]),
                    size: "small"
                  }, {
                    default: C(() => [
                      R(O(n(oe[a.nodeTypeField])), 1)
                    ]),
                    _: 2
                  }, 1032, ["type"])) : (p(), F(ae, { key: 1 }, [
                    R(O(oe[$.prop]), 1)
                  ], 64))
                ], !0)
              ]),
              _: 2
            }, 1032, ["prop", "label", "width", "min-width", "fixed", "show-overflow-tooltip"]))), 128))
          ]),
          _: 3
        }, 8, ["data", "row-key", "default-expand-all"])
      ]);
    };
  }
}), Ua = /* @__PURE__ */ he(Uo, [["__scopeId", "data-v-0aab416f"]]), Mo = { class: "yzh-empty-state__inner" }, Io = { class: "yzh-empty-state__title" }, Oo = {
  key: 2,
  class: "yzh-empty-state__description"
}, Ko = {
  key: 3,
  class: "yzh-empty-state__action"
}, Yo = /* @__PURE__ */ se({
  __name: "YzhEmptyState",
  props: {
    icon: { type: Object, required: !0 },
    title: { type: String, required: !0 },
    description: { type: String, default: "" },
    actionLabel: { type: String, default: "" },
    onAction: { type: Function, default: null },
    compact: { type: Boolean, default: !1 },
    iconSize: { type: Number, default: 48 },
    iconColor: { type: String, default: "var(--yzh-color-text-secondary)" },
    iconBackgroundColor: { type: String, default: "" },
    iconBackgroundPadding: { type: String, default: "20px" }
  },
  setup(a) {
    return (o, e) => {
      const t = A("el-icon"), n = A("el-button");
      return p(), F("div", {
        class: ke(["yzh-empty-state", { "is-compact": a.compact, "is-icon-bg": a.iconBackgroundColor }])
      }, [
        B("div", Mo, [
          a.iconBackgroundColor ? (p(), F("div", {
            key: 0,
            class: "yzh-empty-state__icon-wrap",
            style: Fe({ backgroundColor: a.iconBackgroundColor })
          }, [
            L(t, {
              class: "yzh-empty-state__icon",
              style: Fe({ fontSize: a.iconSize + "px", color: a.iconColor })
            }, {
              default: C(() => [
                (p(), z(Ee(a.icon)))
              ]),
              _: 1
            }, 8, ["style"])
          ], 4)) : (p(), z(t, {
            key: 1,
            class: "yzh-empty-state__icon",
            style: Fe({ fontSize: a.iconSize + "px", color: a.iconColor })
          }, {
            default: C(() => [
              (p(), z(Ee(a.icon)))
            ]),
            _: 1
          }, 8, ["style"])),
          B("div", Io, O(a.title), 1),
          a.description ? (p(), F("div", Oo, O(a.description), 1)) : W("", !0),
          a.actionLabel && a.onAction ? (p(), F("div", Ko, [
            q(o.$slots, "action", {}, () => [
              L(n, {
                size: "small",
                onClick: a.onAction
              }, {
                default: C(() => [
                  R(O(a.actionLabel), 1)
                ]),
                _: 1
              }, 8, ["onClick"])
            ], !0)
          ])) : W("", !0)
        ])
      ], 2);
    };
  }
}), Ma = /* @__PURE__ */ he(Yo, [["__scopeId", "data-v-33c080f4"]]), jo = /* @__PURE__ */ se({
  __name: "YzhStatusBadge",
  props: {
    type: { type: String, default: "info" },
    // success | warning | danger | info
    text: { type: String, default: "" },
    icon: { type: Object, default: null },
    size: { type: String, default: "small" }
    // small | default
  },
  setup(a) {
    const o = a, e = H(() => ({
      success: null,
      // 后续引入图标
      warning: null,
      danger: null,
      info: null
    })[o.type] || null);
    return (t, n) => {
      const l = A("el-icon");
      return p(), F("span", {
        class: ke(["yzh-status-badge", [`is-${a.type}`, `is-${a.size}`]])
      }, [
        a.icon || e.value ? (p(), z(l, {
          key: 0,
          class: "yzh-status-badge__icon"
        }, {
          default: C(() => [
            (p(), z(Ee(a.icon || e.value)))
          ]),
          _: 1
        })) : W("", !0),
        q(t.$slots, "default", {}, () => [
          R(O(a.text), 1)
        ], !0)
      ], 2);
    };
  }
}), Ia = /* @__PURE__ */ he(jo, [["__scopeId", "data-v-d7402330"]]), Wo = { class: "yzh-card" }, qo = {
  key: 0,
  class: "yzh-card__header"
}, Go = { class: "yzh-card__body" }, Ho = {
  key: 1,
  class: "yzh-card__footer"
}, Xo = /* @__PURE__ */ se({
  __name: "YzhCard",
  props: {
    title: { type: String, default: "" }
  },
  setup(a) {
    return (o, e) => (p(), F("div", Wo, [
      o.$slots.header || a.title ? (p(), F("div", qo, [
        q(o.$slots, "header", {}, () => [
          R(O(a.title), 1)
        ], !0)
      ])) : W("", !0),
      B("div", Go, [
        q(o.$slots, "default", {}, void 0, !0)
      ]),
      o.$slots.footer ? (p(), F("div", Ho, [
        q(o.$slots, "footer", {}, void 0, !0)
      ])) : W("", !0)
    ]));
  }
}), Oa = /* @__PURE__ */ he(Xo, [["__scopeId", "data-v-245f071f"]]);
function Jo(a) {
  return {
    TextBox: "text",
    TextArea: "textarea",
    NumberBox: "number",
    Decimal: "number",
    DatePicker: "date",
    DateTimePicker: "datetime",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "radio",
    CheckBox: "checkbox",
    Switch: "switch",
    Upload: "upload",
    TreeSelect: "treeSelect",
    Cascader: "cascader",
    PasswordBox: "password",
    Memo: "textarea"
  }[a] || "text";
}
function Zo(a) {
  return {
    NumberBox: "number",
    DatePicker: "date",
    DateTimePicker: "dateRange",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "select"
  }[a] || "text";
}
function Qo(a) {
  return {
    input: "text",
    select: "select",
    date: "date",
    cascader: "cascader"
  }[a] || "text";
}
function ea(a) {
  const o = a == null ? void 0 : a.Columns;
  if (!o) return [];
  const e = a == null ? void 0 : a.EnableField;
  return o.filter((t) => t.XsFlag).map((t) => {
    const n = {
      prop: t.FieldName,
      label: t.DesName,
      width: Number(t.Width) || void 0,
      sortable: t.Sortable || void 0,
      fixed: t.Fixed || void 0,
      align: t.Align || void 0,
      dictCode: t.DictCode || void 0
    };
    return t.Type === "CustomSlot" && (n.slot = t.FieldName), e && t.FieldName === e && (n.slot = t.FieldName), n;
  });
}
function et(a) {
  var t;
  const o = a == null ? void 0 : a.FormCols;
  return o && o > 0 ? o : (((t = a == null ? void 0 : a.Columns) == null ? void 0 : t.filter((n) => n.BcFlag).length) ?? 0) <= 10 ? 1 : 2;
}
function ta(a, o = "0", e) {
  const t = a == null ? void 0 : a.Columns, n = a == null ? void 0 : a.Schema;
  if (!t) return [];
  const l = et(a), r = Math.floor(24 / l), s = (e == null ? void 0 : e.withDefaults) ?? !1;
  return t.filter((c) => c.BcFlag && c.Type !== "Other").map((c) => {
    var g;
    const f = c.FieldName, i = aa(f), b = n == null ? void 0 : n[i], w = c.GroupIndex || "0", T = o !== "0" && w !== o;
    return {
      prop: f,
      label: c.DesName,
      type: Jo(c.Type),
      required: !c.Yxk,
      disabled: c.Enable === !1 || T,
      span: r,
      dictCode: c.DictCode || void 0,
      options: void 0,
      placeholder: (g = c.Type) != null && g.includes("Picker") ? `请选择${c.DesName}` : `请输入${c.DesName}`,
      defaultValue: s ? c.Mrz ? c.Type === "Switch" ? Number(c.Mrz) : c.Mrz : b == null ? void 0 : b.Default : void 0,
      fieldSchema: b
    };
  });
}
function Je(a) {
  const o = a == null ? void 0 : a.SearchFields;
  if (o && o.length > 0)
    return o.map((n) => ({
      prop: n.Field,
      label: n.Label,
      type: Qo(n.ControlType),
      placeholder: `请输入${n.Label}`,
      options: n.Options ?? void 0
    }));
  const e = a == null ? void 0 : a.Columns;
  if (!e) return [];
  const t = ["Upload", "TreeSelect", "Cascader", "CheckBox"];
  return e.filter((n) => n.XsFlag && n.Type !== "Other" && !t.includes(n.Type) && n.BcFlag).slice(0, 4).map((n) => ({
    prop: n.FieldName,
    label: n.DesName,
    type: Zo(n.Type),
    placeholder: `请输入${n.DesName}`
  }));
}
function oa(a) {
  const o = a == null ? void 0 : a.Toolbar;
  if (!o) return [];
  const e = [];
  if (o.Add !== !1 && e.push({ key: "add", text: "新增", type: "primary" }), o.Delete !== !1 && e.push({ key: "delete", text: "批量删除", type: "danger" }), o.Export !== !1 && e.push({ key: "export", text: "导出", type: "success" }), o.Import !== !1 && e.push({ key: "import", text: "导入", type: "warning" }), o.CustomButtons)
    for (const [t, n] of Object.entries(o.CustomButtons))
      e.push({ key: `custom:${n}`, text: t, type: "info" });
  return e;
}
function tt(a, o) {
  const e = (a == null ? void 0 : a.RowButtons) ?? {}, t = [];
  if (e.Edit !== !1 && t.push({ key: "edit", text: "编辑", type: "primary" }), e.Delete !== !1 && t.push({ key: "delete", text: "删除", type: "danger" }), e.Enable === !0 && o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), e.CustomButtons)
    for (const [n, l] of Object.entries(e.CustomButtons))
      t.push({ key: `custom:${l}`, text: n, type: "info" });
  return t;
}
function Ka(a, o) {
  const e = {};
  for (const t of tt(a, o)) e[t.key] = t.text;
  return e;
}
function Ya(a, o, e) {
  const t = [], n = a;
  if (!n) return t;
  if (n.AllowEdit && ((e == null ? void 0 : e.allowAddChild) !== !1 && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), n.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), n.CustomActions)
    for (const [l, r] of Object.entries(n.CustomActions))
      t.push({ key: `custom:${l}`, text: r, type: "info" });
  return t;
}
function ja(a, o) {
  var t;
  const e = ((t = o == null ? void 0 : o.Extra) == null ? void 0 : t.level) ?? (o == null ? void 0 : o.level) ?? -1;
  return {
    ...a,
    Extra: { ...a.Extra, level: e + 1 },
    Children: []
  };
}
function aa(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
const Oe = {}, Ke = "YZH_TOKEN", ve = {
  get: () => localStorage.getItem(Ke),
  set: (a) => localStorage.setItem(Ke, a),
  clear: () => localStorage.removeItem(Ke)
};
class ot {
  constructor(o) {
    /** 服务根地址（文件下载等场景需要读取） */
    P(this, "baseURL");
    P(this, "getToken");
    P(this, "onUnauthorized");
    P(this, "onError");
    this.baseURL = o.baseURL.replace(/\/$/, ""), this.getToken = o.getToken || (() => ve.get()), this.onUnauthorized = o.onUnauthorized, this.onError = o.onError;
  }
  /**
   * 通用请求方法
   * 原样透传：返回后端 JSON，不做 key 转换
   */
  async request(o, e = {}) {
    var b, w;
    const {
      method: t = "POST",
      params: n,
      body: l,
      headers: r = {},
      requireAuth: s = !0,
      raw: c = !1
    } = e;
    let f = o;
    const i = {
      method: t,
      headers: {
        "Content-Type": "application/json",
        ...r
      }
    };
    if (s !== !1) {
      const T = this.getToken();
      T && (i.headers.Authorization = `Bearer ${T}`);
    }
    if (n) {
      let T = n;
      const g = Object.keys(n), U = n.params;
      g.length === 1 && g[0] === "params" && U && typeof U == "object" && (console.warn(
        "[YzhApi] 查询参数多包了一层 params（应为 get(url, { a, b }) 而非 get(url, { params: { a, b } })），已自动解包：",
        U
      ), T = U);
      const u = new URLSearchParams();
      Object.entries(T).forEach(([G, ee]) => {
        ee != null && u.append(G, String(ee));
      });
      const x = u.toString();
      x && (f += (o.includes("?") ? "&" : "?") + x);
    }
    l !== void 0 ? i.body = JSON.stringify(l) : t !== "GET" && !n && (i.body = "{}");
    try {
      const T = await fetch(this.baseURL + f, i);
      if (T.status === 401)
        throw ve.clear(), (b = this.onUnauthorized) == null || b.call(this), new Error("登录已过期，请重新登录");
      const g = await T.json();
      if (!T.ok) {
        const U = (g == null ? void 0 : g.message) || (g == null ? void 0 : g.msg) || `请求失败 (${T.status})`, u = new Error(U);
        throw u.status = T.status, u.data = g, u;
      }
      return g;
    } catch (T) {
      throw (w = this.onError) == null || w.call(this, T), T;
    }
  }
  get(o, e, t) {
    return this.request(o, { ...t, method: "GET", params: e });
  }
  post(o, e, t) {
    return this.request(o, { ...t, method: "POST", body: e });
  }
  put(o, e, t) {
    return this.request(o, { ...t, method: "PUT", body: e });
  }
  delete(o, e) {
    return this.request(o, { ...e, method: "DELETE" });
  }
  /**
   * GET 二进制内容（带鉴权）——用于预览场景
   *
   * 背景：`<iframe src>` / `<img src>` 无法携带 Authorization 头（本平台 JWT 走 Header），
   * 直接渲染受保护的文件流必然 401。必须先带 Token 取回 Blob，再用 ObjectURL 渲染。
   *
   * @param url    相对路径
   * @param params 查询参数（追加到 URL）
   * @returns      Blob（MIME 取自响应头，缺失时调用方按魔数兜底）
   * @throws       401 / 业务错误：抛出带 status 的 Error（错误信息优先取后端 JSON 的 message）
   */
  async getBlob(o, e) {
    var s;
    const t = this.getToken();
    let n = o;
    if (e) {
      const c = new URLSearchParams();
      Object.entries(e).forEach(([i, b]) => {
        b != null && c.append(i, String(b));
      });
      const f = c.toString();
      f && (n += (o.includes("?") ? "&" : "?") + f);
    }
    const l = await fetch(this.baseURL + n, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (l.status === 401)
      throw ve.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
    if ((l.headers.get("content-type") || "").includes("application/json")) {
      const c = await l.json().catch(() => ({})), f = new Error((c == null ? void 0 : c.message) || (c == null ? void 0 : c.msg) || `请求失败 (${l.status})`);
      throw f.status = l.status, f;
    }
    if (!l.ok) {
      const c = new Error(`请求失败 (${l.status})`);
      throw c.status = l.status, c;
    }
    return await l.blob();
  }
  /**
   * POST 下载文件（导出）
   */
  async download(o, e, t) {
    var s;
    const n = this.getToken(), l = await fetch(this.baseURL + o, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...n ? { Authorization: `Bearer ${n}` } : {}
      },
      body: JSON.stringify(e)
    });
    if (l.status === 401)
      throw ve.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
    if (!l.ok) {
      const c = await l.json().catch(() => ({}));
      throw new Error(c.message || c.msg || "下载失败");
    }
    const r = await l.blob();
    this.triggerDownload(r, t);
  }
  /**
   * GET 下载文件（模板下载）
   */
  async downloadGet(o, e) {
    var r;
    const t = this.getToken(), n = await fetch(this.baseURL + o, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (n.status === 401)
      throw ve.clear(), (r = this.onUnauthorized) == null || r.call(this), new Error("登录已过期，请重新登录");
    if (!n.ok) {
      const s = await n.json().catch(() => ({}));
      throw new Error(s.message || s.msg || "下载失败");
    }
    const l = await n.blob();
    this.triggerDownload(l, e);
  }
  /**
   * 上传文件（导入）
   */
  async upload(o, e) {
    var l;
    const t = this.getToken(), n = await fetch(this.baseURL + o, {
      method: "POST",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      },
      body: e
    });
    if (n.status === 401)
      throw ve.clear(), (l = this.onUnauthorized) == null || l.call(this), new Error("登录已过期，请重新登录");
    return await n.json();
  }
  /**
   * 触发浏览器下载
   */
  triggerDownload(o, e) {
    const t = URL.createObjectURL(o), n = document.createElement("a");
    n.href = t, n.download = e, document.body.appendChild(n), n.click(), document.body.removeChild(n), URL.revokeObjectURL(t);
  }
}
const Ne = new ot({
  baseURL: (Oe == null ? void 0 : Oe.VITE_API_BASE) || "http://127.0.0.1:9992",
  onUnauthorized: () => {
    console.warn("[YzhApi] 401 未授权，请重新登录");
  }
}), Ve = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  YzhApiClient: ot,
  tokenStore: ve,
  yzhApi: Ne
}, Symbol.toStringTag, { value: "Module" })), De = "/api/file-storage";
function Wa(a, o) {
  const e = new FormData();
  return e.append("file", a), Ne.post(`${De}/upload`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function qa(a, o) {
  const e = new FormData();
  return a.forEach((t) => e.append("files", t)), Ne.post(`${De}/upload-batch`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function Ga(a) {
  const o = Ne.baseURL || "", e = localStorage.getItem("token") || "";
  return `${o}${De}/download?path=${encodeURIComponent(a)}&token=${e}`;
}
function Ha(a) {
  return Ne.post(`${De}/delete`, null, { params: { path: a } });
}
function Xa(a) {
  return Ne.get(`${De}/exists`, { path: a });
}
function Ja(a) {
  return Ne.get(`${De}/list`, { prefix: a });
}
function Za() {
  const a = k(ve.get() || ""), o = k(null), e = H(() => !!a.value);
  function t(s) {
    a.value = s, ve.set(s);
  }
  function n() {
    a.value = "", o.value = null, ve.clear();
  }
  function l(s, c) {
    return Promise.resolve();
  }
  function r() {
    n();
  }
  return {
    token: a,
    userInfo: o,
    isAuthenticated: e,
    setToken: t,
    clearToken: n,
    login: l,
    logout: r
  };
}
function Qa() {
  const a = k(!1), o = k([]), e = k(0), t = k(1), n = k(20), l = be({});
  async function r(f) {
    a.value = !0;
    try {
      const i = {
        page: t.value,
        rows: n.value,
        ...l
      }, b = await f(i);
      o.value = b.rows || [], e.value = b.total || 0;
    } finally {
      a.value = !1;
    }
  }
  function s(f) {
    Object.assign(l, f), t.value = 1;
  }
  function c() {
    Object.keys(l).forEach((f) => delete l[f]), t.value = 1;
  }
  return {
    loading: a,
    rows: o,
    total: e,
    page: t,
    pageSize: n,
    searchParams: l,
    loadData: r,
    setSearchParams: s,
    resetSearchParams: c
  };
}
function en() {
  async function a(o) {
    try {
      return await xe.confirm(o.message, o.title ?? "操作确认", {
        type: o.type ?? "warning",
        confirmButtonText: o.confirmButtonText ?? "确定",
        cancelButtonText: o.cancelButtonText ?? "取消"
      }), !0;
    } catch {
      return !1;
    }
  }
  return { confirm: a };
}
function tn(a, ...o) {
  const e = new a(...o), t = k(null);
  return Be(async () => {
    await e.init(), await Te(), e.setTableRef(t.value);
  }), { logic: e, tableRef: t };
}
function on(a, ...o) {
  const e = new a(...o), t = k(null), n = k(null);
  return Be(async () => {
    await e.init(), await Te(), e.setTableRef(t.value), e.setTreeTableRef(n.value);
  }), { logic: e, tableRef: t, treeTableRef: n };
}
function an(a, ...o) {
  const e = new a(...o);
  return Be(async () => {
    await e.init();
  }), { logic: e };
}
function nn(a, ...o) {
  const e = new a(...o);
  return Be(async () => {
    await e.init();
  }), { logic: e };
}
function at(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
function na(a) {
  return !a || a[0] >= "A" && a[0] <= "Z" ? a : a[0].toUpperCase() + a.slice(1);
}
function Ye(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[na(e)] = t;
  return o;
}
function ln(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[at(e)] = t;
  return o;
}
class la {
  constructor() {
    // ──── 后端配置 ────
    /** 后端页面配置（工具栏+表格+表单+搜索栏） */
    P(this, "config", k(null));
    // ──── 表格状态 ────
    /** 表格数据行（PascalCase 字段） */
    P(this, "rows", k([]));
    /** 表格加载状态 */
    P(this, "loading", k(!1));
    /** 选中行集合 */
    P(this, "selectedRows", k([]));
    // ──── 分页状态 ────
    /** 分页参数 */
    P(this, "pagination", be({ page: 1, pageSize: 20, total: 0 }));
    // ──── 搜索过滤状态 ────
    /** 搜索参数（PascalCase key，与业务实体字段名一致） */
    P(this, "searchParams", be({}));
    // ──── 排序状态 ────
    /** 排序字段（PascalCase） */
    P(this, "sortField", k());
    /** 排序方向 */
    P(this, "sortOrder", k());
    // ──── 弹窗状态 ────
    /** 弹窗可见性 */
    P(this, "dialogVisible", k(!1));
    /** 弹窗模式 */
    P(this, "dialogMode", k("add"));
    /**
     * 表单编辑模式（GroupIndex 控制）
     *
     * - '0'：新增/编辑模式，GroupIndex="0" 的字段可编辑
     * - '99'：详情模式，仅 GroupIndex="99" 的字段可编辑（JSON 通常不配 → 全部只读）
     */
    P(this, "formGroupIndex", k("0"));
    /** 提交中状态 */
    P(this, "submitting", k(!1));
    // ──── ShowDisabled 开关（基类统一管理，子类无需手动实现） ────
    /** 显示已禁用记录开关 */
    P(this, "showDisabled", k(!1));
    /**
     * 表单数据：PascalCase key（与 formFields[].prop、NewEntity、实体属性名一致）
     * 例：{ Code: "", UserName: "", Enable: 1 }
     */
    P(this, "formData", be({}));
    // ──── 表格引用（局部刷新） ────
    P(this, "_tableRef", null);
    // ========================================================
    // 动作统一（ST-7 dispatch + registerHandler）
    // ========================================================
    P(this, "handlers", /* @__PURE__ */ new Map());
    /** 当前编辑行（ST-8：提交后与后端返回合并，避免表格行丢字段） */
    P(this, "editingRow", k(null));
  }
  /** 切换 ShowDisabled 并刷新表格 */
  async toggleShowDisabled() {
    this.showDisabled.value = !this.showDisabled.value, await this.refresh();
  }
  /** 设置表格引用（模板中调用，或由 useSingleTable 注入） */
  setTableRef(o) {
    this._tableRef = o;
  }
  /** 刷新表格数据（触发 dataLoader 重新加载） */
  async refresh() {
    var o;
    await ((o = this._tableRef) == null ? void 0 : o.refresh());
  }
  // ========================================================
  // Computed: 从 config 派生 UI 结构（经 adapters/，业务字段不出内核）
  // ========================================================
  /** 表格列配置（AD-1） */
  get columns() {
    return ea(this.config.value);
  }
  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动） */
  get formLayoutCols() {
    return et(this.config.value);
  }
  /** 表单字段配置（AD-2） */
  get formFields() {
    return ta(this.config.value, this.formGroupIndex.value);
  }
  /** 搜索栏字段（config.SearchFields 优先；为空时走 fallbackSearchFields 钩子再走列推导） */
  get searchFields() {
    var t;
    const o = (t = this.config.value) == null ? void 0 : t.SearchFields;
    if (o && o.length > 0)
      return Je(this.config.value);
    const e = this.fallbackSearchFields;
    return e.length > 0 ? e : Je(this.config.value);
  }
  /**
   * 后端 SearchFields 缺失时的业务兜底（如 treepconfig 未映射历史的场景）
   * 子类可覆盖；默认空（走列推导）
   */
  get fallbackSearchFields() {
    return [];
  }
  /** 工具栏按钮（YzhAction[]，声明式，绑定 :toolbar-actions + @toolbar-action） */
  get toolbarActions() {
    return oa(this.config.value);
  }
  /** @deprecated 兼容旧形状（对象数组），等价 toolbarActions 的字段子集 */
  get toolbarButtons() {
    return this.toolbarActions.map((o) => ({
      key: o.key,
      text: o.text,
      type: o.type ?? "primary"
    }));
  }
  /**
   * 行操作按钮（YzhAction[] 或按行解析函数 —— 主形状，绑定 :row-action-buttons）
   *
   * 子类可覆盖为函数式：(row) => YzhAction[]（按行状态动态显隐/禁用）
   */
  get rowActions() {
    return tt(this.config.value, this.enableField);
  }
  /** 行操作按钮字典（兼容旧 Record 消费方，由 rowActions 派生） */
  get rowActionButtons() {
    const o = typeof this.rowActions == "function" ? this.rowActions({}) : this.rowActions, e = {};
    for (const t of o) e[t.key] = t.text;
    return e;
  }
  /** @deprecated 兼容旧形状（数组），由 rowActions 派生 */
  get rowButtons() {
    return (typeof this.rowActions == "function" ? this.rowActions({}) : this.rowActions).map((e) => ({ key: e.key, text: e.text, type: e.type ?? "primary" }));
  }
  /** 启用/禁用字段名（从 EntityConfig.EnableField 读取，null 表示不支持启用/禁用） */
  get enableField() {
    var o;
    return ((o = this.config.value) == null ? void 0 : o.EnableField) || null;
  }
  /** 主键字段名（PascalCase），统一使用 Code */
  get primaryKey() {
    return "Code";
  }
  // ========================================================
  // 覆盖点（ST-3/ST-4/ST-9）
  // ========================================================
  /** 新增默认值（合并到 NewEntity 之后；PascalCase key） */
  get defaultValues() {
    return {};
  }
  /** 确认弹窗中显示的实体名称字段（默认 Name；子类覆盖如 'RoleName'） */
  get entityNameField() {
    return "Name";
  }
  /** 读取行显示名称（需要拼接多个字段的页面覆盖此方法） */
  entityName(o) {
    const e = o == null ? void 0 : o[this.entityNameField];
    return e == null ? "" : String(e);
  }
  /** 提交前归一化钩子（如 Decimal 字符串→数值） */
  normalizeBeforeSubmit(o) {
    return o;
  }
  /** 数据加载后处理钩子（如编码→名称翻译） */
  postprocessRows(o) {
    return o;
  }
  // ========================================================
  // 初始化（ST-5）
  // ========================================================
  /** 初始化页面：加载配置 → onAfterInit（表格数据由 YzhTable dataLoader 自行加载） */
  async init() {
    await this.loadConfig(), await this.onAfterInit();
  }
  /** 加载页面配置（/api/{controller}/config） */
  async loadConfig() {
    const o = await this.apiGet("/config");
    this.config.value = o.data;
  }
  /** 配置加载完成后的钩子（子类在此做额外初始化，不再覆盖 init） */
  async onAfterInit() {
  }
  // ========================================================
  // 数据查询（/filter API）
  // ========================================================
  /** 分页查询（/filter API） */
  async loadPage() {
    this.loading.value = !0;
    try {
      const o = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: this.buildFilters()
      }, t = (await this.apiPost("/filter", o)).data;
      t && (this.rows.value = this.postprocessRows(t.Items ?? []), this.pagination.total = t.TotalCount ?? 0, this.onDataLoaded(this.rows.value));
    } catch {
      this.rows.value = [], this.pagination.total = 0;
    } finally {
      this.loading.value = !1;
    }
  }
  /**
   * YzhTable 数据加载器（页面直接绑定：`:data-loader="logic.dataLoader.bind(logic)"`）
   *
   * 入参由 YzhTable 传入：{ page, rows, sort, order, ...搜索条件 }
   * 搜索条件的 key = EntityConfig.SearchFields[].Field（PascalCase）
   * Operator 取自 SearchFields 配置（未配置时默认 eq）
   */
  async dataLoader(o) {
    const {
      page: e = 1,
      rows: t = this.pagination.pageSize,
      sort: n,
      order: l,
      ...r
    } = o;
    this.loading.value = !0;
    try {
      const s = {
        Page: e,
        PageSize: t,
        SortField: n,
        SortOrder: l,
        Filters: this.buildFilters(r)
      }, c = await this.apiPost("/filter", s), f = c == null ? void 0 : c.data, i = this.postprocessRows(((f == null ? void 0 : f.Items) ?? []).slice());
      return this.pagination.page = e, this.pagination.pageSize = t, this.pagination.total = (f == null ? void 0 : f.TotalCount) ?? 0, this.rows.value = i, this.onDataLoaded(i), { rows: i, total: this.pagination.total };
    } finally {
      this.loading.value = !1;
    }
  }
  /** 构建过滤条件（从 searchParams + 额外条件 + 自动 ShowDisabled） */
  buildFilters(o) {
    var l, r;
    const e = { ...this.searchParams, ...o || {} }, t = /* @__PURE__ */ new Map();
    if ((l = this.config.value) != null && l.SearchFields)
      for (const s of this.config.value.SearchFields)
        s.Operator && t.set(s.Field, s.Operator);
    const n = Object.entries(e).filter(
      ([, s]) => s != null && s !== "" && !(Array.isArray(s) && s.length === 0)
    ).map(([s, c]) => ({
      Field: s,
      Value: Array.isArray(c) ? c.join(",") : String(c),
      Operator: t.get(s) || "eq"
    }));
    return (r = this.config.value) != null && r.EnableField && this.showDisabled.value && n.push({ Field: "ShowDisabled", Value: "true", Operator: "eq" }), n;
  }
  // ========================================================
  // 写入操作
  // ========================================================
  /** 新增实体（/api/{controller}/add） */
  async add(o) {
    return (await this.apiPost("/add", o)).data;
  }
  /** 修改实体（/api/{controller}/update） */
  async update(o) {
    return (await this.apiPost("/update", o)).data;
  }
  /** 批量删除（/api/{controller}/delete） */
  async delete(o) {
    await this.apiPost("/delete", o);
  }
  /** 行操作（/api/{controller}/action/{methodName}） */
  async executeAction(o, e) {
    await this.apiPost(`/action/${o}`, e), await this.loadPage();
  }
  /**
   * 切换有效标志（IsValid: 0 ↔ 1）
   */
  async toggleIsValid(o) {
    const e = await this.apiPost(
      "/toggle-valid",
      { Code: o }
    );
    return e.success ? (Z.success(e.data.IsValid === 1 ? "已启用" : "已禁用"), e.data) : null;
  }
  /**
   * 切换行有效标志（完整流程：确认弹窗 → API → 本地更新）
   */
  async toggleRowIsValidWithConfirm(o, e) {
    const t = (e == null ? void 0 : e.field) ?? this.enableField ?? "IsValid", l = (o[t] ?? 1) === 1 ? "禁用" : "启用", r = (e == null ? void 0 : e.entityName) ?? this.entityName(o);
    await xe.confirm(
      r ? `确定${l}【${r}】？` : `确定${l}该记录？`,
      `${l}确认`,
      {
        type: "warning",
        confirmButtonText: `确定${l}`,
        cancelButtonText: "取消"
      }
    );
    const s = await this.toggleIsValid(o.Code);
    s && this.replaceRowByCode(o.Code, { ...o, [t]: s.IsValid });
  }
  // ========================================================
  // 导出导入
  // ========================================================
  /** 导出 */
  async exportData(o = "excel", e) {
    const t = {
      Filters: this.buildFilters(),
      format: o,
      fields: e
    };
    await this.apiPostAndDownload("/export", t, `export_${Date.now()}.${o}`);
  }
  /** 导入 */
  async importData(o) {
    const e = new FormData();
    return e.append("file", o), (await this.apiUpload("/import", e)).data;
  }
  /** 下载导入模板 */
  async downloadImportTemplate() {
    await this.apiGetAndDownload("/import/template", "import_template.xlsx");
  }
  // ========================================================
  // Split 数据方法（增量更新，不重新请求）
  // ========================================================
  /** 删除行（按主键 Code） */
  removeRowByCode(o) {
    if (this._tableRef)
      this._tableRef.removeRow((e) => String(e.Code) === String(o));
    else {
      const e = this.rows.value.findIndex((t) => t.Code === o);
      e >= 0 && (this.rows.value.splice(e, 1), this.pagination.total = Math.max(0, this.pagination.total - 1));
    }
  }
  /** 替换行（按主键 Code） */
  replaceRowByCode(o, e) {
    if (this._tableRef)
      this._tableRef.replaceRow((t) => String(t.Code) === String(o), e);
    else {
      const t = this.rows.value.findIndex((n) => n.Code === o);
      t >= 0 && this.rows.value.splice(t, 1, e);
    }
  }
  /** 插入行 */
  insertRow(o, e = "top") {
    this._tableRef ? this._tableRef.insertRow(o, e) : (e === "top" ? this.rows.value.unshift(o) : this.rows.value.push(o), this.pagination.total++);
  }
  // ========================================================
  // 钩子方法（子类可覆盖；与后端 OnBeforeAdd/OnAfterAdd/… 对齐）
  // ========================================================
  onDataLoaded(o) {
  }
  onBeforeAdd(o) {
  }
  onAfterAdd(o) {
  }
  onBeforeUpdate(o) {
  }
  onAfterUpdate(o) {
  }
  onDelete(o) {
    return !0;
  }
  onAfterDelete(o) {
  }
  onPrepareAdd(o) {
  }
  /** 注册自定义动作处理器（覆盖内置同名动作） */
  registerHandler(o, e) {
    this.handlers.set(o, e);
  }
  /**
   * 动作统一入口：行按钮 / 工具栏按钮 / 树节点动作都汇聚到这里。
   *
   * 内置分支：add / edit / delete / toggle-valid / export / import / batch-delete / custom:{method}
   */
  async dispatch(o, e, t) {
    const n = this.handlers.get(o);
    if (n) {
      await n(e, t);
      return;
    }
    switch (o) {
      case "add":
        this.openAddDialog();
        return;
      case "edit":
        e && this.openEditDialog(e);
        return;
      case "detail":
        e && this.openDetailDialog(e);
        return;
      case "delete":
        await this.confirmDelete(e ? [e] : void 0);
        return;
      case "batch-delete":
        await this.confirmDelete();
        return;
      case "toggle-valid":
        e && await this.toggleRowIsValidWithConfirm(e);
        return;
      case "export":
        await this.exportData();
        return;
      case "import":
        return;
      default:
        if (o.startsWith("custom:")) {
          const l = o.slice(7);
          e ? await this.executeAction(l, e) : await this.executeCustomToolbarAction(l);
        }
    }
  }
  /** 行动作入口（绑定 @row-action="logic.onRowAction"） */
  async onRowAction(o, e, t) {
    await this.dispatch(o, e, t);
  }
  /** 工具栏动作入口（绑定 @toolbar-action="logic.onToolbarAction"） */
  async onToolbarAction(o, e) {
    await this.dispatch(o, void 0, e);
  }
  /** @deprecated 兼容旧命名，等价 onToolbarAction */
  async onToolbarClick(o) {
    await this.dispatch(o);
  }
  /** @deprecated 兼容旧命名，等价 onRowAction */
  async onRowClick(o, e) {
    await this.dispatch(o, e);
  }
  // ========================================================
  // 事件处理（表格原生事件）
  // ========================================================
  onSearch(o) {
    this.resetObject(this.searchParams), Object.assign(this.searchParams, o), this.pagination.page = 1, this.loadPage();
  }
  onPageChange(o) {
    this.pagination.page = o, this.loadPage();
  }
  onSizeChange(o) {
    this.pagination.pageSize = o, this.pagination.page = 1, this.loadPage();
  }
  onSortChange(o, e) {
    this.sortField.value = o, this.sortOrder.value = e, this.loadPage();
  }
  onSelectionChange(o) {
    this.selectedRows.value = o;
  }
  // ========================================================
  // 弹窗操作
  // ========================================================
  openAddDialog() {
    this.dialogMode.value = "add", this.formGroupIndex.value = "0", this.initFormData(), this.onPrepareAdd(this.formData), this.dialogVisible.value = !0;
  }
  openEditDialog(o) {
    this.dialogMode.value = "edit", this.formGroupIndex.value = "0", this.editingRow.value = o, this.initFormData(o), this.dialogVisible.value = !0;
  }
  /** 打开详情弹窗（只读模式，formGroupIndex='99' → 所有字段只读） */
  openDetailDialog(o) {
    this.dialogMode.value = "detail", this.formGroupIndex.value = "99", this.editingRow.value = o, this.initFormData(o), this.dialogVisible.value = !0;
  }
  /**
   * 打开指定编辑模式的弹窗
   * @param row 行数据（null=新增）
   * @param groupIndex 编辑模式：'0'=全部可编辑, '1'=仅 GroupIndex=1 字段可编辑, '99'=全部只读
   */
  openDialogWithMode(o, e) {
    this.formGroupIndex.value = e, o ? (this.dialogMode.value = e === "99" ? "detail" : "edit", this.editingRow.value = o, this.initFormData(o)) : (this.dialogMode.value = "add", this.initFormData(), this.onPrepareAdd(this.formData)), this.dialogVisible.value = !0;
  }
  /**
   * 初始化表单数据（ST-3：NewEntity → defaultValues / 编辑行）
   */
  initFormData(o) {
    var n;
    const t = { ...((n = this.config.value) == null ? void 0 : n.NewEntity) || {} };
    o ? Object.assign(t, o) : Object.assign(t, this.defaultValues), this.resetObject(this.formData), Object.assign(this.formData, t);
  }
  async cancelDialog() {
    this.dialogVisible.value = !1;
  }
  async submitForm() {
    this.submitting.value = !0;
    try {
      if (this.dialogMode.value === "add") {
        this.onBeforeAdd(this.formData);
        const o = this.normalizeBeforeSubmit({ ...this.formData }), e = await this.add(o);
        this.onAfterAdd(this.formData), this.insertRow(e);
      } else {
        this.onBeforeUpdate(this.formData);
        const o = this.normalizeBeforeSubmit({ ...this.formData }), e = await this.update(o);
        this.onAfterUpdate(this.formData);
        const t = this.primaryKey;
        this.replaceRowByCode(
          e[t],
          { ...this.editingRow.value || {}, ...e }
        );
      }
      Z.success("保存成功"), this.dialogVisible.value = !1;
    } finally {
      this.submitting.value = !1;
    }
  }
  /**
   * 删除确认（ST-10：逐行名称）
   * @param rows 待删行（缺省取选中行）
   */
  async confirmDelete(o) {
    const e = o || this.selectedRows.value;
    if (e.length === 0) {
      Z.warning("请先选择要删除的记录");
      return;
    }
    const t = this.primaryKey, n = e.map((c) => String(c[t] || "")).filter(Boolean);
    if (!await this.onDelete(n)) return;
    const r = e.map((c) => this.entityName(c)).filter(Boolean);
    let s;
    r.length === 1 ? s = `确定删除【${r[0]}】？` : r.length > 1 && r.length <= 3 ? s = `确定删除 ${r.length} 条记录（${r.join("、")}）？` : s = `确定删除 ${n.length} 条记录？`, await xe.confirm(s, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.delete(n), Z.success("删除成功");
    for (const c of n)
      this.removeRowByCode(c);
    this.selectedRows.value = [], this.onAfterDelete(n);
  }
  async executeCustomToolbarAction(o) {
  }
  // ========================================================
  // API 调用
  // ========================================================
  async apiGet(o) {
    const e = `/api/${this.controllerName}${o}`, { yzhApi: t } = await Promise.resolve().then(() => Ve);
    return t.get(e);
  }
  async apiPost(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Ve);
    return n.post(t, e);
  }
  async apiPostAndDownload(o, e, t) {
    const n = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Ve);
    return l.download(n, e, t);
  }
  async apiGetAndDownload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Ve);
    return n.downloadGet(t, e);
  }
  async apiUpload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Ve);
    return n.upload(t, e);
  }
  // ========================================================
  // 私有工具方法
  // ========================================================
  resetObject(o) {
    Object.keys(o).forEach((e) => delete o[e]);
  }
}
class sa {
  constructor() {
    /** 树数据（PascalCase，与后端 DTO 保持一致） */
    P(this, "treeData", k([]));
    /** 树加载状态 */
    P(this, "treeLoading", k(!1));
    /** 当前选中节点 */
    P(this, "selectedNode", k(null));
    /** 节点索引：Code → { node, parent }（O(1) 查找/替换/删除） */
    P(this, "index", /* @__PURE__ */ new Map());
  }
  /** 整树替换并重建索引 */
  setNodes(o) {
    this.treeData.value = o, this.rebuildIndex();
  }
  /** 重建索引（懒加载追加后调用） */
  rebuildIndex() {
    this.index.clear();
    const o = (e, t) => {
      var n;
      for (const l of e)
        this.index.set(l.Code, { node: l, parent: t }), (n = l.Children) != null && n.length && o(l.Children, l);
    };
    o(this.treeData.value, null);
  }
  /** 注册单个节点（append 后调用） */
  register(o, e) {
    this.index.set(o.Code, { node: o, parent: e });
  }
  /** O(1) 查找节点 */
  findNode(o) {
    var e;
    return ((e = this.index.get(o)) == null ? void 0 : e.node) ?? null;
  }
  /** O(1) 查找父节点 */
  findParent(o) {
    var e;
    return ((e = this.index.get(o)) == null ? void 0 : e.parent) ?? null;
  }
  /** 追加子节点（不触发 API，仅更新本地树 + 索引） */
  appendChild(o, e) {
    if (o) {
      const t = this.findNode(o);
      if (t) {
        t.Children = t.Children || [], t.Children.push(e), t.IsLeaf = !1, this.register(e, t);
        return;
      }
    }
    this.treeData.value.push(e), this.register(e, null);
  }
  /** 删除节点（含整个子树），返回是否删除成功 */
  removeNode(o) {
    var r;
    const e = this.index.get(o);
    if (!e) return !1;
    const t = e.parent ? (r = e.parent).Children ?? (r.Children = []) : this.treeData.value, n = t.findIndex((s) => s.Code === o);
    if (n < 0) return !1;
    t.splice(n, 1);
    const l = (s) => {
      this.index.delete(s.Code);
      for (const c of s.Children ?? []) l(c);
    };
    return l(e.node), !0;
  }
  /** 替换节点（O(1) 定位） */
  replaceNode(o, e) {
    var r;
    const t = this.index.get(o);
    if (!t) return !1;
    const n = t.parent ? (r = t.parent).Children ?? (r.Children = []) : this.treeData.value, l = n.findIndex((s) => s.Code === o);
    return l < 0 ? !1 : (n.splice(l, 1, e), this.index.delete(o), this.register(e, t.parent), !0);
  }
  /** 展开到指定节点（返回节点是否存在） */
  has(o) {
    return this.index.has(o);
  }
}
function ra(a) {
  return {
    TextBox: "text",
    TextArea: "textarea",
    NumberBox: "number",
    Decimal: "number",
    DatePicker: "date",
    DateTimePicker: "datetime",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "radio",
    CheckBox: "checkbox",
    Switch: "switch",
    Upload: "upload",
    TreeSelect: "treeSelect",
    Cascader: "cascader",
    PasswordBox: "password",
    Memo: "textarea"
  }[a] || "text";
}
class sn extends la {
  constructor() {
    super(...arguments);
    // ──── 树能力混入（TT-2：状态 + 索引 + 增量变更） ────
    P(this, "treeSide", new sa());
    /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
    P(this, "treeTableConfig", k(null));
    // ──── 树节点表单弹窗状态 ────
    P(this, "treeDialogVisible", k(!1));
    P(this, "treeDialogMode", k("add"));
    P(this, "treeSubmitting", k(!1));
    /** 树节点表单数据：PascalCase key（与 treeFormFields prop 一致） */
    P(this, "treeFormData", be({}));
    /** 当前新增节点的父节点 */
    P(this, "treeParentNode", k(null));
    /** 当前编辑的节点 */
    P(this, "treeEditingNode", k(null));
    // ──── 树表组件引用（用于 appendNode 等直接操作） ────
    P(this, "_treeTableRef", null);
  }
  /** 树数据（PascalCase） */
  get treeData() {
    return this.treeSide.treeData.value;
  }
  set treeData(e) {
    this.treeSide.treeData.value = e;
  }
  /** 树加载状态 */
  get treeLoading() {
    return this.treeSide.treeLoading;
  }
  /** 当前选中节点 */
  get selectedNode() {
    return this.treeSide.selectedNode.value;
  }
  set selectedNode(e) {
    this.treeSide.selectedNode.value = e;
  }
  /** 设置树表组件引用（模板中调用，或由 useTreeTable 注入） */
  setTreeTableRef(e) {
    this._treeTableRef = e;
  }
  // ──── 树配置快捷访问 ────
  /** 树行为配置（YZH.Core.Stand/TreeBehaviorConfigDto） */
  get treeConfig() {
    var e;
    return ((e = this.treeTableConfig.value) == null ? void 0 : e.TreeConfig) ?? null;
  }
  /** 启用/禁用字段名（优先 TreeConfig.EnableField，fallback TableConfig.EnableField） */
  get enableField() {
    var e, t;
    return ((e = this.treeConfig) == null ? void 0 : e.EnableField) ?? ((t = this.config.value) == null ? void 0 : t.EnableField) ?? null;
  }
  /** 树节点表单配置（EntityConfigDto） */
  get treeFormConfig() {
    var e;
    return ((e = this.treeTableConfig.value) == null ? void 0 : e.TreeFormConfig) ?? null;
  }
  /** 未选中树节点时的表格行为 */
  get noSelectionBehavior() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.NoSelectionBehavior) ?? "empty";
  }
  /** 关联字段名（PascalCase） */
  get relateField() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.RelateField) || "ParentCode";
  }
  // ──── 自动注入的操作按钮（来自后端 /treepconfig） ────
  /** 树节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get treeCustomActions() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.CustomActions) ?? {};
  }
  /** 表格行自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get rowCustomButtons() {
    var e, t;
    return ((t = (e = this.config.value) == null ? void 0 : e.RowButtons) == null ? void 0 : t.CustomButtons) ?? {};
  }
  /**
   * 树节点操作按钮（YzhAction[]，TT-9：完全由后端 TreeConfig 配置驱动）
   *
   * AllowEdit → 编辑（+ 新增下级，取决于 AllowAddChild）；AllowDelete → 删除；
   * EnableField → 禁用/启用；CustomActions → 自定义动作。前端零硬编码。
   */
  get nodeActions() {
    return this.resolveTreeActions();
  }
  /** 树动作解析（子类可覆盖以追加自定义动作） */
  resolveTreeActions() {
    const e = [], t = this.treeConfig;
    if (!t) return e;
    if (t.AllowEdit && (this.allowAddChild && e.push({ key: "add-child", text: "新增下级" }), e.push({ key: "edit", text: "编辑" })), t.AllowDelete && e.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), this.enableField && e.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), t.CustomActions)
      for (const [n, l] of Object.entries(t.CustomActions))
        e.push({ key: `custom:${n}`, text: l, type: "info" });
    return e;
  }
  /**
   * 获取树节点操作按钮的显示文字（toggle-valid 按节点状态动态显示）
   */
  getNodeActionLabel(e, t) {
    if (e === "toggle-valid") {
      const l = this.enableField ?? "IsValid";
      return ((t.Extra || {})[l] ?? 1) === 1 ? "禁用" : "启用";
    }
    const n = this.nodeActions.find((l) => l.key === e);
    return (n == null ? void 0 : n.text) ?? e;
  }
  /** 树节点表单字段配置（保留 TreeFormConfig 专用布局规则：2 列 + ColSpan>1 占满） */
  get treeFormFields() {
    var l, r;
    const e = (l = this.treeFormConfig) == null ? void 0 : l.Columns, t = (r = this.treeFormConfig) == null ? void 0 : r.Schema;
    if (!e) return [];
    const n = 12;
    return e.filter((s) => s.BcFlag && s.Type !== "Other").map((s) => {
      var b;
      const c = s.FieldName, f = at(c), i = t == null ? void 0 : t[f];
      return {
        prop: c,
        label: s.DesName,
        type: ra(s.Type),
        required: !s.Yxk,
        disabled: s.Enable === !1,
        span: (s.ColSpan ?? 0) > 1 ? 24 : n,
        dictCode: s.DictCode || void 0,
        options: void 0,
        placeholder: (b = s.Type) != null && b.includes("Picker") ? `请选择${s.DesName}` : `请输入${s.DesName}`,
        defaultValue: s.Mrz ? s.Type === "Switch" ? Number(s.Mrz) : s.Mrz : i == null ? void 0 : i.Default,
        fieldSchema: i
      };
    });
  }
  // ========================================================
  // 覆盖点（TT-8）
  // ========================================================
  /** 是否允许「新增下级」：默认读后端 TreeConfig.AllowAddChild（ISO-9，扁平树由后端配置 false） */
  get allowAddChild() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.AllowAddChild) ?? !0;
  }
  /** 新增行是否要求先选中树节点（无层级树可覆盖为 false，ROL-2） */
  get requireTreeSelectionForAdd() {
    return !0;
  }
  /** 是否允许在指定节点下新增（organization=仅叶子；返回 false 时给出提示） */
  canAddUnderNode(e) {
    return !0;
  }
  /** 指定节点不可新增时的提示文案 */
  canAddUnderNodeMessage(e) {
    return "该节点不允许新增";
  }
  /** 树节点新增默认值（合并到 TreeFormConfig.NewEntity 之后） */
  get defaultTreeValues() {
    return {};
  }
  /** 树节点确认弹窗名称字段（organization=OrgName 等） */
  get treeEntityNameField() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.NameField) ?? "Name";
  }
  /** 关联过滤值：虚拟节点返回 null，其余返回选中节点 Code */
  relatedValue() {
    const e = this.selectedNode;
    return !e || this.isVirtualNode(e) ? null : e.Code;
  }
  /** 是否对表格查询应用树过滤（dictionary 全量模式等可覆盖） */
  shouldApplyTreeFilter() {
    return !!this.selectedNode && !this.isVirtualNode(this.selectedNode);
  }
  /** 虚拟节点判定（skill-manage 的 __all__ 等） */
  isVirtualNode(e) {
    return e.NodeType === "virtual";
  }
  /** 树加载完成后钩子（skill-manage 注入"全部"虚拟节点） */
  async afterTreeLoaded() {
  }
  /** 树加载后自动选中第一个节点（skill-manage=true） */
  get autoSelectFirstNode() {
    return !1;
  }
  // ──── 树节点生命周期钩子（与后端 OnBeforeAddTree/… 对齐，BE-5） ────
  onBeforeAddTree(e, t) {
  }
  onAfterAddTree(e, t) {
  }
  onBeforeUpdateTree(e, t) {
  }
  onAfterUpdateTree(e, t) {
  }
  onBeforeDeleteTree(e) {
    return !0;
  }
  onAfterDeleteTree(e) {
  }
  // ========================================================
  // 配置加载（覆盖：获取 TreeTableConfig）
  // ========================================================
  async loadConfig() {
    const e = await this.apiGet("/treepconfig");
    this.treeTableConfig.value = e.data, this.config.value = e.data.TableConfig;
  }
  // ========================================================
  // 生命周期（TT-3）
  // ========================================================
  /** 初始化：配置 → 树 → afterTreeLoaded → 自动选中 → onAfterInit */
  async init() {
    if (await this.loadConfig(), await this.loadTreeRoot(), await this.afterTreeLoaded(), this.autoSelectFirstNode && !this.selectedNode) {
      const e = this.treeData[0];
      e && await this.onNodeClick(e);
    }
    await this.onAfterInit();
  }
  // ========================================================
  // 树加载
  // ========================================================
  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot() {
    this.treeSide.treeLoading.value = !0;
    try {
      const t = (await this.apiPost("/tree/root", {})).data ?? [];
      this.treeSide.setNodes(t.map((n) => this.dtoToNode(n)));
    } finally {
      this.treeSide.treeLoading.value = !1;
    }
  }
  /** 懒加载子节点（/api/{controller}/tree/children） */
  async loadChildren(e, t) {
    var b;
    const l = Array.isArray(e == null ? void 0 : e.data) && e.data.length === 0 ? e : (e == null ? void 0 : e.data) ?? e, r = l == null ? void 0 : l.Code, s = ((b = l == null ? void 0 : l.Extra) == null ? void 0 : b.level) ?? 0;
    if (!r)
      return t && t([]), [];
    const i = ((await this.apiPost("/tree/children", {
      ParentCode: r,
      Level: s
    })).data ?? []).map((w) => this.dtoToNode(w, l));
    for (const w of i) this.treeSide.register(w, l);
    return t && t(i), l && typeof l == "object" && (l.children = i), i;
  }
  // ========================================================
  // 树→表格联动（TT-6/TT-7）
  // ========================================================
  /** 节点点击 → 表格联动刷新（dataLoader 已自动注入 RelateField） */
  async onNodeClick(e) {
    var t;
    this.treeSide.selectedNode.value = e, this.pagination.page = 1, !((t = this.treeConfig) != null && t.OnlyLeafSelectable && !e.IsLeaf) && (this._tableRef ? await this._tableRef.refresh() : await this.refreshTable());
  }
  /**
   * 覆盖 buildFilters：自动注入 RelateField 树过滤（TT-6）
   */
  buildFilters(e) {
    const t = super.buildFilters(e);
    return this.shouldApplyTreeFilter() && t.push({
      Field: this.relateField,
      Value: this.relatedValue(),
      Operator: "eq"
    }), t;
  }
  /**
   * 覆盖 dataLoader：未选中节点且 NoSelectionBehavior='empty' 时不发请求
   */
  async dataLoader(e) {
    return !this.shouldApplyTreeFilter() && this.noSelectionBehavior === "empty" ? (this.pagination.total = 0, { rows: [], total: 0 }) : super.dataLoader(e);
  }
  /** 带树条件的分页查询（兼容保留；新代码走统一 dataLoader） */
  async loadPageWithTree(e) {
    this.loading.value = !0;
    try {
      const t = [
        ...super.buildFilters(),
        { Field: this.relateField, Value: e, Operator: "eq" }
      ], n = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: t
      }, r = (await this.apiPost("/filter", n)).data;
      r && (this.rows.value = this.postprocessRows(r.Items ?? []), this.pagination.total = r.TotalCount ?? 0);
    } catch {
      this.rows.value = [], this.pagination.total = 0;
    } finally {
      this.loading.value = !1;
    }
  }
  /** 无树条件的表格加载 */
  async loadPageWithoutTree() {
    this.noSelectionBehavior === "empty" ? (this.rows.value = [], this.pagination.total = 0) : await this.loadPage();
  }
  /** 刷新右侧表格（保持当前选中节点） */
  async refreshTable() {
    this.selectedNode && this.shouldApplyTreeFilter() ? this._tableRef ? await this._tableRef.refresh() : await this.loadPageWithTree(this.selectedNode.Code) : await this.loadPageWithoutTree();
  }
  // ========================================================
  // 行 CRUD 泛型流（TT-4）
  // ========================================================
  /**
   * 打开行弹窗（新增需满足树选中/叶子约束）
   * @returns 是否成功打开（失败时已给出提示）
   */
  openRowDialog(e) {
    if (!e) {
      const t = this.selectedNode;
      return this.requireTreeSelectionForAdd && !t ? (Z.warning("请先在左侧选择节点"), !1) : t && !this.isVirtualNode(t) && !this.canAddUnderNode(t) ? (Z.warning(this.canAddUnderNodeMessage(t)), !1) : (this.dialogMode.value = "add", this.formGroupIndex.value = "0", this.initFormData(), this.onPrepareAdd(this.formData), this.dialogVisible.value = !0, !0);
    }
    return this.openEditDialog(e), !0;
  }
  /** 提交行表单（= submitForm 别名，语义化入口） */
  async submitRowForm() {
    await this.submitForm();
  }
  /** 删除单行（带确认，名称取 entityName） */
  async deleteRow(e) {
    await this.confirmDelete([e]);
  }
  /** 批量删除选中行（带确认） */
  async batchDeleteRows(e) {
    await this.confirmDelete(e);
  }
  // ========================================================
  // 树节点 CRUD 泛型流（TT-5）
  // ========================================================
  /**
   * 打开树节点弹窗
   * @param node 编辑目标（null=新增）
   * @param parent 新增时的父节点（缺省取当前选中节点）
   */
  openTreeNodeDialog(e = null, t = null) {
    var r, s, c;
    if (e) {
      this.treeDialogMode.value = "edit", this.treeEditingNode.value = e, this.treeParentNode.value = null, this.resetObject(this.treeFormData);
      const f = ((r = this.treeFormConfig) == null ? void 0 : r.NewEntity) || {}, i = e.Extra || {}, b = {};
      for (const w of this.treeFormFields)
        w.prop in i && (b[w.prop] = i[w.prop]);
      return Object.assign(this.treeFormData, f, b, {
        Code: e.Code,
        ParentCode: e.ParentCode,
        [this.treeEntityNameField]: e.Name
      }), this.treeDialogVisible.value = !0, !0;
    }
    const n = t ?? this.selectedNode;
    if (this.requireTreeSelectionForAdd && !n)
      return Z.warning("请先在左侧选择节点"), !1;
    if (n && !this.canAddUnderNode(n))
      return Z.warning(this.canAddUnderNodeMessage(n)), !1;
    this.treeDialogMode.value = "add", this.treeEditingNode.value = null, this.treeParentNode.value = n, this.resetObject(this.treeFormData);
    const l = ((s = this.treeFormConfig) == null ? void 0 : s.NewEntity) || {};
    return Object.assign(this.treeFormData, l, this.defaultTreeValues, {
      [this.treeEntityNameField]: "",
      ParentCode: (n == null ? void 0 : n.Code) ?? ((c = this.treeConfig) == null ? void 0 : c.RootParentCode) ?? null
    }), this.treeDialogVisible.value = !0, !0;
  }
  /** 提交树节点表单 */
  async submitTreeNodeForm() {
    this.treeSubmitting.value = !0;
    try {
      const e = this.normalizeBeforeSubmit({ ...this.treeFormData });
      if (this.treeDialogMode.value === "add") {
        this.onBeforeAddTree(e, this.treeParentNode.value);
        const t = await this.addTreeNode(this.treeParentNode.value, e);
        t && this.onAfterAddTree(t, this.treeParentNode.value);
      } else {
        const t = this.treeEditingNode.value;
        this.onBeforeUpdateTree(t, e), await this.updateTreeNode(
          t,
          e[this.treeEntityNameField] ?? "",
          e
        ), this.onAfterUpdateTree(t, e);
      }
      this.treeDialogVisible.value = !1, Z.success(this.treeDialogMode.value === "add" ? "创建成功" : "修改成功");
    } finally {
      this.treeSubmitting.value = !1;
    }
  }
  /** 删除树节点（完整流程：确认弹窗 → API → 本地更新 → 表格联动） */
  async deleteTreeNodeWithConfirm(e) {
    const t = e.Name;
    await this.onBeforeDeleteTree(e) && (await xe.confirm(`确定删除【${t}】？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.deleteTreeNode(e, !0), this.onAfterDeleteTree(e), Z.success("已删除"));
  }
  // ========================================================
  // 树节点底层操作（兼容保留）
  // ========================================================
  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(e, t) {
    var s, c;
    const n = {
      ...Ye(t),
      [((s = this.treeConfig) == null ? void 0 : s.ParentCodeField) ?? "ParentCode"]: (e == null ? void 0 : e.Code) ?? ((c = this.treeConfig) == null ? void 0 : c.RootParentCode) ?? null
    }, l = await this.apiPost("/tree/add", n), r = this.dtoToNode(l.data, e ?? void 0);
    return this._treeTableRef ? (this._treeTableRef.appendNode((e == null ? void 0 : e.Code) ?? null, r), this.treeSide.register(r, e)) : this.treeSide.appendChild((e == null ? void 0 : e.Code) ?? null, r), r;
  }
  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(e, t, n) {
    var f, i;
    const l = n ? Ye(n) : {}, r = {
      [((f = this.treeConfig) == null ? void 0 : f.CodeField) ?? "Code"]: e.Code,
      [((i = this.treeConfig) == null ? void 0 : i.NameField) ?? "Name"]: t,
      ...l
    }, s = await this.apiPost("/tree/update", r), c = this.dtoToNode(
      s.data ?? { ...e, Name: t },
      this.treeSide.findParent(e.Code)
    );
    this.treeSide.replaceNode(e.Code, c) || (e.Name = t);
  }
  /** 删除树节点（skipConfirm=true 时由调用方负责确认） */
  async deleteTreeNode(e, t = !1) {
    var l, r;
    if (!((l = this.treeConfig) != null && l.AllowDeleteWithChildren) && e.Children && e.Children.length > 0) {
      Z.warning("该节点包含子节点，请先删除子节点");
      return;
    }
    t || await xe.confirm(`确定删除节点 "${e.Name}"？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定",
      cancelButtonText: "取消"
    });
    const n = await this.apiPost("/tree/delete", [e.Code]);
    if (!n.success) {
      Z.error(n.message || "删除失败");
      return;
    }
    this.treeSide.removeNode(e.Code), ((r = this.selectedNode) == null ? void 0 : r.Code) === e.Code && (this.treeSide.selectedNode.value = null, await this.loadPageWithoutTree());
  }
  /** 树节点执行自定义操作 */
  async executeTreeAction(e, t, n) {
    var s;
    const l = n ? Ye(n) : {}, r = await this.apiPost(`/tree/action/${e}`, {
      [((s = this.treeConfig) == null ? void 0 : s.CodeField) ?? "Code"]: t.Code,
      ...l
    });
    return await this.loadTreeRoot(), r.data;
  }
  /** 切换树节点有效标志（自动更新 node.Extra[enableField]） */
  async toggleTreeNodeIsValid(e) {
    var l;
    const t = this.enableField ?? "IsValid", n = await this.apiPost(
      "/tree/toggle-valid",
      { [((l = this.treeConfig) == null ? void 0 : l.CodeField) ?? "Code"]: e.Code }
    );
    if (n.success) {
      const r = e.Extra || {};
      return r[t] = n.data.IsValid, e.Extra = { ...r }, Z.success(n.data.IsValid === 1 ? "已启用" : "已禁用"), n.data;
    }
    return null;
  }
  /** 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新） */
  async toggleTreeNodeWithConfirm(e, t) {
    const n = this.enableField ?? "IsValid", s = ((e.Extra || {})[n] ?? 1) === 1 ? "禁用" : "启用", c = (t == null ? void 0 : t.entityName) ?? e.Name;
    await xe.confirm(`确定${s}【${c}】？`, `${s}确认`, {
      type: "warning",
      confirmButtonText: `确定${s}`,
      cancelButtonText: "取消"
    }), await this.toggleTreeNodeIsValid(e);
  }
  // ========================================================
  // DTO → TreeNode 映射（AD-5）
  // ========================================================
  /** TreeItemDto → TreeNode（PascalCase，附 level 计算并注册索引） */
  dtoToNode(e, t) {
    var l;
    const n = (((l = t == null ? void 0 : t.Extra) == null ? void 0 : l.level) ?? -1) + 1;
    return {
      Code: e.Code,
      Name: e.Name,
      ParentCode: e.ParentCode ?? null,
      NodeType: e.NodeType,
      IsLeaf: e.IsLeaf,
      Extra: { ...e.Extra, level: n },
      Children: []
    };
  }
  // ========================================================
  // dispatch 扩展（TT-10）：树节点动作路由
  // ========================================================
  /** 树节点动作入口（绑定 @tree-node-action="logic.onNodeAction"） */
  async onNodeAction(e, t) {
    var l, r;
    const n = (r = (l = this.handlers) == null ? void 0 : l.get) == null ? void 0 : r.call(l, e);
    if (n) {
      await n(t, void 0);
      return;
    }
    switch (e) {
      case "add-child":
        this.openTreeNodeDialog(null, t);
        return;
      case "add-root":
        this.openTreeNodeDialog(null, null);
        return;
      case "edit":
      case "node-edit":
        this.openTreeNodeDialog(t);
        return;
      case "delete":
      case "node-delete":
        await this.deleteTreeNodeWithConfirm(t);
        return;
      case "toggle-valid":
      case "node-toggle-valid":
        await this.toggleTreeNodeWithConfirm(t);
        return;
      default:
        e.startsWith("custom:") && await this.executeTreeAction(e.slice(7), t);
    }
  }
  // ========================================================
  // 树 Split 方法（O(1)，基于 TreeSide 索引）
  // ========================================================
  /** @deprecated 兼容旧命名，等价 treeSide.removeNode */
  removeNodeFromTree(e) {
    this.treeSide.removeNode(e);
  }
  /** @deprecated 兼容旧命名，等价 treeSide.findNode（O(1)） */
  findNode(e) {
    return this.treeSide.findNode(e);
  }
  /** @deprecated 兼容旧命名，等价 treeSide.replaceNode */
  replaceTreeNode(e, t) {
    this.treeSide.replaceNode(e, t);
  }
  async refreshChildren(e) {
    var r;
    const l = ((await this.apiPost("/tree/children", {
      ParentCode: e.Code,
      Level: ((r = e.Extra) == null ? void 0 : r.level) ?? 0
    })).data ?? []).map((s) => this.dtoToNode(s, e));
    for (const s of l) this.treeSide.register(s, e);
    e.Children = l, e.IsLeaf = l.length === 0;
  }
  async refreshTree() {
    await this.loadTreeRoot();
  }
  // ========================================================
  // 兼容便捷方法
  // ========================================================
  onCheckChange(e, t) {
  }
  async addRootNode(e) {
    return this.addTreeNode(null, e);
  }
  async addChildNode(e, t) {
    return this.addTreeNode(e, t);
  }
  async renameNode(e, t) {
    await this.updateTreeNode(e, t);
  }
}
class nt {
  constructor(o) {
    // ──── 左树 ────
    P(this, "treeData", k([]));
    P(this, "selectedNode", k(null));
    // ──── 右侧关联数据 ────
    P(this, "associationData", k([]));
    // ──── 加载状态 ────
    P(this, "loading", k(!1));
    P(this, "saving", k(!1));
    // ──── 本地关联缓存（badge / 差集保存依据） ────
    P(this, "associationCache", be(/* @__PURE__ */ new Map()));
    P(this, "cacheLoaded", !1);
    // ──── API 注入 ────
    P(this, "api");
    this.api = o;
  }
  // ========================================================
  // 初始化：加载本地缓存 + 左树
  // ========================================================
  async init() {
    await this.initCache(), await this.loadTreeRoot();
  }
  async initCache() {
    if (!this.cacheLoaded)
      try {
        const o = await this.api.getAll();
        this.buildCache(o), this.cacheLoaded = !0;
      } catch (o) {
        console.error(`[${this.constructor.name}] 加载关联缓存失败:`, o);
      }
  }
  buildCache(o) {
    this.associationCache.clear();
    for (const e of o)
      this.associationCache.has(e.ContextCode) || this.associationCache.set(e.ContextCode, /* @__PURE__ */ new Set()), this.associationCache.get(e.ContextCode).add(e.TargetCode);
  }
  // ========================================================
  // 左树
  // ========================================================
  async loadTreeRoot() {
    try {
      const o = await this.api.getTreeRoot();
      return this.treeData.value = o, o;
    } catch (o) {
      return Z.error(o.message || "加载树失败"), [];
    }
  }
  async loadChildren(o, e) {
    try {
      const t = await this.api.getTreeChildren(o.data.Code, o.level ?? 0);
      e(t);
    } catch (t) {
      Z.error(t.message || "加载子节点失败"), e([]);
    }
  }
  // ========================================================
  // Badge（来自本地缓存，局部更新）
  // ========================================================
  getCountForNode(o) {
    var e;
    return ((e = this.associationCache.get(o)) == null ? void 0 : e.size) ?? 0;
  }
  getNodeBadge(o) {
    const e = this.getCountForNode(o);
    return e > 0 ? String(e) : void 0;
  }
  // ========================================================
  // 节点选择 → 加载关联态
  // ========================================================
  async handleNodeSelect(o) {
    if (!(!o || !o.Code)) {
      this.selectedNode.value = o, this.loading.value = !0;
      try {
        const e = await this.api.getAssociations(o.Code);
        for (const n of e)
          n.Extra && Object.assign(n, n.Extra);
        const t = this.associationCache.get(o.Code) ?? /* @__PURE__ */ new Set();
        for (const n of e)
          n.CheckFlag = t.has(n.Code);
        this.afterAssociationsLoaded(e, t), this.associationData.value = e;
      } catch (e) {
        Z.error(e.message || "加载数据失败"), this.associationData.value = [];
      } finally {
        this.loading.value = !1;
      }
    }
  }
  /** 子类覆盖：关联态加载后处理（如 role-api 的分组跟随） */
  afterAssociationsLoaded(o, e) {
  }
  // ========================================================
  // 增量保存（乐观更新：先本地后端，失败提示）
  // ========================================================
  async handleCheckChange(o) {
    if (!this.selectedNode.value) {
      Z.warning("请先选择左侧节点");
      return;
    }
    const e = this.selectedNode.value.Code;
    this.saving.value = !0;
    try {
      if (o.added.length > 0) {
        const t = this.buildSelections(o.added);
        if (t.length > 0) {
          const n = await this.api.add(e, t);
          this.syncCacheAdd(e, n.Applied ?? t.map((l) => l.Code));
        }
      }
      if (o.removed.length > 0) {
        const t = this.buildSelections(o.removed);
        t.length > 0 && (await this.api.remove(e, t), this.syncCacheRemove(e, t.map((n) => n.Code)));
      }
      Z.success("保存成功");
    } catch (t) {
      Z.error(t.message || "保存失败");
    } finally {
      this.saving.value = !1;
    }
  }
  syncCacheAdd(o, e) {
    let t = this.associationCache.get(o);
    t || (t = /* @__PURE__ */ new Set(), this.associationCache.set(o, t));
    for (const n of e) t.add(n);
  }
  syncCacheRemove(o, e) {
    const t = this.associationCache.get(o);
    if (t)
      for (const n of e) t.delete(n);
  }
  // ========================================================
  // 选择项构建（子类定义可勾选的 NodeType）
  // ========================================================
  /** 子类定义：哪些 NodeType 可被勾选（空数组 = 全部） */
  get selectableNodeTypes() {
    return [];
  }
  buildSelections(o) {
    const e = [], t = this.associationData.value;
    for (const n of o) {
      const l = t.find((r) => r.Code === n);
      l && (this.selectableNodeTypes.length === 0 || this.selectableNodeTypes.includes(l.NodeType)) && e.push({ Code: n, NodeType: l.NodeType });
    }
    return e;
  }
}
class rn extends nt {
  /** 可勾选的节点类型（如 ['menu']）—— 子类必须声明 */
  get selectableNodeTypes() {
    return [];
  }
}
class dn extends nt {
  /** 构造：linkApi 负责树/列表/保存；baseApi 可传 null 走空实现 */
  constructor(e) {
    super({
      getTreeRoot: e.treeRoot,
      getTreeChildren: async () => [],
      getAssociations: e.list,
      add: async () => ({ Updated: 0 }),
      remove: async () => ({ Updated: 0 }),
      getAll: async () => []
    });
    P(this, "linkApi");
    /** 搜索关键字（页面可绑定本地过滤） */
    P(this, "searchKey", k(""));
    this.linkApi = e;
  }
  /** 左侧节点的主键字段名（默认 Code） */
  get leftKeyField() {
    return "Code";
  }
  /** 右表行中承载关联键的字段名（如 StandardCode / PhaseCode） */
  get linkedKeyField() {
    return "Code";
  }
  /** 关联行数据（含 Linked） */
  get linkRows() {
    return this.associationData.value;
  }
  /** 节点选择 → list + 同步勾选快照 */
  async handleNodeSelect(e) {
    if (!(!e || !e.Code)) {
      this.selectedNode.value = e, this.loading.value = !0;
      try {
        const t = await this.linkApi.list(e.Code);
        this.associationData.value = t;
      } catch (t) {
        Z.error(t.message || "加载关联数据失败"), this.associationData.value = [];
      } finally {
        this.loading.value = !1;
      }
    }
  }
  /**
   * 差集保存：与「变更前 Linked 快照」比对后逐条提交。
   *
   * 由页面的 selection-change 调用：
   *   onSelectionChange(selection) → 差集计算 → saveLinked()
   *
   * @param currentCodes 当前勾选的 code 集合
   * @param itemName 行显示名（失败提示用）
   */
  async saveLinkedDiff(e, t) {
    const n = this.selectedNode.value;
    if (!n) return;
    const l = new Set(
      this.associationData.value.filter((i) => i.Linked).map((i) => i[this.linkedKeyField])
    ), r = [], s = [];
    for (const i of this.associationData.value) {
      const b = i[this.linkedKeyField], w = e.has(b), T = l.has(b);
      w && !T && r.push(i), !w && T && s.push(i);
    }
    for (const i of this.associationData.value)
      i.Linked = e.has(i[this.linkedKeyField]);
    const c = [
      ...r.map((i) => this.buildSavePayload(n, i, !0)),
      ...s.map((i) => this.buildSavePayload(n, i, !1))
    ];
    if (c.length === 0) return;
    const f = [];
    for (const i of c)
      try {
        await this.linkApi.save(i);
      } catch {
        const b = this.associationData.value.find(
          (w) => w[this.linkedKeyField] === i[this.linkedKeyField]
        );
        b && (b.Linked = !b.Linked), f.push(t(b ?? i));
      }
    f.length > 0 && Z.error(`保存失败：${f.join("、")}`);
  }
  /** 组装 save 请求体（子类可覆盖以适配后端字段） */
  buildSavePayload(e, t, n) {
    return {
      [this.leftKeyField]: e.Code,
      [this.linkedKeyField]: t[this.linkedKeyField],
      Linked: n
    };
  }
}
const cn = [
  {
    type: "root",
    label: "根机构",
    icon: "OfficeBuilding",
    allowedActions: ["add"],
    allowedChildTypes: ["company"],
    isRelatable: !1,
    onlyRoot: !0,
    onlyLeaf: !1,
    maxChildren: 1
  },
  {
    type: "company",
    label: "公司",
    icon: "Company",
    allowedActions: ["add", "edit", "delete", "select"],
    allowedChildTypes: ["company", "department"],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !1,
    maxChildren: 0
  },
  {
    type: "department",
    label: "部门",
    icon: "User",
    allowedActions: ["add", "edit", "delete", "select"],
    allowedChildTypes: ["department", "team"],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !1,
    maxChildren: 0
  },
  {
    type: "team",
    label: "小组",
    icon: "Avatar",
    allowedActions: ["edit", "delete", "select"],
    allowedChildTypes: [],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !0,
    maxChildren: 0
  }
];
function ia(a, o, e) {
  if (o === null || o === "")
    return [...a, e];
  const t = (n) => n.map((l) => l.Code === o ? { ...l, Children: [...l.Children ?? [], e] } : l.Children && l.Children.length > 0 ? { ...l, Children: t(l.Children) } : l);
  return t(a);
}
function da(a, o) {
  const e = [], t = (n) => {
    const l = [];
    for (const r of n)
      r.Code === o ? (e.push(r), ua(r).forEach((s) => e.push(s))) : r.Children && r.Children.length > 0 ? l.push({ ...r, Children: t(r.Children) }) : l.push(r);
    return l;
  };
  return { tree: t(a), removed: e };
}
function un(a, o, e, t) {
  const n = je(a, o);
  if (!n)
    return { tree: a, error: `节点 "${o}" 不存在` };
  if (o === e)
    return { tree: a, error: "不能移动到自己" };
  if (e !== null && e !== "") {
    if (!je(a, e))
      return { tree: a, error: `目标父节点 "${e}" 不存在` };
    if (lt(n, e))
      return { tree: a, error: "不能移动到自己的子树下（会形成循环）" };
  }
  if (t !== void 0 && t > 0) {
    const s = st(n);
    if ((e === null || e === "" ? 0 : ha(a, e)) + 1 + s > t)
      return { tree: a, error: `移动后深度将超过限制 (${t})` };
  }
  const { tree: l } = da(a, o);
  return { tree: ia(l, e, {
    ...n,
    ParentCode: e
  }) };
}
function hn(a, o, e) {
  const t = (n) => n.map((l) => l.Code === o ? { ...l, ...e } : l.Children && l.Children.length > 0 ? { ...l, Children: t(l.Children) } : l);
  return t(a);
}
function fn(...a) {
  const o = [];
  for (const e of a)
    o.push(...e);
  return o;
}
function pn(a, o) {
  const e = Ze(a), t = Ze(o), n = new Map(e.map((f) => [f.node.Code, f])), l = new Map(t.map((f) => [f.node.Code, f])), r = [], s = [], c = [];
  for (const [, f] of l) {
    const i = n.get(f.node.Code);
    if (!i)
      r.push(f.node);
    else if (!pa(i.node, f.node)) {
      const b = ma(i.node, f.node);
      c.push({ code: f.node.Code, changes: b });
    }
  }
  for (const [f] of n)
    l.has(f) || s.push(f);
  return { added: r, removed: s, updated: c };
}
function mn(a) {
  const o = [], e = ca(a), t = new Set(e.map((l) => l.Code)), n = /* @__PURE__ */ new Map();
  for (const l of e)
    n.set(l.Code, (n.get(l.Code) ?? 0) + 1);
  for (const [l, r] of n)
    r > 1 && o.push(`Code "${l}" 重复 ${r} 次`);
  for (const l of e)
    l.ParentCode != null && l.ParentCode !== "" && !t.has(l.ParentCode) && o.push(`节点 "${l.Name}" 的 ParentCode "${l.ParentCode}" 不存在`);
  return fa(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function ca(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a), o;
}
function je(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = je(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function ua(a) {
  const o = [], e = (t) => {
    o.push(t);
    for (const n of t.Children ?? []) e(n);
  };
  return e(a), o;
}
function lt(a, o) {
  for (const e of a.Children ?? [])
    if (e.Code === o || lt(e, o)) return !0;
  return !1;
}
function st(a) {
  if (!a.Children || a.Children.length === 0) return 1;
  let o = 0;
  for (const e of a.Children)
    o = Math.max(o, st(e));
  return o + 1;
}
function ha(a, o) {
  const e = (t, n) => {
    for (const l of t) {
      if (l.Code === o) return n;
      if (l.Children && l.Children.length > 0) {
        const r = e(l.Children, n + 1);
        if (r >= 0) return r;
      }
    }
    return -1;
  };
  return e(a, 0);
}
function fa(a) {
  const o = /* @__PURE__ */ new Set(), e = /* @__PURE__ */ new Set(), t = (n) => {
    if (e.has(n.Code)) return !0;
    if (o.has(n.Code)) return !1;
    o.add(n.Code), e.add(n.Code);
    for (const l of n.Children ?? [])
      if (t(l)) return !0;
    return e.delete(n.Code), !1;
  };
  for (const n of a)
    if (t(n)) return !0;
  return !1;
}
function Ze(a) {
  const o = [], e = (t, n) => {
    for (const l of t)
      o.push({ node: l, level: n }), l.Children && l.Children.length > 0 && e(l.Children, n + 1);
  };
  return e(a, 0), o;
}
function pa(a, o) {
  return a.Code === o.Code && a.Name === o.Name && a.ParentCode === o.ParentCode && a.NodeType === o.NodeType && a.IsLeaf === o.IsLeaf && a.Sort === o.Sort;
}
function ma(a, o) {
  const e = {};
  return a.Name !== o.Name && (e.Name = o.Name), a.ParentCode !== o.ParentCode && (e.ParentCode = o.ParentCode), a.NodeType !== o.NodeType && (e.NodeType = o.NodeType), a.IsLeaf !== o.IsLeaf && (e.IsLeaf = o.IsLeaf), a.Sort !== o.Sort && (e.Sort = o.Sort), e;
}
function ya(a, o, e) {
  if (!a || a.length === 0) return [];
  const {
    codeField: t,
    nameField: n,
    parentCodeField: l,
    typeField: r,
    leafField: s,
    sortField: c,
    extraFields: f,
    rootParentCode: i
  } = o, b = (e == null ? void 0 : e.maxLevel) ?? 0, w = e == null ? void 0 : e.startFromCode, T = (e == null ? void 0 : e.currentLevel) ?? 0;
  if (b > 0 && T >= b) return [];
  const g = /* @__PURE__ */ new Map(), U = [];
  for (const u of a) {
    const x = ce(u, t), G = ce(u, n), ee = ce(u, l) ?? null, ne = r ? ce(u, r) : void 0, re = s ? ce(u, s) : void 0, le = c ? ce(u, c) : void 0;
    let S;
    if (f && f.length > 0) {
      S = {};
      for (const Y of f)
        S[Y] = ce(u, Y);
    }
    const E = {
      Code: x,
      Name: G,
      ParentCode: ee,
      NodeType: ne,
      IsLeaf: re,
      Sort: le,
      Extra: S,
      Children: [],
      Raw: u
    };
    g.set(x, E);
  }
  for (const u of g.values())
    if (w && u.Code === w)
      U.push(u);
    else if (!w && (u.ParentCode === i || u.ParentCode === null || u.ParentCode === ""))
      U.push(u);
    else {
      const x = g.get(u.ParentCode ?? "");
      x && (x.Children = [...x.Children ?? [], u]);
    }
  return U.sort((u, x) => (u.Sort ?? 0) - (x.Sort ?? 0)), U;
}
function ga(a, o, e) {
  const {
    codeField: t,
    nameField: n,
    parentCodeField: l,
    typeField: r,
    leafField: s,
    sortField: c,
    extraFields: f
  } = o, i = ce(a, t), b = ce(a, n), w = ce(a, l) ?? null, T = r ? ce(r, r) : void 0, g = s ? ce(s, s) : void 0, U = c ? ce(a, c) : void 0;
  let u;
  if (f && f.length > 0) {
    u = {};
    for (const x of f)
      u[x] = ce(a, x);
  }
  return {
    Code: i,
    Name: b,
    ParentCode: w,
    NodeType: T,
    IsLeaf: g,
    Sort: U,
    Extra: u,
    Children: [],
    Raw: a
  };
}
function va(a, o) {
  const e = {
    [o.codeField]: a.Code,
    [o.nameField]: a.Name,
    [o.parentCodeField]: a.ParentCode
  };
  return o.typeField && a.NodeType && (e[o.typeField] = a.NodeType), o.sortField && a.Sort !== void 0 && (e[o.sortField] = a.Sort), e;
}
function qe(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a), o;
}
function Ge(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a.Children ?? []), o;
}
function ba(a) {
  return [a, ...Ge(a)];
}
function Ue(a, o) {
  const e = [];
  let t = a;
  for (; t && t.ParentCode; ) {
    const n = Me(o, t.ParentCode);
    if (n)
      e.unshift(n), t = n;
    else
      break;
  }
  return e;
}
function Ca(a, o) {
  return [...Ue(o, a).map((t) => t.Code), o.Code];
}
function wa(a, o) {
  return [...Ue(o, a).map((t) => t.Name), o.Name];
}
function Me(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = Me(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function rt(a, o) {
  for (const e of a) {
    if (o(e)) return e;
    if (e.Children && e.Children.length > 0) {
      const t = rt(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function He(a, o) {
  const e = [], t = (n) => {
    for (const l of n)
      o(l) && e.push(l), l.Children && l.Children.length > 0 && t(l.Children);
  };
  return t(a), e;
}
function ka(a, o) {
  return o.ParentCode ? Me(a, o.ParentCode) : null;
}
function _a(a, o) {
  return He(a, (e) => e.NodeType === o);
}
function it(a, o) {
  if (!o || !o.trim()) return [];
  const e = o.toLowerCase();
  return He(a, (t) => t.Name.toLowerCase().includes(e));
}
function Ta(a, o) {
  const e = it(a, o), t = /* @__PURE__ */ new Set();
  for (const n of e)
    t.add(n), Ue(n, a).forEach((r) => t.add(r));
  return Array.from(t);
}
function xa(a, o) {
  const e = (t) => {
    const n = [];
    for (const l of t) {
      const r = e(l.Children ?? []);
      (o(l) || r.length > 0) && n.push({
        ...l,
        Children: r
      });
    }
    return n;
  };
  return e(a);
}
function Sa(a) {
  return Ge(a).length;
}
function za(a) {
  const o = (e, t) => {
    if (e.length === 0) return t;
    let n = t;
    for (const l of e)
      l.Children && l.Children.length > 0 && (n = Math.max(n, o(l.Children, t + 1)));
    return n;
  };
  return o(a, 0);
}
function Aa(a) {
  return qe(a).length;
}
function Fa(a, o) {
  const e = [], t = (n, l) => {
    for (const r of n)
      l === o && e.push(r), r.Children && r.Children.length > 0 && t(r.Children, l + 1);
  };
  return t(a, 0), e;
}
function Na(a) {
  const o = [], e = qe(a), t = new Set(e.map((l) => l.Code));
  for (const l of e)
    !l.Code && l.Code !== null && o.push(`节点 ${l.Name} 的 Code 为空`);
  const n = /* @__PURE__ */ new Map();
  for (const l of e)
    n.set(l.Code, (n.get(l.Code) ?? 0) + 1);
  for (const [l, r] of n)
    r > 1 && o.push(`Code "${l}" 重复 ${r} 次`);
  for (const l of e)
    l.ParentCode != null && l.ParentCode !== "" && !t.has(l.ParentCode) && o.push(`节点 "${l.Name}" 的 ParentCode "${l.ParentCode}" 不存在`);
  return dt(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function dt(a) {
  const o = /* @__PURE__ */ new Set(), e = /* @__PURE__ */ new Set(), t = (n) => {
    if (e.has(n.Code)) return !0;
    if (o.has(n.Code)) return !1;
    o.add(n.Code), e.add(n.Code);
    for (const l of n.Children ?? [])
      if (t(l)) return !0;
    return e.delete(n.Code), !1;
  };
  for (const n of a)
    if (t(n)) return !0;
  return !1;
}
function ce(a, o) {
  if (!a || !o) return;
  const e = o.split(".");
  let t = a;
  for (const n of e) {
    if (t == null) return;
    t = t[n];
  }
  return t;
}
const yn = {
  // 构造
  buildTree: ya,
  entityToNode: ga,
  nodeToEntity: va,
  flattenTree: qe,
  // 遍历/查询
  getDescendants: Ge,
  getDescendantsWithSelf: ba,
  getAncestors: Ue,
  getPath: Ca,
  getPathNames: wa,
  findNode: Me,
  findNodeBy: rt,
  findNodesBy: He,
  getParent: ka,
  // 过滤/搜索
  filterByType: _a,
  search: it,
  searchWithAncestors: Ta,
  filterTree: xa,
  // 统计
  getChildrenCount: Sa,
  getDepth: za,
  getTotalCount: Aa,
  getNodesAtLevel: Fa,
  // 验证
  validate: Na,
  hasCycle: dt
};
export {
  nt as AssociationTreeCore,
  rn as CheckTreeCore,
  la as CrudPageLogic,
  dn as LinkTableCore,
  cn as OrgNodeTypeExamples,
  la as SingleTableCore,
  sa as TreeSide,
  sn as TreeTableCore,
  sn as TreeTableLogic,
  ot as YzhApiClient,
  Oa as YzhCard,
  Ra as YzhDialog,
  Ma as YzhEmptyState,
  zt as YzhForm,
  Ba as YzhFormDialog,
  Pa as YzhPageLayout,
  Ot as YzhPagination,
  Pt as YzhSearchBar,
  Ia as YzhStatusBadge,
  xo as YzhTable,
  Mt as YzhToolbar,
  Qe as YzhTree,
  La as YzhTreeTable,
  Ua as YzhTreeTableCheckSelector,
  Ea as YzhTreeTableSelector,
  ia as addNode,
  ya as buildTree,
  Ha as deleteStorageFile,
  pn as diff,
  ga as entityToNode,
  Xa as fileExists,
  _a as filterByType,
  xa as filterTree,
  Me as findNode,
  rt as findNodeBy,
  He as findNodesBy,
  ca as flatten,
  qe as flattenTree,
  Ue as getAncestors,
  Sa as getChildrenCount,
  za as getDepth,
  Ge as getDescendants,
  ba as getDescendantsWithSelf,
  Ga as getFileUrl,
  Fa as getNodesAtLevel,
  ka as getParent,
  Ca as getPath,
  wa as getPathNames,
  Aa as getTotalCount,
  dt as hasCycle,
  Ja as listFiles,
  Jo as mapControlType,
  Qo as mapSearchControlType,
  Zo as mapSearchType,
  fn as mergeRoots,
  un as moveSubtree,
  va as nodeToEntity,
  Ye as pascalCaseFormData,
  da as removeSubtree,
  ln as rowToFormData,
  it as search,
  Ta as searchWithAncestors,
  at as toCamelCase,
  ta as toFormFields,
  et as toFormLayoutCols,
  na as toPascalCase,
  Ka as toRowActionButtons,
  tt as toRowActions,
  Je as toSearchFields,
  ea as toTableColumns,
  oa as toToolbarActions,
  Ya as toTreeActions,
  ve as tokenStore,
  ja as treeItemToNode,
  yn as treeUtils,
  hn as updateNode,
  Wa as uploadFile,
  qa as uploadFileBatch,
  Za as useAuth,
  an as useCheckTree,
  en as useConfirm,
  nn as useLinkTable,
  tn as useSingleTable,
  Qa as useTable,
  on as useTreeTable,
  Na as validate,
  mn as validateTreeOps,
  Ne as yzhApi
};
