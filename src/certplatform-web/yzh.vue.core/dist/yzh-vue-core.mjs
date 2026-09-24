var Dt = Object.defineProperty;
var $t = (a, o, e) => o in a ? Dt(a, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[o] = e;
var z = (a, o, e) => $t(a, typeof o != "symbol" ? o + "" : o, e);
import { defineComponent as ne, ref as k, computed as j, reactive as Ce, watch as _e, resolveComponent as $, openBlock as m, createBlock as N, withCtx as T, createVNode as L, createElementBlock as R, Fragment as ae, renderList as ce, mergeProps as be, createTextVNode as V, toDisplayString as I, renderSlot as W, createCommentVNode as H, createElementVNode as P, unref as ze, normalizeStyle as Ne, withKeys as Rt, normalizeClass as ke, createSlots as dt, resolveDynamicComponent as Ie, withModifiers as Bt, getCurrentInstance as Pt, onMounted as Pe, resolveDirective as Vt, withDirectives as Lt, nextTick as xe, normalizeProps as Et, guardReactiveProps as Mt } from "vue";
import { ElInput as Qe, ElTree as Ut, ElMessageBox as Se, ElMessage as J } from "element-plus";
const It = {
  key: 0,
  class: "yzh-form__actions"
}, Ot = /* @__PURE__ */ ne({
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
    const t = a, n = e, l = k(), r = j(() => 24 / t.cols), s = j(() => {
      if (t.rules) return t.rules;
      const h = {};
      return t.fields.forEach((x) => {
        if (x.hidden) return;
        const K = [];
        x.required && K.push({
          required: !0,
          message: `请${x.type === "select" || x.type === "radio" || x.type === "switch" ? "选择" : "输入"}${x.label}`,
          trigger: x.trigger || (x.type === "select" || x.type === "switch" ? "change" : "blur")
        }), x.validator && K.push({ validator: x.validator, trigger: x.trigger || "blur" }), K.length && (h[x.prop] = K);
      }), h;
    }), d = Ce({});
    async function f(h) {
      if (h.options) return h.options;
      if (!h.loadOptions) return [];
      if (d[h.prop]) return d[h.prop];
      const x = await h.loadOptions();
      return d[h.prop] = x, x;
    }
    (async () => {
      for (const h of t.fields)
        if (h.loadOptions && !h.options)
          try {
            await f(h);
          } catch {
          }
    })();
    const c = Ce({});
    function b() {
      Object.keys(c).forEach((h) => delete c[h]), Object.assign(c, t.modelValue || {}), t.fields.forEach((h) => {
        c[h.prop] === void 0 && h.defaultValue !== void 0 && (c[h.prop] = h.defaultValue);
      });
    }
    b(), _e(
      () => t.modelValue,
      () => b(),
      { deep: !0 }
    ), _e(
      c,
      (h) => {
        n("update:modelValue", { ...h });
      },
      { deep: !0 }
    );
    async function w() {
      if (l.value)
        try {
          await l.value.validate(), n("submit", { ...c }), n("validate", !0);
        } catch (h) {
          n("validate", !1, h);
        }
    }
    function y() {
      var h;
      b(), (h = l.value) == null || h.clearValidate(), n("reset");
    }
    async function p() {
      var h;
      return (h = l.value) == null ? void 0 : h.validate();
    }
    async function A() {
      var h;
      (h = l.value) == null || h.resetFields();
    }
    return o({ validate: p, resetFields: A, formRef: l }), (h, x) => {
      const K = $("el-input"), ee = $("el-input-number"), le = $("el-option"), re = $("el-select"), se = $("el-radio"), F = $("el-radio-group"), E = $("el-checkbox"), q = $("el-checkbox-group"), G = $("el-switch"), X = $("el-date-picker"), te = $("el-tree-select"), Z = $("el-cascader"), me = $("el-form-item"), fe = $("el-col"), ye = $("el-row"), we = $("el-button"), ie = $("el-form");
      return m(), N(ie, {
        ref_key: "formRef",
        ref: l,
        model: c,
        rules: s.value,
        "label-width": a.labelWidth,
        "label-position": a.labelPosition,
        size: a.size,
        class: "yzh-form"
      }, {
        default: T(() => [
          L(ye, { gutter: 20 }, {
            default: T(() => [
              (m(!0), R(ae, null, ce(a.fields, (i) => (m(), R(ae, {
                key: i.prop
              }, [
                i.hidden ? H("", !0) : (m(), N(fe, {
                  key: 0,
                  span: i.span || r.value
                }, {
                  default: T(() => [
                    L(me, {
                      label: i.label,
                      prop: i.prop
                    }, {
                      default: T(() => [
                        !i.type || i.type === "text" || i.type === "textarea" || i.type === "password" ? (m(), N(K, be({
                          key: 0,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          type: i.type === "textarea" ? "textarea" : i.type === "password" ? "password" : "text",
                          placeholder: i.placeholder || `请输入${i.label}`,
                          disabled: i.disabled,
                          rows: i.type === "textarea" ? 3 : void 0,
                          autocomplete: i.type === "password" ? "new-password" : "off"
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "type", "placeholder", "disabled", "rows", "autocomplete"])) : i.type === "number" ? (m(), N(ee, be({
                          key: 1,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          placeholder: i.placeholder,
                          disabled: i.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : i.type === "select" ? (m(), N(re, be({
                          key: 2,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          placeholder: i.placeholder || `请选择${i.label}`,
                          multiple: i.multiple,
                          filterable: i.filterable,
                          disabled: i.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), {
                          default: T(() => [
                            (m(!0), R(ae, null, ce(i.options || d[i.prop] || [], (u) => (m(), N(le, {
                              key: u.value,
                              label: u.label,
                              value: u.value,
                              disabled: u.disabled
                            }, null, 8, ["label", "value", "disabled"]))), 128))
                          ]),
                          _: 2
                        }, 1040, ["modelValue", "onUpdate:modelValue", "placeholder", "multiple", "filterable", "disabled"])) : i.type === "radio" ? (m(), N(F, {
                          key: 3,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          disabled: i.disabled
                        }, {
                          default: T(() => [
                            (m(!0), R(ae, null, ce(i.options || [], (u) => (m(), N(se, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: T(() => [
                                V(I(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : i.type === "checkbox" ? (m(), N(q, {
                          key: 4,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          disabled: i.disabled
                        }, {
                          default: T(() => [
                            (m(!0), R(ae, null, ce(i.options || [], (u) => (m(), N(E, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: T(() => [
                                V(I(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : i.type === "switch" ? (m(), N(G, be({
                          key: 5,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          disabled: i.disabled,
                          "active-value": 1,
                          "inactive-value": 0
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "disabled"])) : i.type === "date" ? (m(), N(X, be({
                          key: 6,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          type: "date",
                          placeholder: i.placeholder || `请选择${i.label}`,
                          disabled: i.disabled,
                          "value-format": "YYYY-MM-DD",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : i.type === "datetime" ? (m(), N(X, be({
                          key: 7,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          type: "datetime",
                          placeholder: i.placeholder || `请选择${i.label}`,
                          disabled: i.disabled,
                          "value-format": "YYYY-MM-DD HH:mm:ss",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : i.type === "dateRange" ? (m(), N(X, be({
                          key: 8,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          type: "daterange",
                          placeholder: i.placeholder || `请选择${i.label}`,
                          disabled: i.disabled,
                          "value-format": "YYYY-MM-DD",
                          "range-separator": "至",
                          "start-placeholder": "开始日期",
                          "end-placeholder": "结束日期",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : i.type === "treeSelect" ? (m(), N(te, be({
                          key: 9,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          data: i.options || [],
                          placeholder: i.placeholder || `请选择${i.label}`,
                          disabled: i.disabled,
                          "check-strictly": "",
                          clearable: "",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "data", "placeholder", "disabled"])) : i.type === "cascader" ? (m(), N(Z, be({
                          key: 10,
                          modelValue: c[i.prop],
                          "onUpdate:modelValue": (u) => c[i.prop] = u,
                          options: i.options || [],
                          placeholder: i.placeholder || `请选择${i.label}`,
                          disabled: i.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, i.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "options", "placeholder", "disabled"])) : i.type === "custom" && i.slot ? W(h.$slots, i.slot, {
                          value: c[i.prop],
                          field: i,
                          data: c
                        }, void 0, !0, 11) : W(h.$slots, `field-${i.prop}`, {
                          value: c[i.prop],
                          field: i,
                          data: c
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
          a.showActions ? (m(), R("div", It, [
            W(h.$slots, "actions", {
              submit: w,
              reset: y
            }, () => [
              L(we, { onClick: y }, {
                default: T(() => [
                  V(I(a.resetText), 1)
                ]),
                _: 1
              }),
              L(we, {
                type: "primary",
                loading: a.loading,
                onClick: w
              }, {
                default: T(() => [
                  V(I(a.submitText), 1)
                ]),
                _: 1
              }, 8, ["loading"])
            ], !0)
          ])) : H("", !0)
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
}, Kt = /* @__PURE__ */ he(Ot, [["__scopeId", "data-v-0464ba24"]]), nn = /* @__PURE__ */ ne({
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
    const e = a, t = o, n = j({
      get: () => e.visible,
      set: (c) => t("update:visible", c)
    }), l = j({
      get: () => e.modelValue,
      set: (c) => t("update:modelValue", c)
    }), r = j(() => {
      if (e.title) return e.title;
      const c = e.entityName || "";
      return e.mode === "add" ? c ? `新增${c}` : "新增" : e.mode === "detail" ? c ? `${c}详情` : "详情" : c ? `编辑${c}` : "编辑";
    }), s = j(() => e.submitText ?? (e.mode === "detail" ? "关闭" : "保存"));
    function d() {
      t("submit");
    }
    function f() {
      t("cancel"), t("update:visible", !1);
    }
    return (c, b) => {
      const w = $("el-button"), y = $("el-dialog");
      return m(), N(y, {
        modelValue: n.value,
        "onUpdate:modelValue": b[1] || (b[1] = (p) => n.value = p),
        title: r.value,
        width: a.width,
        "close-on-click-modal": !1,
        "destroy-on-close": a.destroyOnClose,
        onClosed: b[2] || (b[2] = (p) => t("closed"))
      }, {
        footer: T(() => [
          W(c.$slots, "footer", {}, () => [
            L(w, { onClick: f }, {
              default: T(() => [...b[3] || (b[3] = [
                V("取消", -1)
              ])]),
              _: 1
            }),
            L(w, {
              type: "primary",
              loading: a.loading,
              onClick: d
            }, {
              default: T(() => [
                V(I(s.value), 1)
              ]),
              _: 1
            }, 8, ["loading"])
          ])
        ]),
        default: T(() => [
          W(c.$slots, "default", {}, () => [
            W(c.$slots, "prepend"),
            L(Kt, {
              modelValue: l.value,
              "onUpdate:modelValue": b[0] || (b[0] = (p) => l.value = p),
              fields: a.fields,
              loading: a.loading,
              cols: a.cols,
              "label-width": a.labelWidth,
              "show-actions": !1,
              onSubmit: d,
              onReset: f
            }, null, 8, ["modelValue", "fields", "loading", "cols", "label-width"])
          ])
        ]),
        _: 3
      }, 8, ["modelValue", "title", "width", "destroy-on-close"]);
    };
  }
}), jt = { class: "yzh-search-bar" }, Yt = { class: "yzh-search-bar__inner" }, Wt = { class: "yzh-search-bar__fields" }, qt = { class: "yzh-search-bar__field-row" }, Gt = { class: "yzh-search-bar__label" }, Ht = { class: "yzh-search-bar__actions" }, Xt = /* @__PURE__ */ ne({
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
    const e = a, t = o, n = Ce({});
    _e(
      () => e.defaultValues,
      (d) => {
        d && (Object.keys(n).forEach((f) => delete n[f]), Object.assign(n, d));
      },
      { immediate: !0, deep: !0 }
    );
    const l = e.fields.slice(0, e.maxFields);
    function r() {
      const d = {};
      l.forEach((f) => {
        const c = n[f.prop];
        c !== void 0 && c !== "" && !(Array.isArray(c) && c.length === 0) && (d[f.prop] = c);
      }), t("search", d);
    }
    function s() {
      l.forEach((d) => {
        delete n[d.prop];
      }), t("reset");
    }
    return (d, f) => {
      const c = $("el-input"), b = $("el-input-number"), w = $("el-option"), y = $("el-select"), p = $("el-date-picker"), A = $("el-button");
      return m(), R("div", jt, [
        P("div", Yt, [
          P("div", Wt, [
            (m(!0), R(ae, null, ce(ze(l), (h) => (m(), R("div", {
              key: h.prop,
              class: "yzh-search-bar__field"
            }, [
              P("div", qt, [
                P("label", Gt, I(h.label), 1),
                P("div", {
                  class: "yzh-search-bar__input-wrap",
                  style: Ne({ width: a.inputWidth })
                }, [
                  !h.type || h.type === "text" ? (m(), N(c, {
                    key: 0,
                    modelValue: n[h.prop],
                    "onUpdate:modelValue": (x) => n[h.prop] = x,
                    placeholder: h.placeholder || `请输入${h.label}`,
                    clearable: "",
                    onKeyup: Rt(r, ["enter"])
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "number" ? (m(), N(b, {
                    key: 1,
                    modelValue: n[h.prop],
                    "onUpdate:modelValue": (x) => n[h.prop] = x,
                    placeholder: h.placeholder || `请输入${h.label}`
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "select" ? (m(), N(y, {
                    key: 2,
                    modelValue: n[h.prop],
                    "onUpdate:modelValue": (x) => n[h.prop] = x,
                    placeholder: h.placeholder || `请选择${h.label}`,
                    clearable: "",
                    filterable: ""
                  }, {
                    default: T(() => [
                      (m(!0), R(ae, null, ce(h.options || [], (x) => (m(), N(w, {
                        key: x.value,
                        label: x.label,
                        value: x.value
                      }, null, 8, ["label", "value"]))), 128))
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "date" ? (m(), N(p, {
                    key: 3,
                    modelValue: n[h.prop],
                    "onUpdate:modelValue": (x) => n[h.prop] = x,
                    type: "date",
                    placeholder: h.placeholder || `请选择${h.label}`,
                    "value-format": "YYYY-MM-DD"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "dateRange" ? (m(), N(p, {
                    key: 4,
                    modelValue: n[h.prop],
                    "onUpdate:modelValue": (x) => n[h.prop] = x,
                    type: "daterange",
                    placeholder: h.placeholder || `请选择${h.label}`,
                    "value-format": "YYYY-MM-DD",
                    "range-separator": "至",
                    "start-placeholder": "开始日期",
                    "end-placeholder": "结束日期"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : H("", !0)
                ], 4)
              ])
            ]))), 128)),
            f[0] || (f[0] = P("div", { class: "yzh-search-bar__spacer" }, null, -1))
          ]),
          P("div", Ht, [
            L(A, {
              type: "primary",
              onClick: r
            }, {
              default: T(() => [...f[1] || (f[1] = [
                P("i", { class: "bi bi-search" }, null, -1),
                V(" 查询 ", -1)
              ])]),
              _: 1
            }),
            L(A, { onClick: s }, {
              default: T(() => [...f[2] || (f[2] = [
                P("i", { class: "bi bi-arrow-counterclockwise" }, null, -1),
                V(" 重置 ", -1)
              ])]),
              _: 1
            })
          ])
        ])
      ]);
    };
  }
}), Jt = /* @__PURE__ */ he(Xt, [["__scopeId", "data-v-d0360654"]]), Zt = { class: "yzh-toolbar" }, Qt = { class: "yzh-toolbar__left" }, eo = { class: "yzh-toolbar__right" }, to = /* @__PURE__ */ ne({
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
      const r = $("el-button");
      return m(), R("div", Zt, [
        P("div", Qt, [
          (m(!0), R(ae, null, ce(a.buttons.filter((s) => s.group !== "right"), (s) => (m(), N(r, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (d) => t(s)
          }, {
            default: T(() => [
              V(I(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(n.$slots, "left", {}, void 0, !0)
        ]),
        P("div", eo, [
          (m(!0), R(ae, null, ce(a.buttons.filter((s) => s.group === "right"), (s) => (m(), N(r, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (d) => t(s)
          }, {
            default: T(() => [
              V(I(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(n.$slots, "right", {}, void 0, !0)
        ])
      ]);
    };
  }
}), oo = /* @__PURE__ */ he(to, [["__scopeId", "data-v-95dfd97a"]]), ao = /* @__PURE__ */ ne({
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
    const e = a, t = o, n = j({
      get: () => e.page,
      set: (r) => t("update:page", r)
    }), l = j({
      get: () => e.pageSize,
      set: (r) => t("update:pageSize", r)
    });
    return (r, s) => {
      const d = $("el-pagination");
      return m(), N(d, {
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
}), no = /* @__PURE__ */ he(ao, [["__scopeId", "data-v-13879d57"]]), lo = { class: "yzh-page-layout" }, so = {
  key: 0,
  class: "yzh-page-layout__search"
}, ro = {
  key: 1,
  class: "yzh-page-layout__toolbar"
}, io = { class: "yzh-page-layout__toolbar-left" }, co = { class: "yzh-page-layout__toolbar-right" }, uo = {
  key: 2,
  class: "yzh-page-layout__footer"
}, ho = /* @__PURE__ */ ne({
  __name: "YzhPageLayout",
  props: {
    pageTitle: {},
    helpText: {},
    showTitle: { type: Boolean },
    noPadding: { type: Boolean },
    hideToolbar: { type: Boolean }
  },
  setup(a) {
    return (o, e) => (m(), R("div", lo, [
      o.$slots.search ? (m(), R("div", so, [
        W(o.$slots, "search", {}, void 0, !0)
      ])) : H("", !0),
      !a.hideToolbar && (o.$slots.toolbar || o.$slots["toolbar-left"] || o.$slots["toolbar-right"]) ? (m(), R("div", ro, [
        W(o.$slots, "toolbar", {}, () => [
          P("div", io, [
            W(o.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          P("div", co, [
            W(o.$slots, "toolbar-right", {}, void 0, !0)
          ])
        ], !0)
      ])) : H("", !0),
      P("div", {
        class: ke(["yzh-page-layout__content", { "yzh-page-layout__content--no-padding": a.noPadding }])
      }, [
        W(o.$slots, "default", {}, void 0, !0)
      ], 2),
      o.$slots.pagination ? (m(), R("div", uo, [
        W(o.$slots, "pagination", {}, void 0, !0)
      ])) : H("", !0)
    ]));
  }
}), ln = /* @__PURE__ */ he(ho, [["__scopeId", "data-v-756b5466"]]), fo = { class: "yzh-dialog__body" }, po = { class: "yzh-dialog__footer" }, yo = /* @__PURE__ */ ne({
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
    const e = a, t = o, n = j(() => typeof e.width == "number" ? `${e.width}px` : e.width);
    function l() {
      t("update:modelValue", !1), t("close");
    }
    function r() {
      e.confirmDisabled || e.confirmLoading || t("confirm");
    }
    function s() {
      t("cancel"), l();
    }
    return _e(
      () => e.modelValue,
      (d) => {
        d && t("open");
      }
    ), (d, f) => {
      const c = $("el-button"), b = $("el-dialog");
      return m(), N(b, {
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
      }, dt({
        default: T(() => [
          P("div", fo, [
            W(d.$slots, "default", {}, void 0, !0)
          ])
        ]),
        _: 2
      }, [
        a.showFooter ? {
          name: "footer",
          fn: T(() => [
            W(d.$slots, "footer", {
              confirm: r,
              cancel: s
            }, () => [
              P("div", po, [
                L(c, { onClick: s }, {
                  default: T(() => [
                    V(I(a.cancelText), 1)
                  ]),
                  _: 1
                }),
                L(c, {
                  type: a.confirmType,
                  disabled: a.confirmDisabled,
                  loading: a.confirmLoading,
                  onClick: r
                }, {
                  default: T(() => [
                    V(I(a.confirmText), 1)
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
}), sn = /* @__PURE__ */ he(yo, [["__scopeId", "data-v-dbdcca59"]]);
/*! Element Plus Icons Vue v2.3.2 */
var mo = /* @__PURE__ */ ne({
  name: "Document",
  __name: "document",
  setup(a) {
    return (o, e) => (m(), R("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      P("path", {
        fill: "currentColor",
        d: "M832 384H576V128H192v768h640zm-26.496-64L640 154.496V320zM160 64h480l256 256v608a32 32 0 0 1-32 32H160a32 32 0 0 1-32-32V96a32 32 0 0 1 32-32m160 448h384v64H320zm0-192h160v64H320zm0 384h384v64H320z"
      })
    ]));
  }
}), go = mo, vo = /* @__PURE__ */ ne({
  name: "Folder",
  __name: "folder",
  setup(a) {
    return (o, e) => (m(), R("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      P("path", {
        fill: "currentColor",
        d: "M128 192v640h768V320H485.76L357.504 192zm-32-64h287.872l128.384 128H928a32 32 0 0 1 32 32v576a32 32 0 0 1-32 32H96a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32"
      })
    ]));
  }
}), bo = vo;
const Co = { class: "yzh-tree" }, wo = {
  key: 0,
  class: "yzh-tree__search"
}, ko = ["onMouseenter"], To = {
  key: 1,
  class: "yzh-tree__icon"
}, xo = {
  key: 4,
  class: "yzh-tree__badge"
}, So = /* @__PURE__ */ ne({
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
    function t(i) {
      return /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u.test(i);
    }
    function n(i, u) {
      if (!i) return;
      const g = u.charAt(0).toLowerCase() + u.slice(1);
      return i[u] ?? i[g];
    }
    const l = a, r = e, s = k(), d = k(""), f = k(null);
    function c(i) {
      return String(n(i, l.nodeKey) ?? "");
    }
    function b(i) {
      return String(n(i, l.labelField) ?? "");
    }
    function w(i) {
      return n(i, l.childrenField) ?? [];
    }
    function y(i) {
      return n(i, l.isLeafField) === !0;
    }
    function p(i) {
      return n(i, l.extraField) ?? {};
    }
    const A = j(() => ({
      label: l.labelField,
      children: l.childrenField,
      // 必须读叶子字段（后端 TreeControllerBase.FillIsLeafBatch 批量计算）。
      // 读错字段会让末端节点也长出展开箭头并白跑一次 tree/children。
      isLeaf: (i) => y(i),
      disabled: (i) => p(i).disabled ?? !1
    }));
    function h(i, u) {
      return i ? (b(u) || "").toLowerCase().includes(String(i).toLowerCase()) : !0;
    }
    function x(i) {
      return d.value ? (b(i) || "").toLowerCase().includes(d.value.toLowerCase()) : !1;
    }
    let K = null;
    _e(d, (i) => {
      K && clearTimeout(K), K = setTimeout(() => {
        var u;
        (u = s.value) == null || u.filter(i);
      }, 200);
    });
    function ee(i) {
      let u;
      typeof l.nodeActions == "function" ? u = l.nodeActions(i) || [] : u = l.nodeActions;
      const g = Object.entries(l.legacyNodeActions || {}).map(([C, S]) => ({
        key: C,
        text: l.getActionLabel ? l.getActionLabel(C, i) : S
      }));
      return [...u, ...g].filter((C) => C.visible !== !1);
    }
    function le(i) {
      return i.danger ? "yzh-tree__action-danger" : i.type === "warning" ? "yzh-tree__action-toggle" : "";
    }
    function re(i) {
      r("node-click", i);
    }
    function se() {
      if (!s.value) return;
      const i = s.value.getCheckedNodes();
      r("check-change", i);
    }
    function F(i) {
      r("node-expand", i);
    }
    function E(i) {
      r("node-collapse", i);
    }
    function q(i, u) {
      r("node-action", i, u);
    }
    function G() {
      var i;
      return ((i = s.value) == null ? void 0 : i.getCheckedNodes()) ?? [];
    }
    function X(i) {
      var u;
      (u = s.value) == null || u.setCheckedNodes(i);
    }
    function te(i, u) {
      var g;
      (g = s.value) == null || g.setChecked(i, u, !1);
    }
    function Z() {
      const i = (u) => {
        var g;
        for (const C of u) {
          const S = (g = s.value) == null ? void 0 : g.store;
          S && S.nodesMap[c(C)] && (S.nodesMap[c(C)].expanded = !0), w(C).length && i(w(C));
        }
      };
      i(l.data);
    }
    function me() {
      const i = (u) => {
        var g;
        for (const C of u) {
          const S = (g = s.value) == null ? void 0 : g.store;
          S && S.nodesMap[c(C)] && (S.nodesMap[c(C)].expanded = !1), w(C).length && i(w(C));
        }
      };
      i(l.data);
    }
    function fe(i) {
      var u;
      (u = s.value) == null || u.setCurrentKey(i);
    }
    function ye(i, u) {
      var g;
      if (s.value) {
        if (i) {
          try {
            s.value.append(u, i);
            return;
          } catch {
          }
          const C = s.value.store, S = (g = C == null ? void 0 : C.nodesMap) == null ? void 0 : g[i];
          if (S && typeof S.append == "function") {
            S.append(u);
            return;
          }
          if (we(l.data, i, u)) return;
        }
        l.data.push(u);
      }
    }
    function we(i, u, g) {
      for (const C of i) {
        if (c(C) === u) {
          const S = w(C);
          return S.push(g), C[l.childrenField] = S, C[l.isLeafField] = !1, !0;
        }
        if (w(C).length && we(w(C), u, g))
          return !0;
      }
      return !1;
    }
    o({
      getCheckedNodes: G,
      setCheckedNodes: X,
      setChecked: te,
      expandAll: Z,
      collapseAll: me,
      setCurrentNode: fe,
      appendNode: ye,
      /** 从树中移除指定节点（不触发 API，仅更新本地树 UI） */
      removeNode: (i, u) => {
        var S;
        if (!s.value) return;
        try {
          s.value.remove(u);
          return;
        } catch {
        }
        const g = s.value.store, C = (S = g == null ? void 0 : g.nodesMap) == null ? void 0 : S[u];
        if (C && C.parentNode) {
          C.parentNode.remove(C);
          return;
        }
        ie(l.data, u);
      }
    });
    function ie(i, u) {
      for (let g = 0; g < i.length; g++) {
        if (c(i[g]) === u)
          return i.splice(g, 1), !0;
        if (w(i[g]).length && ie(w(i[g]), u))
          return !0;
      }
      return !1;
    }
    return (i, u) => {
      const g = $("el-icon"), C = $("el-button"), S = $("el-dropdown-item"), M = $("el-dropdown-menu"), Q = $("el-dropdown");
      return m(), R("div", Co, [
        a.searchable ? (m(), R("div", wo, [
          L(ze(Qe), {
            modelValue: d.value,
            "onUpdate:modelValue": u[0] || (u[0] = (D) => d.value = D),
            placeholder: a.searchPlaceholder,
            clearable: "",
            "prefix-icon": "Search",
            size: "small"
          }, null, 8, ["modelValue", "placeholder"])
        ])) : H("", !0),
        L(ze(Ut), {
          ref_key: "treeRef",
          ref: s,
          data: a.data,
          props: A.value,
          "show-checkbox": a.showCheckbox,
          "check-strictly": a.checkStrictly,
          lazy: a.lazy,
          load: a.loadData,
          "default-expand-all": a.defaultExpandAll,
          "expand-on-click-node": a.expandOnClickNode,
          "highlight-current": a.highlightCurrent,
          "node-key": a.nodeKey,
          "current-node-key": a.currentKey,
          "filter-node-method": h,
          "empty-text": "暂无数据",
          class: "yzh-tree__inner",
          onNodeClick: re,
          onCheckChange: se,
          onNodeExpand: F,
          onNodeCollapse: E
        }, {
          default: T(({ data: D }) => [
            P("div", {
              class: "yzh-tree__node",
              onMouseenter: (Y) => f.value = c(D),
              onMouseleave: u[2] || (u[2] = (Y) => f.value = null)
            }, [
              p(D).icon && !t(p(D).icon) ? (m(), N(g, {
                key: 0,
                class: "yzh-tree__icon"
              }, {
                default: T(() => [
                  (m(), N(Ie(p(D).icon)))
                ]),
                _: 2
              }, 1024)) : p(D).icon ? (m(), R("span", To, I(p(D).icon), 1)) : y(D) ? (m(), N(g, {
                key: 2,
                class: "yzh-tree__icon yzh-tree__icon--leaf"
              }, {
                default: T(() => [
                  L(ze(go))
                ]),
                _: 1
              })) : (m(), N(g, {
                key: 3,
                class: "yzh-tree__icon yzh-tree__icon--folder"
              }, {
                default: T(() => [
                  L(ze(bo))
                ]),
                _: 1
              })),
              P("span", {
                class: ke(["yzh-tree__label", { "is-highlight": a.highlightKeyword && x(D) }])
              }, I(b(D)), 3),
              p(D).badge ? (m(), R("span", xo, I(p(D).badge), 1)) : H("", !0),
              ee(D).length ? (m(), N(Q, {
                key: 5,
                trigger: "click",
                onCommand: (Y) => q(Y, D),
                onClick: u[1] || (u[1] = Bt(() => {
                }, ["stop"]))
              }, {
                dropdown: T(() => [
                  L(M, null, {
                    default: T(() => [
                      (m(!0), R(ae, null, ce(ee(D), (Y) => (m(), N(S, {
                        key: Y.key,
                        command: Y.key,
                        disabled: Y.disabled,
                        class: ke(le(Y))
                      }, {
                        default: T(() => [
                          V(I(Y.text), 1)
                        ]),
                        _: 2
                      }, 1032, ["command", "disabled", "class"]))), 128))
                    ]),
                    _: 2
                  }, 1024)
                ]),
                default: T(() => [
                  L(C, {
                    link: "",
                    size: "small",
                    class: "yzh-tree__more-btn"
                  }, {
                    default: T(() => [...u[3] || (u[3] = [
                      V(" ⋯ ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                _: 2
              }, 1032, ["onCommand"])) : H("", !0)
            ], 40, ko)
          ]),
          _: 1
        }, 8, ["data", "props", "show-checkbox", "check-strictly", "lazy", "load", "default-expand-all", "expand-on-click-node", "highlight-current", "node-key", "current-node-key"])
      ]);
    };
  }
}), ct = /* @__PURE__ */ he(So, [["__scopeId", "data-v-ef58d20f"]]), _o = { class: "yzh-tree-table" }, Ao = { class: "yzh-tree-table__main" }, Fo = {
  key: 0,
  class: "yzh-tree-table__tree-toolbar"
}, zo = {
  key: 1,
  class: "yzh-tree-table__tree-footer"
}, No = { class: "yzh-tree-table__table-panel" }, Do = /* @__PURE__ */ ne({
  __name: "YzhTreeTableLayout",
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
    const t = a, n = e, l = k(), r = k(""), s = j(() => r.value ? y(t.treeData, r.value) : t.treeData);
    function d(p) {
      n("tree-node-click", p);
    }
    function f(p) {
      n("tree-check-change", p);
    }
    function c(p, A) {
      n("tree-node-action", p, A);
    }
    function b() {
      var p;
      (p = l.value) == null || p.expandAll();
    }
    function w() {
      var p;
      (p = l.value) == null || p.collapseAll();
    }
    function y(p, A) {
      const h = A.toLowerCase(), x = [];
      for (const K of p) {
        const le = String(K[t.labelField] ?? "").toLowerCase().includes(h), re = K[t.childrenField] ?? [], se = y(re, A);
        (le || se.length > 0) && x.push({ ...K, [t.childrenField]: se });
      }
      return x;
    }
    return o({
      treeRef: l,
      getCheckedNodes: () => {
        var p;
        return ((p = l.value) == null ? void 0 : p.getCheckedNodes()) ?? [];
      },
      expandAll: b,
      collapseAll: w,
      appendNode: (p, A) => {
        var h;
        return (h = l.value) == null ? void 0 : h.appendNode(p, A);
      },
      removeNode: (p, A) => {
        var h;
        return (h = l.value) == null ? void 0 : h.removeNode(p, A);
      }
    }), (p, A) => (m(), R("div", _o, [
      P("div", Ao, [
        P("div", {
          class: "yzh-tree-table__tree-panel",
          style: Ne({ width: a.treeWidth + "px" })
        }, [
          a.treeToolbar ? (m(), R("div", Fo, [
            a.treeSearchable ? (m(), N(ze(Qe), {
              key: 0,
              modelValue: r.value,
              "onUpdate:modelValue": A[0] || (A[0] = (h) => r.value = h),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue"])) : H("", !0)
          ])) : H("", !0),
          L(ct, {
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
            onNodeClick: d,
            onCheckChange: f,
            onNodeAction: c
          }, null, 8, ["data", "node-key", "label-field", "children-field", "is-leaf-field", "extra-field", "show-checkbox", "check-strictly", "lazy", "load-data", "default-expand-all", "node-actions", "legacy-node-actions", "get-action-label"]),
          p.$slots.treeFooter ? (m(), R("div", zo, [
            W(p.$slots, "treeFooter", {}, void 0, !0)
          ])) : H("", !0)
        ], 4),
        P("div", No, [
          W(p.$slots, "default", {}, void 0, !0)
        ])
      ])
    ]));
  }
}), rn = /* @__PURE__ */ he(Do, [["__scopeId", "data-v-d15619e1"]]), $o = { class: "yzh-table" }, Ro = { class: "yzh-column-settings" }, Bo = { class: "yzh-column-settings__body" }, Po = { class: "yzh-column-settings__footer" }, Vo = { key: 1 }, Lo = { class: "yzh-table__empty" }, Eo = {
  key: 1,
  class: "yzh-table__error"
}, Mo = {
  key: 2,
  class: "yzh-table__pagination"
}, Uo = /* @__PURE__ */ ne({
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
    actionMaxInline: { default: 0 },
    defaultExpandAll: { type: Boolean, default: !1 },
    treeProps: { default: void 0 }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action", "expand-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(!1), r = k(""), s = k([]), d = k(0), f = k([]), c = k(1), b = k(t.pageSize), w = k(t.defaultSort || null), y = Ce({}), p = k(/* @__PURE__ */ new Set()), A = j(() => t.selectMode ? t.selectMode : t.selectable ? "multiple" : "none"), h = j(() => A.value === "multiple"), x = j(
      () => t.columns.filter((v) => v.label && v.prop !== "__yzh_action")
    ), K = j(
      () => t.columns.filter((v) => !(v.hidden || p.value.has(v.prop)))
    );
    function ee(v) {
      return Object.entries(v).map(([_, U]) => ({ key: _, text: U }));
    }
    function le(v) {
      const _ = typeof t.rowActionButtons == "function" ? t.rowActionButtons(v) : t.rowActionButtons;
      return (Array.isArray(_) ? _ : ee(_ || {})).filter((oe) => oe.visible !== !1);
    }
    const re = j(() => {
      const v = t.columns.some((U) => U.prop === "actions");
      return (typeof t.rowActionButtons == "function" || (Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length) > 0) && !v;
    }), se = j(() => t.actionMaxInline > 0);
    function F(v) {
      return !se.value || v.length <= t.actionMaxInline ? { inline: v, overflow: [] } : { inline: v.slice(0, t.actionMaxInline), overflow: v.slice(t.actionMaxInline) };
    }
    const E = j(
      () => t.toolbarActions.filter((v) => v.visible !== !1)
    );
    async function q(v, _) {
      if (!v.disabled) {
        if (v.confirm)
          try {
            await Se.confirm(v.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        n("row-action", v.key, _, v);
      }
    }
    async function G(v) {
      if (!v.disabled) {
        if (v.confirm)
          try {
            await Se.confirm(v.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        n("toolbar-action", v.key, v);
      }
    }
    function X(v, _) {
      _ ? p.value.delete(v.prop) : p.value.add(v.prop), p.value = new Set(p.value);
    }
    function te(v) {
      if (v.sortable === !1) return;
      const _ = v.prop;
      w.value && w.value.prop === _ ? w.value = { ...w.value, order: w.value.order === "asc" ? "desc" : "asc" } : w.value = { prop: _, order: "asc" };
    }
    function Z(v) {
      const _ = v.prop;
      return !w.value || w.value.prop !== _ ? "排序" : w.value.order === "asc" ? "↑ 升序" : "↓ 降序";
    }
    function me() {
      p.value = /* @__PURE__ */ new Set(), w.value = t.defaultSort || null;
    }
    function fe() {
      ie();
    }
    const ye = j(() => t.toolbar === !1 ? {} : t.toolbar === !0 ? { columnSetting: !0 } : t.toolbar), we = j(() => Object.keys(ye.value).length > 0 || E.value.length > 0);
    async function ie() {
      l.value = !0, r.value = "";
      try {
        const v = new Set(f.value.map((oe) => oe[t.rowKey])), _ = {
          page: c.value,
          rows: b.value,
          ...w.value ? { sort: w.value.prop, order: w.value.order } : {},
          ...y
        }, U = await t.dataLoader(_);
        if (s.value = U.rows || [], d.value = U.total || 0, v.size > 0) {
          const oe = [];
          for (const Le of s.value)
            v.has(Le[t.rowKey]) && oe.push(Le);
          f.value = oe;
        }
      } catch (v) {
        r.value = (v == null ? void 0 : v.message) || "数据加载失败", s.value = [], d.value = 0, J.error(r.value);
      } finally {
        l.value = !1;
      }
    }
    function i({ prop: v, order: _ }) {
      _ ? w.value = {
        prop: v,
        order: _ === "ascending" ? "asc" : "desc"
      } : w.value = null, ie();
    }
    function u(v) {
      c.value = v, ie();
    }
    function g(v) {
      b.value = v, c.value = 1, ie();
    }
    function C(v) {
      Object.assign(y, v), c.value = 1, ie();
    }
    function S() {
      Object.keys(y).forEach((v) => delete y[v]), t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((v) => {
        v.defaultValue !== void 0 && (y[v.prop] = v.defaultValue);
      }), c.value = 1, ie();
    }
    function M(v) {
      f.value = v, n("selection-change", v);
    }
    function Q(v, _) {
      n("row-click", v, _);
    }
    Pt();
    let D = !1;
    const Y = j(() => {
      if (typeof t.rowActionButtons == "function")
        return 4 * 70 + 40;
      const v = Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length;
      return v > 0 ? v * 70 + 40 : 140;
    });
    _e(
      () => typeof t.rowActionButtons == "function" ? 1 : Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length,
      (v) => {
      },
      { immediate: !0 }
    );
    function ge() {
      c.value = 1, ie(), n("refresh");
    }
    Pe(() => {
      t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((v) => {
        v.defaultValue !== void 0 && (y[v.prop] = v.defaultValue);
      }), ie();
    });
    function $e(v, _ = "top") {
      _ === "top" ? s.value.unshift(v) : s.value.push(v), d.value++;
    }
    function de(v, _) {
      const U = s.value.findIndex((oe) => v(oe));
      U >= 0 && s.value.splice(U, 1, _);
    }
    function Fe(v) {
      const _ = s.value.findIndex((U) => v(U));
      _ >= 0 && (s.value.splice(_, 1), d.value = Math.max(0, d.value - 1));
    }
    function Te() {
      return s.value.length;
    }
    function Ve(v, _) {
      if (_) {
        const U = new Set(f.value);
        for (const oe of s.value)
          v(oe) && !U.has(oe) && f.value.push(oe);
      } else
        f.value = f.value.filter((U) => !v(U));
      n("selection-change", [...f.value]);
    }
    const Ye = k(), lt = j(() => {
      var v;
      return ((v = t.treeProps) == null ? void 0 : v.children) || "children";
    }), wt = j(() => {
      var v;
      return {
        children: lt.value,
        hasChildren: ((v = t.treeProps) == null ? void 0 : v.hasChildren) || "hasChildren",
        ...t.treeProps
      };
    });
    function We(v, _) {
      for (const U of v) {
        _(U);
        const oe = U[lt.value];
        Array.isArray(oe) && oe.length && We(oe, _);
      }
    }
    function kt() {
      We(s.value, (v) => {
        var _, U;
        return (U = (_ = Ye.value) == null ? void 0 : _.toggleRowExpansion) == null ? void 0 : U.call(_, v, !0);
      });
    }
    function Tt() {
      We(s.value, (v) => {
        var _, U;
        return (U = (_ = Ye.value) == null ? void 0 : _.toggleRowExpansion) == null ? void 0 : U.call(_, v, !1);
      });
    }
    function xt(v, _) {
      n("expand-change", v, _);
    }
    return o({
      refresh: ge,
      loadData: ie,
      insertRow: $e,
      replaceRow: de,
      removeRow: Fe,
      getRowCount: Te,
      getSelectedRows: () => f.value,
      setCheckedRows: Ve,
      clearSelection: () => {
        f.value = [], n("selection-change", []);
      },
      expandAll: kt,
      collapseAll: Tt
    }), (v, _) => {
      const U = $("el-button"), oe = $("el-checkbox"), Le = $("el-popover"), qe = $("el-table-column"), st = $("el-tag"), St = $("el-dropdown-item"), _t = $("el-dropdown-menu"), At = $("el-dropdown"), Ft = $("el-empty"), zt = $("el-table"), Nt = Vt("loading");
      return m(), R("div", $o, [
        a.searchFields && a.searchFields.length ? (m(), N(Jt, {
          key: 0,
          fields: a.searchFields,
          "default-values": y,
          cols: 2,
          "max-fields": a.searchMaxFields,
          onSearch: C,
          onReset: S
        }, null, 8, ["fields", "default-values", "max-fields"])) : H("", !0),
        we.value ? (m(), N(oo, {
          key: 1,
          buttons: E.value,
          onAction: _[0] || (_[0] = (B, O) => G(O))
        }, {
          left: T(() => [
            W(v.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          right: T(() => [
            W(v.$slots, "toolbar-right", {
              selected: f.value,
              refresh: ge
            }, () => [
              ye.value.columnSetting ? (m(), N(Le, {
                key: 0,
                trigger: "click",
                placement: "bottom-end",
                width: 200
              }, {
                reference: T(() => [
                  L(U, { text: "" }, {
                    default: T(() => [..._[1] || (_[1] = [
                      P("i", { class: "bi bi-columns" }, null, -1),
                      V(" 列设置 ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                default: T(() => [
                  P("div", Ro, [
                    _[4] || (_[4] = P("div", { class: "yzh-column-settings__header" }, "列筛选与排序", -1)),
                    P("div", Bo, [
                      (m(!0), R(ae, null, ce(x.value, (B) => {
                        var O;
                        return m(), R("div", {
                          key: B.prop,
                          class: "yzh-column-settings__item"
                        }, [
                          L(oe, {
                            "model-value": !p.value.has(B.prop) && !B.hidden,
                            onChange: (ve) => X(B, ve)
                          }, {
                            default: T(() => [
                              V(I(B.label), 1)
                            ]),
                            _: 2
                          }, 1032, ["model-value", "onChange"]),
                          L(U, {
                            class: ke(["yzh-column-settings__sort-btn", { "is-active": ((O = w.value) == null ? void 0 : O.prop) === B.prop }]),
                            disabled: B.sortable === !1,
                            onClick: (ve) => te(B)
                          }, {
                            default: T(() => [
                              V(I(Z(B)), 1)
                            ]),
                            _: 2
                          }, 1032, ["class", "disabled", "onClick"])
                        ]);
                      }), 128))
                    ]),
                    P("div", Po, [
                      L(U, {
                        size: "small",
                        onClick: me
                      }, {
                        default: T(() => [..._[2] || (_[2] = [
                          V("重置", -1)
                        ])]),
                        _: 1
                      }),
                      L(U, {
                        size: "small",
                        type: "primary",
                        onClick: fe
                      }, {
                        default: T(() => [..._[3] || (_[3] = [
                          V("确定", -1)
                        ])]),
                        _: 1
                      })
                    ])
                  ])
                ]),
                _: 1
              })) : H("", !0)
            ], !0)
          ]),
          _: 3
        }, 8, ["buttons"])) : H("", !0),
        P("div", {
          class: ke(["yzh-table__wrapper", { "yzh-table__wrapper--no-padding": a.noPadding }])
        }, [
          P("div", {
            class: "yzh-table__body",
            style: Ne(a.height ? { height: typeof a.height == "number" ? a.height + "px" : a.height } : {})
          }, [
            Lt((m(), N(zt, {
              ref_key: "tableRef",
              ref: Ye,
              data: s.value,
              "row-key": a.rowKey,
              "default-expand-all": a.defaultExpandAll,
              "tree-props": wt.value,
              height: a.height !== void 0 && a.height !== null ? a.height : "100%",
              "highlight-current-row": A.value === "single",
              stripe: "",
              border: "",
              onSelectionChange: M,
              onSortChange: i,
              onRowClick: Q,
              onExpandChange: xt
            }, {
              empty: T(() => [
                P("div", Lo, [
                  !l.value && !r.value ? (m(), N(Ft, {
                    key: 0,
                    description: a.emptyText
                  }, null, 8, ["description"])) : r.value ? (m(), R("div", Eo, [
                    _[7] || (_[7] = P("i", { class: "bi bi-exclamation-triangle" }, null, -1)),
                    P("span", null, I(r.value), 1),
                    L(U, {
                      text: "",
                      type: "primary",
                      onClick: ge
                    }, {
                      default: T(() => [..._[6] || (_[6] = [
                        V("重试", -1)
                      ])]),
                      _: 1
                    })
                  ])) : H("", !0)
                ])
              ]),
              default: T(() => [
                h.value ? (m(), N(qe, {
                  key: 0,
                  type: "selection",
                  width: "48",
                  "reserve-selection": !1
                })) : H("", !0),
                (m(!0), R(ae, null, ce(K.value, (B) => (m(), N(qe, {
                  key: B.prop,
                  prop: B.prop,
                  label: B.label,
                  width: B.width,
                  "min-width": B.minWidth,
                  fixed: B.fixed,
                  sortable: B.sortable,
                  align: B.align || "left",
                  "show-overflow-tooltip": !B.slot,
                  "class-name": B.className
                }, {
                  default: T(({ row: O, $index: ve }) => {
                    var Ee;
                    return [
                      B.slot ? W(v.$slots, `column-${String(B.prop)}`, {
                        row: O,
                        index: ve,
                        value: O[B.prop]
                      }, () => [
                        V(I(B.formatter ? B.formatter(O[B.prop], O, ve) : O[B.prop]), 1)
                      ], !0, 0) : B.dictCode ? (m(), R(ae, { key: 1 }, [
                        B.tagType ? (m(), N(st, {
                          key: 0,
                          type: B.tagType,
                          "disable-transitions": ""
                        }, {
                          default: T(() => [
                            V(I(O[B.prop]), 1)
                          ]),
                          _: 2
                        }, 1032, ["type"])) : (m(), R("span", Vo, I(O[B.prop]), 1))
                      ], 64)) : B.tagMap ? (m(), N(st, {
                        key: 2,
                        type: ((Ee = B.tagTypeMap) == null ? void 0 : Ee[O[B.prop]]) ?? "info",
                        size: "small",
                        "disable-transitions": ""
                      }, {
                        default: T(() => [
                          V(I(B.tagMap[O[B.prop]] ?? O[B.prop]), 1)
                        ]),
                        _: 2
                      }, 1032, ["type"])) : (m(), R(ae, { key: 3 }, [
                        V(I(B.formatter ? B.formatter(O[B.prop], O, ve) : O[B.prop]), 1)
                      ], 64))
                    ];
                  }),
                  _: 2
                }, 1032, ["prop", "label", "width", "min-width", "fixed", "sortable", "align", "show-overflow-tooltip", "class-name"]))), 128)),
                re.value ? (m(), N(qe, {
                  key: 1,
                  label: "操作",
                  width: Y.value,
                  fixed: "right",
                  align: "center"
                }, {
                  default: T(({ row: B }) => [
                    (m(!0), R(ae, null, ce(F(le(B)).inline, (O) => (m(), N(U, {
                      key: O.key,
                      link: a.rowActionLink,
                      size: "small",
                      type: O.type ?? "primary",
                      disabled: O.disabled,
                      onClick: (ve) => q(O, B)
                    }, {
                      default: T(() => [
                        V(I(O.text), 1)
                      ]),
                      _: 2
                    }, 1032, ["link", "type", "disabled", "onClick"]))), 128)),
                    F(le(B)).overflow.length > 0 ? (m(), N(At, {
                      key: 0,
                      trigger: "click",
                      onCommand: (O) => {
                        const ve = F(le(B)).overflow.find((Ee) => Ee.key === O);
                        ve && q(ve, B);
                      }
                    }, {
                      dropdown: T(() => [
                        L(_t, null, {
                          default: T(() => [
                            (m(!0), R(ae, null, ce(F(le(B)).overflow, (O) => (m(), N(St, {
                              key: O.key,
                              command: O.key,
                              disabled: O.disabled,
                              class: ke({ "yzh-row-action-danger": O.type === "danger" })
                            }, {
                              default: T(() => [
                                V(I(O.text), 1)
                              ]),
                              _: 2
                            }, 1032, ["command", "disabled", "class"]))), 128))
                          ]),
                          _: 2
                        }, 1024)
                      ]),
                      default: T(() => [
                        L(U, {
                          link: "",
                          size: "small"
                        }, {
                          default: T(() => [..._[5] || (_[5] = [
                            V("更多", -1)
                          ])]),
                          _: 1
                        })
                      ]),
                      _: 2
                    }, 1032, ["onCommand"])) : H("", !0)
                  ]),
                  _: 1
                }, 8, ["width"])) : H("", !0)
              ]),
              _: 3
            }, 8, ["data", "row-key", "default-expand-all", "tree-props", "height", "highlight-current-row"])), [
              [Nt, l.value]
            ])
          ], 4)
        ], 2),
        a.showPagination ? (m(), R("div", Mo, [
          L(no, {
            page: c.value,
            "page-size": b.value,
            total: d.value,
            "onUpdate:page": u,
            "onUpdate:pageSize": g
          }, null, 8, ["page", "page-size", "total"])
        ])) : H("", !0)
      ]);
    };
  }
}), ut = /* @__PURE__ */ he(Uo, [["__scopeId", "data-v-38f64d87"]]), Io = { class: "yzh-tree-table-selector" }, Oo = {
  key: 0,
  class: "yzh-tree-table-selector__tree-search"
}, Ko = { class: "yzh-tree-table-selector__tree-actions" }, jo = {
  key: 1,
  class: "yzh-tree-table-selector__tree-footer"
}, Yo = { class: "yzh-tree-table-selector__table-panel" }, Wo = { class: "yzh-tree-table-selector__table-toolbar" }, qo = { class: "yzh-tree-table-selector__selection-info" }, Go = /* @__PURE__ */ ne({
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
    const t = a, n = e, l = k(), r = k(), s = k(""), d = k([]), f = k([]), c = k(/* @__PURE__ */ new Map()), b = j(() => s.value ? se(t.treeData, s.value) : t.treeData);
    function w() {
      var F;
      (F = l.value) == null || F.expandAll();
    }
    function y() {
      var F;
      (F = l.value) == null || F.collapseAll();
    }
    function p() {
      const F = (E) => {
        var q;
        for (const G of E)
          (q = l.value) == null || q.setChecked(G.Code, !0), G.Children && G.Children.length > 0 && F(G.Children);
      };
      F(t.treeData);
    }
    function A() {
      var F;
      (F = l.value) == null || F.setCheckedNodes([]);
    }
    function h(F) {
      K(F.Code);
    }
    async function x() {
      if (!l.value) return;
      const F = l.value.getCheckedNodes();
      d.value = F;
      const E = F.map((te) => te.Code), q = [];
      for (const te of E) {
        const Z = await K(te);
        Z && q.push(...Z);
      }
      const G = /* @__PURE__ */ new Set(), X = q.filter((te) => {
        const Z = te[t.rowKey];
        return G.has(Z) ? !1 : (G.add(Z), !0);
      });
      f.value = X, r.value && r.value.setCheckedRows(
        (te) => X.some((Z) => Z[t.rowKey] === te[t.rowKey]),
        !0
      ), n("update:checkedTreeNodes", F), n("update:checkedTableRows", X), n("tree-check-change", F);
    }
    async function K(F) {
      if (c.value.has(F))
        return c.value.get(F);
      try {
        const q = (await t.loadTableData(F)).rows ?? [];
        return c.value.set(F, q), q;
      } catch (E) {
        return J.error(E.message || "加载表格数据失败"), null;
      }
    }
    async function ee(F) {
      if (d.value.length === 0)
        return { rows: [], total: 0 };
      const E = [];
      for (const me of d.value) {
        const fe = await K(me.Code);
        fe && E.push(...fe);
      }
      const q = /* @__PURE__ */ new Set(), G = E.filter((me) => {
        const fe = me[t.rowKey];
        return q.has(fe) ? !1 : (q.add(fe), !0);
      }), X = (F.page - 1) * F.rows, te = X + F.rows;
      return { rows: G.slice(X, te), total: G.length };
    }
    function le(F) {
      f.value = F, n("update:checkedTableRows", F), n("selection-change", F);
    }
    function re() {
      var F;
      (F = r.value) == null || F.clearSelection(), A(), d.value = [], f.value = [], c.value.clear(), n("update:checkedTreeNodes", []), n("update:checkedTableRows", []);
    }
    function se(F, E) {
      const q = E.toLowerCase(), G = [];
      for (const X of F) {
        const te = (X.Name || "").toLowerCase().includes(q), Z = se(X.Children ?? [], E);
        (te || Z.length > 0) && G.push({ ...X, Children: Z });
      }
      return G;
    }
    return o({
      getCheckedTreeNodes: () => d.value,
      getCheckedTableRows: () => f.value,
      clearSelection: re,
      refreshTable: () => {
        var F;
        return (F = r.value) == null ? void 0 : F.refresh();
      }
    }), (F, E) => {
      const q = $("el-input"), G = $("el-button");
      return m(), R("div", Io, [
        P("div", {
          class: "yzh-tree-table-selector__tree-panel",
          style: Ne({ width: a.treeWidth + "px" })
        }, [
          a.treeSearchable ? (m(), R("div", Oo, [
            L(q, {
              modelValue: s.value,
              "onUpdate:modelValue": E[0] || (E[0] = (X) => s.value = X),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search",
              size: "small"
            }, null, 8, ["modelValue"])
          ])) : H("", !0),
          P("div", Ko, [
            L(G, {
              size: "small",
              onClick: w
            }, {
              default: T(() => [...E[1] || (E[1] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: y
            }, {
              default: T(() => [...E[2] || (E[2] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: p
            }, {
              default: T(() => [...E[3] || (E[3] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: A
            }, {
              default: T(() => [...E[4] || (E[4] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ]),
          L(ct, {
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
            onNodeClick: h
          }, null, 8, ["data", "check-strictly", "lazy", "load-data", "default-expand-all", "node-key"]),
          F.$slots.treeFooter ? (m(), R("div", jo, [
            W(F.$slots, "treeFooter", {}, void 0, !0)
          ])) : H("", !0)
        ], 4),
        P("div", Yo, [
          P("div", Wo, [
            P("div", qo, [
              E[5] || (E[5] = V(" 已选择 ", -1)),
              P("strong", null, I(f.value.length), 1),
              E[6] || (E[6] = V(" 条记录 ", -1))
            ]),
            L(G, {
              size: "small",
              type: "danger",
              onClick: re,
              disabled: f.value.length === 0
            }, {
              default: T(() => [...E[7] || (E[7] = [
                V(" 清空选择 ", -1)
              ])]),
              _: 1
            }, 8, ["disabled"])
          ]),
          L(ut, {
            ref_key: "tableRef",
            ref: r,
            columns: a.tableColumns,
            "data-loader": ee,
            selectable: !0,
            "show-pagination": a.showPagination,
            "page-size": a.pageSize,
            "row-key": a.rowKey,
            onSelectionChange: le
          }, null, 8, ["columns", "show-pagination", "page-size", "row-key"])
        ])
      ]);
    };
  }
}), dn = /* @__PURE__ */ he(Go, [["__scopeId", "data-v-8e5e846d"]]), Ho = { class: "yzh-tree-table-check-selector" }, Xo = { class: "yzh-tree-table-check-selector__toolbar" }, Jo = { class: "yzh-tree-table-check-selector__selection-info" }, Zo = {
  key: 0,
  class: "yzh-tree-table-check-selector__search"
}, Qo = { class: "yzh-tree-table-check-selector__toolbar-actions" }, ea = /* @__PURE__ */ ne({
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
    function n(u) {
      var g;
      return ((g = t.typeLabels) == null ? void 0 : g[u]) ?? u;
    }
    function l(u) {
      var g;
      return ((g = t.typeTagTypes) == null ? void 0 : g[u]) ?? "info";
    }
    const r = e, s = k(), d = k([]), f = k(""), c = k(/* @__PURE__ */ new Set()), b = k(/* @__PURE__ */ new Map()), w = k(/* @__PURE__ */ new Set()), y = k(!1), p = k(/* @__PURE__ */ new Set()), A = j(() => {
      var g;
      if (!t.countType) return c.value.size;
      let u = 0;
      for (const C of c.value)
        ((g = b.value.get(C)) == null ? void 0 : g[t.nodeTypeField]) === t.countType && u++;
      return u;
    }), h = j(() => {
      var M, Q;
      const u = f.value.trim().toLowerCase();
      if (!u) return t.flatData;
      const g = /* @__PURE__ */ new Set();
      for (const D of t.flatData)
        t.searchFields.some(
          (ge) => String(D[ge] ?? "").toLowerCase().includes(u)
        ) && g.add(String(D[t.nodeKey]));
      const C = new Map(t.flatData.map((D) => [String(D[t.nodeKey]), D])), S = new Set(g);
      for (const D of g) {
        let Y = (M = C.get(D)) == null ? void 0 : M[t.parentKey];
        for (; Y && !S.has(String(Y)); )
          S.add(String(Y)), Y = (Q = C.get(String(Y))) == null ? void 0 : Q[t.parentKey];
      }
      return t.flatData.filter((D) => S.has(String(D[t.nodeKey])));
    });
    function x(u) {
      const g = /* @__PURE__ */ new Map(), C = [];
      for (const S of u) {
        const M = {
          ...S,
          children: []
        };
        g.set(S[t.nodeKey], M), b.value.set(S[t.nodeKey], M);
      }
      for (const S of u) {
        const M = g.get(S[t.nodeKey]), Q = S[t.parentKey];
        Q && g.has(Q) ? g.get(Q).children.push(M) : C.push(M);
      }
      return C;
    }
    function K(u) {
      const g = /* @__PURE__ */ new Set();
      function C(S) {
        for (const M of S)
          M[t.checkField] && g.add(M[t.nodeKey]), M.children && M.children.length > 0 && C(M.children);
      }
      C(u), c.value = g;
    }
    function ee() {
      if (!s.value) return;
      y.value = !0, s.value.clearSelection();
      const u = /* @__PURE__ */ new Set();
      for (const g of c.value) {
        const C = b.value.get(g);
        C && (s.value.toggleRowSelection(C, !0), u.add(g));
      }
      p.value = u, xe(() => {
        y.value = !1;
      });
    }
    function le(u, g) {
      if (y.value = !0, b.value.clear(), !u || u.length === 0) {
        d.value = [], g && (c.value = /* @__PURE__ */ new Set()), xe(() => {
          ee(), re();
        });
        return;
      }
      d.value = x(u), g && K(d.value), t.defaultExpandAll && (w.value.clear(), se(d.value)), xe(() => {
        ee(), re();
      });
    }
    function re() {
      xe(() => {
        y.value = !1;
      });
    }
    _e(
      () => t.flatData,
      (u) => {
        le(u, !0);
      },
      { immediate: !0 }
    ), _e(f, () => {
      le(h.value, !1);
    });
    function se(u) {
      for (const g of u)
        g.children && g.children.length > 0 && (w.value.add(g[t.nodeKey]), se(g.children));
    }
    function F() {
      se(d.value);
    }
    function E() {
      w.value.clear(), y.value = !0;
      const u = d.value;
      d.value = [], xe(() => {
        d.value = u, re();
      });
    }
    function q() {
      if (!s.value) return;
      y.value = !0;
      const u = X(d.value), g = new Set(c.value);
      for (const C of u)
        g.add(C[t.nodeKey]), s.value.toggleRowSelection(C, !0);
      c.value = g, p.value = new Set(g), y.value = !1, ye([], u.map((C) => C[t.nodeKey]));
    }
    function G() {
      if (!s.value) return;
      y.value = !0;
      const u = Array.from(c.value);
      c.value = /* @__PURE__ */ new Set(), p.value = /* @__PURE__ */ new Set(), s.value.clearSelection(), y.value = !1, ye(u, []);
    }
    function X(u) {
      const g = t.checkAllExcludeTypes ?? [], C = [];
      for (const S of u)
        g.includes(S[t.nodeTypeField]) || C.push(S), S.children && S.children.length > 0 && C.push(...X(S.children));
      return C;
    }
    function te(u) {
      const g = [], C = (S) => {
        var M;
        for (const Q of S)
          g.push(Q), (M = Q.children) != null && M.length && C(Q.children);
      };
      return C(u.children ?? []), g;
    }
    function Z(u, g) {
      const C = [];
      for (const S of g) u.has(S) || C.push(S);
      return C;
    }
    function me(u, g, C) {
      const S = new Set(C), M = (de, Fe) => {
        var Ve;
        const Te = String(de[t.nodeKey]);
        Fe ? S.add(Te) : S.delete(Te), (Ve = s.value) == null || Ve.toggleRowSelection(de, Fe);
      };
      y.value = !0, M(u, g);
      for (const de of te(u)) M(de, g);
      const Q = t.checkAllExcludeTypes ?? [];
      let D = u[t.parentKey];
      for (; D; ) {
        const de = b.value.get(String(D));
        if (!de) break;
        const Fe = de.children.filter(
          (Te) => !Q.includes(String(Te[t.nodeTypeField]))
        );
        M(de, Fe.length > 0 && Fe.every((Te) => S.has(String(Te[t.nodeKey])))), D = de[t.parentKey];
      }
      y.value = !1;
      const Y = Z(S, C), ge = Z(C, S), $e = new Set(c.value);
      for (const de of ge) $e.add(de);
      for (const de of Y) $e.delete(de);
      c.value = $e, p.value = new Set(S), ye(Y, ge);
    }
    function fe(u) {
      if (y.value) return;
      const g = new Set(u.map((D) => String(D[t.nodeKey]))), C = p.value, S = Z(C, g), M = Z(g, C);
      if (S.length === 0 && M.length === 0) return;
      if (t.cascade) {
        const Y = S.length + M.length === 1 ? S[0] ?? M[0] : void 0, ge = Y ? b.value.get(Y) : void 0;
        if (ge) {
          me(ge, S.length > 0, C);
          return;
        }
      }
      p.value = g;
      const Q = new Set(c.value);
      for (const D of S) Q.add(D);
      for (const D of M) Q.delete(D);
      c.value = Q, S.length > 0 && ye([], S), M.length > 0 && ye(M, []);
    }
    function ye(u, g) {
      r("check-change", { added: g, removed: u });
    }
    function we() {
      return Array.from(c.value);
    }
    function ie(u) {
      c.value = new Set(u), xe(() => {
        ee();
      });
    }
    function i() {
      const u = [];
      for (const g of c.value) {
        const C = b.value.get(g);
        C && u.push(C);
      }
      return u;
    }
    return o({
      getCheckedKeys: we,
      setCheckedKeys: ie,
      getCheckedNodes: i,
      expandAll: F,
      collapseAll: E,
      checkAll: q,
      uncheckAll: G
    }), (u, g) => {
      const C = $("el-button"), S = $("el-table-column"), M = $("el-tag"), Q = $("el-table");
      return m(), R("div", Ho, [
        P("div", Xo, [
          P("div", Jo, [
            g[1] || (g[1] = V(" 已选择 ", -1)),
            P("strong", null, I(A.value), 1),
            g[2] || (g[2] = V(" 条记录 ", -1))
          ]),
          a.searchable ? (m(), R("div", Zo, [
            L(ze(Qe), {
              modelValue: f.value,
              "onUpdate:modelValue": g[0] || (g[0] = (D) => f.value = D),
              placeholder: a.searchPlaceholder,
              clearable: "",
              size: "small",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue", "placeholder"])
          ])) : H("", !0),
          P("div", Qo, [
            L(C, {
              size: "small",
              onClick: F
            }, {
              default: T(() => [...g[3] || (g[3] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: E
            }, {
              default: T(() => [...g[4] || (g[4] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: q
            }, {
              default: T(() => [...g[5] || (g[5] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: G
            }, {
              default: T(() => [...g[6] || (g[6] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ])
        ]),
        L(Q, {
          ref_key: "tableRef",
          ref: s,
          data: d.value,
          "row-key": a.nodeKey,
          "tree-props": { children: "children", checkStrictly: !0 },
          onSelectionChange: fe,
          "default-expand-all": a.defaultExpandAll,
          style: { width: "100%" },
          class: "yzh-tree-table-check-selector__table"
        }, {
          default: T(() => [
            L(S, {
              type: "selection",
              width: "50"
            }),
            (m(!0), R(ae, null, ce(a.columns, (D) => (m(), N(S, {
              key: D.prop,
              prop: D.prop,
              label: D.label,
              width: D.width,
              "min-width": D.minWidth,
              fixed: D.fixed,
              "show-overflow-tooltip": D.showOverflowTooltip !== !1
            }, {
              default: T(({ row: Y }) => [
                W(u.$slots, `column-${D.prop}`, {
                  row: Y,
                  column: D
                }, () => [
                  D.prop === a.nodeTypeField ? (m(), N(M, {
                    key: 0,
                    type: l(Y[a.nodeTypeField]),
                    size: "small"
                  }, {
                    default: T(() => [
                      V(I(n(Y[a.nodeTypeField])), 1)
                    ]),
                    _: 2
                  }, 1032, ["type"])) : (m(), R(ae, { key: 1 }, [
                    V(I(Y[D.prop]), 1)
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
}), cn = /* @__PURE__ */ he(ea, [["__scopeId", "data-v-0aab416f"]]), un = /* @__PURE__ */ ne({
  __name: "YzhTreeTable",
  props: {
    columns: {},
    dataLoader: {},
    searchFields: { default: void 0 },
    selectable: { type: Boolean, default: void 0 },
    selectMode: { default: void 0 },
    showPagination: { type: Boolean, default: !1 },
    pageSize: { default: 20 },
    defaultSort: { default: void 0 },
    height: { default: void 0 },
    rowKey: { default: "Code" },
    emptyText: { default: "暂无数据" },
    toolbar: { type: [Boolean, Object], default: !0 },
    toolbarActions: { default: () => [] },
    searchMaxFields: { default: 2 },
    noPadding: { type: Boolean, default: !1 },
    rowActionButtons: { type: [Object, Array, Function], default: () => [] },
    rowActionLink: { type: Boolean, default: !0 },
    actionMaxInline: { default: 0 },
    defaultExpandAll: { type: Boolean, default: !0 },
    childrenField: { default: "children" },
    allowAddChild: { type: [Boolean, Function], default: !1 },
    addChildText: { default: "新增下级" }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action", "expand-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, n = e, l = k(null);
    function r(y) {
      return typeof t.allowAddChild == "function" ? !!t.allowAddChild(y) : !!t.allowAddChild;
    }
    const s = j(() => {
      const y = t.rowActionButtons;
      if (!t.allowAddChild) return y;
      const p = (A) => {
        const h = Array.isArray(A) ? [...A] : Object.entries(A || {}).map(([K, ee]) => ({ key: K, text: ee }));
        return h.some((K) => K.key === "add-child") ? h : [{ key: "add-child", text: t.addChildText, type: "primary" }, ...h];
      };
      return typeof y == "function" ? (A) => {
        const h = p(y(A));
        return Array.isArray(h) && !r(A) ? h.map((x) => x.key === "add-child" ? { ...x, visible: !1 } : x) : h;
      } : p(y);
    }), d = j(() => ({
      children: t.childrenField,
      hasChildren: "hasChildren"
    }));
    function f() {
      var y;
      (y = l.value) == null || y.refresh();
    }
    function c() {
      var y;
      (y = l.value) == null || y.loadData();
    }
    function b() {
      var y;
      (y = l.value) == null || y.expandAll();
    }
    function w() {
      var y;
      (y = l.value) == null || y.collapseAll();
    }
    return o({
      refresh: f,
      loadData: c,
      expandAll: b,
      collapseAll: w,
      insertRow: (...y) => {
        var p;
        return (p = l.value) == null ? void 0 : p.insertRow(...y);
      },
      replaceRow: (...y) => {
        var p;
        return (p = l.value) == null ? void 0 : p.replaceRow(...y);
      },
      removeRow: (...y) => {
        var p;
        return (p = l.value) == null ? void 0 : p.removeRow(...y);
      },
      getRowCount: () => {
        var y, p;
        return ((p = (y = l.value) == null ? void 0 : y.getRowCount) == null ? void 0 : p.call(y)) ?? 0;
      },
      getSelectedRows: () => {
        var y, p;
        return ((p = (y = l.value) == null ? void 0 : y.getSelectedRows) == null ? void 0 : p.call(y)) ?? [];
      },
      setCheckedRows: (...y) => {
        var p;
        return (p = l.value) == null ? void 0 : p.setCheckedRows(...y);
      },
      clearSelection: () => {
        var y, p;
        return (p = (y = l.value) == null ? void 0 : y.clearSelection) == null ? void 0 : p.call(y);
      },
      tableRef: l
    }), (y, p) => (m(), N(ut, be({
      ref_key: "tableRef",
      ref: l,
      columns: a.columns,
      "data-loader": a.dataLoader,
      "search-fields": a.searchFields,
      selectable: a.selectable,
      "select-mode": a.selectMode,
      "show-pagination": a.showPagination,
      "page-size": a.pageSize,
      "default-sort": a.defaultSort,
      height: a.height,
      "row-key": a.rowKey,
      "empty-text": a.emptyText,
      toolbar: a.toolbar,
      "toolbar-actions": a.toolbarActions,
      "search-max-fields": a.searchMaxFields,
      "no-padding": a.noPadding,
      "row-action-buttons": s.value,
      "row-action-link": a.rowActionLink,
      "action-max-inline": a.actionMaxInline,
      "default-expand-all": a.defaultExpandAll,
      "tree-props": d.value
    }, y.$attrs, {
      onSelectionChange: p[0] || (p[0] = (A) => n("selection-change", A)),
      onRowClick: p[1] || (p[1] = (A, h) => n("row-click", A, h)),
      onRefresh: p[2] || (p[2] = (A) => n("refresh")),
      onRowAction: p[3] || (p[3] = (A, h, x) => n("row-action", A, h, x)),
      onToolbarAction: p[4] || (p[4] = (A, h) => n("toolbar-action", A, h)),
      onExpandChange: p[5] || (p[5] = (A, h) => n("expand-change", A, h))
    }), dt({ _: 2 }, [
      ce(y.$slots, (A, h) => ({
        name: h,
        fn: T((x) => [
          W(y.$slots, h, Et(Mt(x)))
        ])
      }))
    ]), 1040, ["columns", "data-loader", "search-fields", "selectable", "select-mode", "show-pagination", "page-size", "default-sort", "height", "row-key", "empty-text", "toolbar", "toolbar-actions", "search-max-fields", "no-padding", "row-action-buttons", "row-action-link", "action-max-inline", "default-expand-all", "tree-props"]));
  }
}), ta = { class: "yzh-empty-state__inner" }, oa = { class: "yzh-empty-state__title" }, aa = {
  key: 2,
  class: "yzh-empty-state__description"
}, na = {
  key: 3,
  class: "yzh-empty-state__action"
}, la = /* @__PURE__ */ ne({
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
      const t = $("el-icon"), n = $("el-button");
      return m(), R("div", {
        class: ke(["yzh-empty-state", { "is-compact": a.compact, "is-icon-bg": a.iconBackgroundColor }])
      }, [
        P("div", ta, [
          a.iconBackgroundColor ? (m(), R("div", {
            key: 0,
            class: "yzh-empty-state__icon-wrap",
            style: Ne({ backgroundColor: a.iconBackgroundColor })
          }, [
            L(t, {
              class: "yzh-empty-state__icon",
              style: Ne({ fontSize: a.iconSize + "px", color: a.iconColor })
            }, {
              default: T(() => [
                (m(), N(Ie(a.icon)))
              ]),
              _: 1
            }, 8, ["style"])
          ], 4)) : (m(), N(t, {
            key: 1,
            class: "yzh-empty-state__icon",
            style: Ne({ fontSize: a.iconSize + "px", color: a.iconColor })
          }, {
            default: T(() => [
              (m(), N(Ie(a.icon)))
            ]),
            _: 1
          }, 8, ["style"])),
          P("div", oa, I(a.title), 1),
          a.description ? (m(), R("div", aa, I(a.description), 1)) : H("", !0),
          a.actionLabel && a.onAction ? (m(), R("div", na, [
            W(o.$slots, "action", {}, () => [
              L(n, {
                size: "small",
                onClick: a.onAction
              }, {
                default: T(() => [
                  V(I(a.actionLabel), 1)
                ]),
                _: 1
              }, 8, ["onClick"])
            ], !0)
          ])) : H("", !0)
        ])
      ], 2);
    };
  }
}), hn = /* @__PURE__ */ he(la, [["__scopeId", "data-v-33c080f4"]]), sa = /* @__PURE__ */ ne({
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
    const o = a, e = j(() => ({
      success: null,
      // 后续引入图标
      warning: null,
      danger: null,
      info: null
    })[o.type] || null);
    return (t, n) => {
      const l = $("el-icon");
      return m(), R("span", {
        class: ke(["yzh-status-badge", [`is-${a.type}`, `is-${a.size}`]])
      }, [
        a.icon || e.value ? (m(), N(l, {
          key: 0,
          class: "yzh-status-badge__icon"
        }, {
          default: T(() => [
            (m(), N(Ie(a.icon || e.value)))
          ]),
          _: 1
        })) : H("", !0),
        W(t.$slots, "default", {}, () => [
          V(I(a.text), 1)
        ], !0)
      ], 2);
    };
  }
}), fn = /* @__PURE__ */ he(sa, [["__scopeId", "data-v-d7402330"]]), ra = { class: "yzh-card" }, ia = {
  key: 0,
  class: "yzh-card__header"
}, da = { class: "yzh-card__body" }, ca = {
  key: 1,
  class: "yzh-card__footer"
}, ua = /* @__PURE__ */ ne({
  __name: "YzhCard",
  props: {
    title: { type: String, default: "" }
  },
  setup(a) {
    return (o, e) => (m(), R("div", ra, [
      o.$slots.header || a.title ? (m(), R("div", ia, [
        W(o.$slots, "header", {}, () => [
          V(I(a.title), 1)
        ], !0)
      ])) : H("", !0),
      P("div", da, [
        W(o.$slots, "default", {}, void 0, !0)
      ]),
      o.$slots.footer ? (m(), R("div", ca, [
        W(o.$slots, "footer", {}, void 0, !0)
      ])) : H("", !0)
    ]));
  }
}), pn = /* @__PURE__ */ he(ua, [["__scopeId", "data-v-245f071f"]]);
function ha(a) {
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
function fa(a) {
  return {
    NumberBox: "number",
    DatePicker: "date",
    DateTimePicker: "dateRange",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "select"
  }[a] || "text";
}
function pa(a) {
  return {
    input: "text",
    select: "select",
    date: "date",
    cascader: "cascader"
  }[a] || "text";
}
function ya(a) {
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
function ma(a, o = "0", e) {
  const t = a == null ? void 0 : a.Columns, n = a == null ? void 0 : a.Schema;
  if (!t) return [];
  const l = et(a), r = Math.floor(24 / l), s = (e == null ? void 0 : e.withDefaults) ?? !1;
  return t.filter((d) => d.BcFlag && d.Type !== "Other").map((d) => {
    var p;
    const f = d.FieldName, c = va(f), b = n == null ? void 0 : n[c], w = d.GroupIndex || "0", y = o !== "0" && w !== o;
    return {
      prop: f,
      label: d.DesName,
      type: ha(d.Type),
      required: !d.Yxk,
      disabled: d.Enable === !1 || y,
      span: r,
      dictCode: d.DictCode || void 0,
      options: void 0,
      placeholder: (p = d.Type) != null && p.includes("Picker") ? `请选择${d.DesName}` : `请输入${d.DesName}`,
      defaultValue: s ? d.Mrz ? d.Type === "Switch" ? Number(d.Mrz) : d.Mrz : b == null ? void 0 : b.Default : void 0,
      fieldSchema: b
    };
  });
}
function rt(a) {
  const o = a == null ? void 0 : a.SearchFields;
  if (o && o.length > 0)
    return o.map((n) => ({
      prop: n.Field,
      label: n.Label,
      type: pa(n.ControlType),
      placeholder: `请输入${n.Label}`,
      options: n.Options ?? void 0
    }));
  const e = a == null ? void 0 : a.Columns;
  if (!e) return [];
  const t = ["Upload", "TreeSelect", "Cascader", "CheckBox"];
  return e.filter((n) => n.XsFlag && n.Type !== "Other" && !t.includes(n.Type) && n.BcFlag).slice(0, 4).map((n) => ({
    prop: n.FieldName,
    label: n.DesName,
    type: fa(n.Type),
    placeholder: `请输入${n.DesName}`
  }));
}
function ga(a) {
  const o = a == null ? void 0 : a.Toolbar;
  if (!o) return [];
  const e = [];
  if (o.Add !== !1 && e.push({ key: "add", text: "新增", type: "primary" }), o.Delete !== !1 && e.push({ key: "delete", text: "批量删除", type: "danger" }), o.Export !== !1 && e.push({ key: "export", text: "导出", type: "success" }), o.Import !== !1 && e.push({ key: "import", text: "导入", type: "warning" }), o.CustomButtons)
    for (const [t, n] of Object.entries(o.CustomButtons))
      e.push({ key: `custom:${n}`, text: t, type: "info" });
  return e;
}
function ht(a, o) {
  const e = (a == null ? void 0 : a.RowButtons) ?? {}, t = [];
  if (e.Edit !== !1 && t.push({ key: "edit", text: "编辑", type: "primary" }), e.Delete !== !1 && t.push({ key: "delete", text: "删除", type: "danger" }), e.Enable === !0 && o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), e.CustomButtons)
    for (const [n, l] of Object.entries(e.CustomButtons))
      t.push({ key: `custom:${l}`, text: n, type: "info" });
  return t;
}
function yn(a, o) {
  const e = {};
  for (const t of ht(a, o)) e[t.key] = t.text;
  return e;
}
function mn(a, o, e) {
  const t = [], n = a;
  if (!n) return t;
  if (n.AllowEdit && ((e == null ? void 0 : e.allowAddChild) !== !1 && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), n.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), n.CustomActions)
    for (const [l, r] of Object.entries(n.CustomActions))
      t.push({ key: `custom:${l}`, text: r, type: "info" });
  return t;
}
function gn(a, o) {
  var t;
  const e = ((t = o == null ? void 0 : o.Extra) == null ? void 0 : t.level) ?? (o == null ? void 0 : o.level) ?? -1;
  return {
    ...a,
    Extra: { ...a.Extra, level: e + 1 },
    Children: []
  };
}
function va(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
const Ge = "YZH_TOKEN", pe = {
  get: () => localStorage.getItem(Ge),
  set: (a) => localStorage.setItem(Ge, a),
  clear: () => localStorage.removeItem(Ge)
};
class ft {
  constructor(o) {
    /** 服务根地址（文件下载等场景需要读取） */
    z(this, "baseURL");
    z(this, "getToken");
    z(this, "onUnauthorized");
    z(this, "onError");
    this.baseURL = o.baseURL.replace(/\/$/, ""), this.getToken = o.getToken || (() => pe.get()), this.onUnauthorized = o.onUnauthorized, this.onError = o.onError;
  }
  /**
   * 动态配置客户端（宿主 main.ts 启动时调用 `configureYzhApi`）
   * baseURL 缺省为 ''（相对路径 /api/*）—— dev 由宿主 vite proxy 承接、prod 同源承接；
   * 跨域部署才由宿主显式注入绝对地址。core 永不硬编码地址（守卫 R11）。
   */
  configure(o) {
    o.baseURL !== void 0 && (this.baseURL = o.baseURL.replace(/\/$/, "")), o.getToken && (this.getToken = o.getToken), o.onUnauthorized !== void 0 && (this.onUnauthorized = o.onUnauthorized), o.onError !== void 0 && (this.onError = o.onError);
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
      raw: d = !1
    } = e;
    let f = o;
    const c = {
      method: t,
      headers: {
        "Content-Type": "application/json",
        ...r
      }
    };
    if (s !== !1) {
      const y = this.getToken();
      y && (c.headers.Authorization = `Bearer ${y}`);
    }
    if (n) {
      let y = n;
      const p = Object.keys(n), A = n.params;
      p.length === 1 && p[0] === "params" && A && typeof A == "object" && (console.warn(
        "[YzhApi] 查询参数多包了一层 params（应为 get(url, { a, b }) 而非 get(url, { params: { a, b } })），已自动解包：",
        A
      ), y = A);
      const h = new URLSearchParams();
      Object.entries(y).forEach(([K, ee]) => {
        ee != null && h.append(K, String(ee));
      });
      const x = h.toString();
      x && (f += (o.includes("?") ? "&" : "?") + x);
    }
    l !== void 0 ? c.body = JSON.stringify(l) : t !== "GET" && !n && (c.body = "{}");
    try {
      const y = await fetch(this.baseURL + f, c);
      if (y.status === 401)
        throw pe.clear(), (b = this.onUnauthorized) == null || b.call(this), new Error("登录已过期，请重新登录");
      const p = await y.json();
      if (!y.ok) {
        const A = (p == null ? void 0 : p.message) || (p == null ? void 0 : p.msg) || `请求失败 (${y.status})`, h = new Error(A);
        throw h.status = y.status, h.data = p, h;
      }
      return p;
    } catch (y) {
      throw (w = this.onError) == null || w.call(this, y), y;
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
      const d = new URLSearchParams();
      Object.entries(e).forEach(([c, b]) => {
        b != null && d.append(c, String(b));
      });
      const f = d.toString();
      f && (n += (o.includes("?") ? "&" : "?") + f);
    }
    const l = await fetch(this.baseURL + n, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (l.status === 401)
      throw pe.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
    if ((l.headers.get("content-type") || "").includes("application/json")) {
      const d = await l.json().catch(() => ({})), f = new Error((d == null ? void 0 : d.message) || (d == null ? void 0 : d.msg) || `请求失败 (${l.status})`);
      throw f.status = l.status, f;
    }
    if (!l.ok) {
      const d = new Error(`请求失败 (${l.status})`);
      throw d.status = l.status, d;
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
      throw pe.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
    if (!l.ok) {
      const d = await l.json().catch(() => ({}));
      throw new Error(d.message || d.msg || "下载失败");
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
      throw pe.clear(), (r = this.onUnauthorized) == null || r.call(this), new Error("登录已过期，请重新登录");
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
      throw pe.clear(), (l = this.onUnauthorized) == null || l.call(this), new Error("登录已过期，请重新登录");
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
const Ae = new ft({
  baseURL: "",
  onUnauthorized: () => {
    console.warn("[YzhApi] 401 未授权，请重新登录");
  }
}), Re = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  YzhApiClient: ft,
  tokenStore: pe,
  yzhApi: Ae
}, Symbol.toStringTag, { value: "Module" })), De = "/api/file-storage";
function vn(a, o) {
  const e = new FormData();
  return e.append("file", a), Ae.post(`${De}/upload`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function bn(a, o) {
  const e = new FormData();
  return a.forEach((t) => e.append("files", t)), Ae.post(`${De}/upload-batch`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function Cn(a) {
  const o = Ae.baseURL || "", e = localStorage.getItem("token") || "";
  return `${o}${De}/download?path=${encodeURIComponent(a)}&token=${e}`;
}
function wn(a) {
  return Ae.post(`${De}/delete`, null, { params: { path: a } });
}
function kn(a) {
  return Ae.get(`${De}/exists`, { path: a });
}
function Tn(a) {
  return Ae.get(`${De}/list`, { prefix: a });
}
function xn() {
  const a = k(pe.get() || ""), o = k(null), e = j(() => !!a.value);
  function t(s) {
    a.value = s, pe.set(s);
  }
  function n() {
    a.value = "", o.value = null, pe.clear();
  }
  function l(s, d) {
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
const Oe = k(pe.get() || ""), Be = k(null), tt = k([]), ba = () => !!Oe.value;
function Ca(a) {
  Oe.value = a, pe.set(a);
}
function wa(a) {
  Be.value = a;
}
function ka(a) {
  Be.value = { ...Be.value || {}, ...a };
}
function Ta(a) {
  tt.value = a;
}
function xa() {
  Oe.value = "", Be.value = null, tt.value = [], pe.clear();
}
function Sn() {
  return {
    token: Oe,
    userInfo: Be,
    roles: tt,
    isAuthenticated: ba,
    setToken: Ca,
    setUserInfo: wa,
    patchUserInfo: ka,
    setRoles: Ta,
    clearToken: xa
  };
}
function Sa(a) {
  return {
    id: a.Id ?? "",
    code: a.Code ?? "",
    menuName: a.MenuName ?? "",
    parentCode: a.ParentCode ?? "0",
    url: a.Url,
    icon: a.Icon,
    description: a.Description,
    enable: a.IsValid ?? 1,
    orderNo: a.OrderNo ?? 0,
    tag: a.Tag
  };
}
function _a(a) {
  var l;
  const o = a.map(Sa).sort((r, s) => r.orderNo - s.orderNo), e = /* @__PURE__ */ new Map(), t = [];
  for (const r of o)
    e.set(r.code, { ...r, children: [] });
  for (const r of o) {
    const s = e.get(r.code);
    if (r.parentCode === "0" || !e.has(r.parentCode))
      t.push(s);
    else {
      const d = e.get(r.parentCode);
      (l = d == null ? void 0 : d.children) == null || l.push(s);
    }
  }
  const n = (r) => r.map((s) => {
    var f;
    const d = { ...s };
    return ((f = d.children) == null ? void 0 : f.length) === 0 ? delete d.children : d.children = n(d.children), d;
  });
  return n(t);
}
async function Aa() {
  const a = await Ae.get("/api/System/MenuManagement/tree");
  return { ...a, data: Array.isArray(a.data) ? _a(a.data) : [] };
}
const Me = k([]), He = k(!1), Ue = k(!1);
function _n() {
  async function a(e = !1) {
    if (!(Ue.value && !e)) {
      He.value = !0;
      try {
        const t = await Aa();
        Me.value = t.data ?? [], Ue.value = !0;
      } catch (t) {
        console.error("加载菜单失败:", t), Me.value = [];
      } finally {
        He.value = !1;
      }
    }
  }
  function o() {
    Me.value = [], Ue.value = !1;
  }
  return { menus: Me, loading: He, loaded: Ue, loadMenus: a, clearMenus: o };
}
const Je = "yzh:menu-changed";
function An() {
  window.dispatchEvent(new Event(Je));
}
function Fn(a) {
  return window.addEventListener(Je, a), () => window.removeEventListener(Je, a);
}
function zn() {
  const a = k(!1), o = k([]), e = k(0), t = k(1), n = k(20), l = Ce({});
  async function r(f) {
    a.value = !0;
    try {
      const c = {
        page: t.value,
        rows: n.value,
        ...l
      }, b = await f(c);
      o.value = b.rows || [], e.value = b.total || 0;
    } finally {
      a.value = !1;
    }
  }
  function s(f) {
    Object.assign(l, f), t.value = 1;
  }
  function d() {
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
    resetSearchParams: d
  };
}
function Nn() {
  async function a(o) {
    try {
      return await Se.confirm(o.message, o.title ?? "操作确认", {
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
function Dn(a, ...o) {
  const e = new a(...o), t = k(null);
  return Pe(async () => {
    await e.init(), await xe(), e.setTableRef(t.value);
  }), { logic: e, tableRef: t };
}
function $n(a, ...o) {
  const e = new a(...o), t = k(null), n = k(null);
  return Pe(async () => {
    await e.init(), await xe(), e.setTableRef(t.value), e.setTreeTableRef(n.value);
  }), { logic: e, tableRef: t, treeTableRef: n };
}
function Rn(a, ...o) {
  const e = new a(...o);
  return Pe(async () => {
    await e.init();
  }), { logic: e };
}
function Bn(a, ...o) {
  const e = new a(...o);
  return Pe(async () => {
    await e.init();
  }), { logic: e };
}
function pt(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
function Fa(a) {
  return !a || a[0] >= "A" && a[0] <= "Z" ? a : a[0].toUpperCase() + a.slice(1);
}
function Xe(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[Fa(e)] = t;
  return o;
}
function Pn(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[pt(e)] = t;
  return o;
}
class za {
  constructor() {
    // ──── 后端配置 ────
    /** 后端页面配置（工具栏+表格+表单+搜索栏） */
    z(this, "config", k(null));
    // ──── 表格状态 ────
    /** 表格数据行（PascalCase 字段） */
    z(this, "rows", k([]));
    /** 表格加载状态 */
    z(this, "loading", k(!1));
    /** 选中行集合 */
    z(this, "selectedRows", k([]));
    // ──── 分页状态 ────
    /** 分页参数 */
    z(this, "pagination", Ce({ page: 1, pageSize: 20, total: 0 }));
    // ──── 搜索过滤状态 ────
    /** 搜索参数（PascalCase key，与业务实体字段名一致） */
    z(this, "searchParams", Ce({}));
    // ──── 排序状态 ────
    /** 排序字段（PascalCase） */
    z(this, "sortField", k());
    /** 排序方向 */
    z(this, "sortOrder", k());
    // ──── 弹窗状态 ────
    /** 弹窗可见性 */
    z(this, "dialogVisible", k(!1));
    /** 弹窗模式 */
    z(this, "dialogMode", k("add"));
    /**
     * 表单编辑模式（GroupIndex 控制）
     *
     * - '0'：新增/编辑模式，GroupIndex="0" 的字段可编辑
     * - '99'：详情模式，仅 GroupIndex="99" 的字段可编辑（JSON 通常不配 → 全部只读）
     */
    z(this, "formGroupIndex", k("0"));
    /** 提交中状态 */
    z(this, "submitting", k(!1));
    // ──── ShowDisabled 开关（基类统一管理，子类无需手动实现） ────
    /** 显示已禁用记录开关 */
    z(this, "showDisabled", k(!1));
    /**
     * 表单数据：PascalCase key（与 formFields[].prop、NewEntity、实体属性名一致）
     * 例：{ Code: "", UserName: "", Enable: 1 }
     */
    z(this, "formData", Ce({}));
    // ──── 表格引用（局部刷新） ────
    z(this, "_tableRef", null);
    // ========================================================
    // 动作统一（ST-7 dispatch + registerHandler）
    // ========================================================
    z(this, "handlers", /* @__PURE__ */ new Map());
    /** 行动作入口（绑定 @row-action="logic.onRowAction"；箭头属性自动绑定 this，模板引用式传参不丢上下文） */
    z(this, "onRowAction", async (o, e, t) => {
      await this.dispatch(o, e, t);
    });
    /** 工具栏动作入口（绑定 @toolbar-action="logic.onToolbarAction"） */
    z(this, "onToolbarAction", async (o, e) => {
      await this.dispatch(o, void 0, e);
    });
    /** @deprecated 兼容旧命名，等价 onToolbarAction */
    z(this, "onToolbarClick", async (o) => {
      await this.dispatch(o);
    });
    /** @deprecated 兼容旧命名，等价 onRowAction */
    z(this, "onRowClick", async (o, e) => {
      await this.dispatch(o, e);
    });
    // ========================================================
    // 事件处理（表格原生事件；箭头属性自动绑定 this，供模板引用式绑定）
    // ========================================================
    z(this, "onSearch", async (o) => {
      this.resetObject(this.searchParams), Object.assign(this.searchParams, o), this.pagination.page = 1, await this.loadPage();
    });
    z(this, "onPageChange", async (o) => {
      this.pagination.page = o, await this.loadPage();
    });
    z(this, "onSizeChange", async (o) => {
      this.pagination.pageSize = o, this.pagination.page = 1, await this.loadPage();
    });
    z(this, "onSortChange", async (o, e) => {
      this.sortField.value = o, this.sortOrder.value = e, await this.loadPage();
    });
    z(this, "onSelectionChange", (o) => {
      this.selectedRows.value = o;
    });
    /** 当前编辑行（ST-8：提交后与后端返回合并，避免表格行丢字段） */
    z(this, "editingRow", k(null));
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
    return ya(this.config.value);
  }
  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动） */
  get formLayoutCols() {
    return et(this.config.value);
  }
  /** 表单字段配置（AD-2） */
  get formFields() {
    return ma(this.config.value, this.formGroupIndex.value);
  }
  /** 搜索栏字段（config.SearchFields 优先；为空时走 fallbackSearchFields 钩子再走列推导） */
  get searchFields() {
    var t;
    const o = (t = this.config.value) == null ? void 0 : t.SearchFields;
    if (o && o.length > 0)
      return rt(this.config.value);
    const e = this.fallbackSearchFields;
    return e.length > 0 ? e : rt(this.config.value);
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
    return ga(this.config.value);
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
    return ht(this.config.value, this.enableField);
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
      }, d = await this.apiPost("/filter", s), f = d == null ? void 0 : d.data, c = this.postprocessRows(((f == null ? void 0 : f.Items) ?? []).slice());
      return this.pagination.page = e, this.pagination.pageSize = t, this.pagination.total = (f == null ? void 0 : f.TotalCount) ?? 0, this.rows.value = c, this.onDataLoaded(c), { rows: c, total: this.pagination.total };
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
    ).map(([s, d]) => ({
      Field: s,
      Value: Array.isArray(d) ? d.join(",") : String(d),
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
    return e.success ? (J.success(e.data.IsValid === 1 ? "已启用" : "已禁用"), e.data) : null;
  }
  /**
   * 切换行有效标志（完整流程：确认弹窗 → API → 本地更新）
   */
  async toggleRowIsValidWithConfirm(o, e) {
    const t = (e == null ? void 0 : e.field) ?? this.enableField ?? "IsValid", l = (o[t] ?? 1) === 1 ? "禁用" : "启用", r = (e == null ? void 0 : e.entityName) ?? this.entityName(o);
    await Se.confirm(
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
      J.success("保存成功"), this.dialogVisible.value = !1;
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
      J.warning("请先选择要删除的记录");
      return;
    }
    const t = this.primaryKey, n = e.map((d) => String(d[t] || "")).filter(Boolean);
    if (!await this.onDelete(n)) return;
    const r = e.map((d) => this.entityName(d)).filter(Boolean);
    let s;
    r.length === 1 ? s = `确定删除【${r[0]}】？` : r.length > 1 && r.length <= 3 ? s = `确定删除 ${r.length} 条记录（${r.join("、")}）？` : s = `确定删除 ${n.length} 条记录？`, await Se.confirm(s, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.delete(n), J.success("删除成功");
    for (const d of n)
      this.removeRowByCode(d);
    this.selectedRows.value = [], this.onAfterDelete(n);
  }
  async executeCustomToolbarAction(o) {
  }
  // ========================================================
  // API 调用
  // ========================================================
  async apiGet(o) {
    const e = `/api/${this.controllerName}${o}`, { yzhApi: t } = await Promise.resolve().then(() => Re);
    return t.get(e);
  }
  async apiPost(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Re);
    return n.post(t, e);
  }
  async apiPostAndDownload(o, e, t) {
    const n = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Re);
    return l.download(n, e, t);
  }
  async apiGetAndDownload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Re);
    return n.downloadGet(t, e);
  }
  async apiUpload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Re);
    return n.upload(t, e);
  }
  // ========================================================
  // 私有工具方法
  // ========================================================
  resetObject(o) {
    Object.keys(o).forEach((e) => delete o[e]);
  }
}
class Na {
  constructor() {
    /** 树数据（PascalCase，与后端 DTO 保持一致） */
    z(this, "treeData", k([]));
    /** 树加载状态 */
    z(this, "treeLoading", k(!1));
    /** 当前选中节点 */
    z(this, "selectedNode", k(null));
    /** 节点索引：Code → { node, parent }（O(1) 查找/替换/删除） */
    z(this, "index", /* @__PURE__ */ new Map());
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
      for (const d of s.Children ?? []) l(d);
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
function Da(a) {
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
class Vn extends za {
  constructor() {
    super(...arguments);
    // ──── 树能力混入（TT-2：状态 + 索引 + 增量变更） ────
    z(this, "treeSide", new Na());
    /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
    z(this, "treeTableConfig", k(null));
    // ──── 树节点表单弹窗状态 ────
    z(this, "treeDialogVisible", k(!1));
    z(this, "treeDialogMode", k("add"));
    z(this, "treeSubmitting", k(!1));
    /** 树节点表单数据：PascalCase key（与 treeFormFields prop 一致） */
    z(this, "treeFormData", Ce({}));
    /** 当前新增节点的父节点 */
    z(this, "treeParentNode", k(null));
    /** 当前编辑的节点 */
    z(this, "treeEditingNode", k(null));
    // ──── 树表组件引用（用于 appendNode 等直接操作） ────
    z(this, "_treeTableRef", null);
    // ========================================================
    // 树→表格联动（TT-6/TT-7）
    // ========================================================
    /** 节点点击 → 表格联动刷新（dataLoader 已自动注入 RelateField；箭头属性自动绑定 this） */
    z(this, "onNodeClick", async (e) => {
      var t;
      this.treeSide.selectedNode.value = e, this.pagination.page = 1, !((t = this.treeConfig) != null && t.OnlyLeafSelectable && !e.IsLeaf) && (this._tableRef ? await this._tableRef.refresh() : await this.refreshTable());
    });
    // ========================================================
    // dispatch 扩展（TT-10）：树节点动作路由
    // ========================================================
    /** 树节点动作入口（绑定 @tree-node-action="logic.onNodeAction"；箭头属性自动绑定 this） */
    z(this, "onNodeAction", async (e, t) => {
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
    });
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
   * EnableField → 禁用/启用（按节点状态动态显示单个）；CustomActions → 自定义动作。前端零硬编码。
   */
  get nodeActions() {
    return (e) => this.resolveTreeActions(e);
  }
  /** 树动作解析（子类可覆盖以追加自定义动作） */
  resolveTreeActions(e) {
    const t = [], n = this.treeConfig;
    if (!n) return t;
    if (n.AllowEdit && (this.allowAddChild && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), n.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), n.EnableField || this.enableField) {
      const l = n.EnableField ?? this.enableField;
      if (l) {
        const r = e.Extra || {}, s = l.charAt(0).toLowerCase() + l.slice(1);
        (r[l] ?? r[s] ?? 1) === 1 ? t.push({ key: "toggle-disable", text: "禁用", type: "warning" }) : t.push({ key: "toggle-enable", text: "启用", type: "warning" });
      }
    }
    if (!n.EnableField && !this.enableField && n.CustomActions)
      for (const [l, r] of Object.entries(n.CustomActions))
        t.push({ key: `custom:${l}`, text: r, type: "info" });
    return t;
  }
  /**
   * 获取树节点操作按钮的显示文字（toggle 按节点状态动态显示）
   */
  getNodeActionLabel(e, t) {
    if (e === "toggle-disable" || e === "toggle-enable")
      return e === "toggle-disable" ? "禁用" : "启用";
    const n = this.nodeActions(t).find((l) => l.key === e);
    return (n == null ? void 0 : n.text) ?? e;
  }
  /** 树节点表单布局列数（从 TreeFormConfig.FormCols 读取） */
  get treeFormLayoutCols() {
    return et(this.treeFormConfig);
  }
  /** 树节点表单字段配置（保留 TreeFormConfig 专用布局规则：ColSpan>1 占满整行） */
  get treeFormFields() {
    var r, s;
    const e = (r = this.treeFormConfig) == null ? void 0 : r.Columns, t = (s = this.treeFormConfig) == null ? void 0 : s.Schema;
    if (!e) return [];
    const n = this.treeFormLayoutCols, l = Math.floor(24 / n);
    return e.filter((d) => d.BcFlag && d.Type !== "Other").map((d) => {
      var w;
      const f = d.FieldName, c = pt(f), b = t == null ? void 0 : t[c];
      return {
        prop: f,
        label: d.DesName,
        type: Da(d.Type),
        required: !d.Yxk,
        disabled: d.Enable === !1,
        span: (d.ColSpan ?? 0) > 1 ? 24 : l,
        dictCode: d.DictCode || void 0,
        options: void 0,
        placeholder: (w = d.Type) != null && w.includes("Picker") ? `请选择${d.DesName}` : `请输入${d.DesName}`,
        defaultValue: d.Mrz ? d.Type === "Switch" ? Number(d.Mrz) : d.Mrz : b == null ? void 0 : b.Default,
        fieldSchema: b
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
    const c = ((await this.apiPost("/tree/children", {
      ParentCode: r,
      Level: s
    })).data ?? []).map((w) => this.dtoToNode(w, l));
    for (const w of c) this.treeSide.register(w, l);
    return t && t(c), l && typeof l == "object" && (l.children = c), c;
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
      return this.requireTreeSelectionForAdd && !t ? (J.warning("请先在左侧选择节点"), !1) : t && !this.isVirtualNode(t) && !this.canAddUnderNode(t) ? (J.warning(this.canAddUnderNodeMessage(t)), !1) : (this.dialogMode.value = "add", this.formGroupIndex.value = "0", this.initFormData(), this.onPrepareAdd(this.formData), this.dialogVisible.value = !0, !0);
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
    var r, s, d;
    if (e) {
      this.treeDialogMode.value = "edit", this.treeEditingNode.value = e, this.treeParentNode.value = null, this.resetObject(this.treeFormData);
      const f = ((r = this.treeFormConfig) == null ? void 0 : r.NewEntity) || {}, c = e.Extra || {}, b = {};
      for (const w of this.treeFormFields)
        w.prop in c && (b[w.prop] = c[w.prop]);
      return Object.assign(this.treeFormData, f, b, {
        Code: e.Code,
        ParentCode: e.ParentCode,
        [this.treeEntityNameField]: e.Name
      }), this.treeDialogVisible.value = !0, !0;
    }
    const n = t ?? this.selectedNode;
    if (this.requireTreeSelectionForAdd && !n)
      return J.warning("请先在左侧选择节点"), !1;
    if (n && !this.canAddUnderNode(n))
      return J.warning(this.canAddUnderNodeMessage(n)), !1;
    this.treeDialogMode.value = "add", this.treeEditingNode.value = null, this.treeParentNode.value = n, this.resetObject(this.treeFormData);
    const l = ((s = this.treeFormConfig) == null ? void 0 : s.NewEntity) || {};
    return Object.assign(this.treeFormData, l, this.defaultTreeValues, {
      [this.treeEntityNameField]: "",
      ParentCode: (n == null ? void 0 : n.Code) ?? ((d = this.treeConfig) == null ? void 0 : d.RootParentCode) ?? null
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
      this.treeDialogVisible.value = !1, J.success(this.treeDialogMode.value === "add" ? "创建成功" : "修改成功");
    } finally {
      this.treeDialogMode.value = "add", this.treeEditingNode.value = null, this.treeSubmitting.value = !1;
    }
  }
  /** 删除树节点（完整流程：确认弹窗 → API → 本地更新 → 表格联动） */
  async deleteTreeNodeWithConfirm(e) {
    const t = e.Name;
    await this.onBeforeDeleteTree(e) && (await Se.confirm(`确定删除【${t}】？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.deleteTreeNode(e, !0), this.onAfterDeleteTree(e), J.success("已删除"));
  }
  // ========================================================
  // 树节点底层操作（兼容保留）
  // ========================================================
  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(e, t) {
    var c, b, w, y;
    const n = ((c = this.treeConfig) == null ? void 0 : c.CodeField) ?? "Code", l = {
      ...Xe(t),
      [((b = this.treeConfig) == null ? void 0 : b.ParentCodeField) ?? "ParentCode"]: (e == null ? void 0 : e.Code) ?? ((w = this.treeConfig) == null ? void 0 : w.RootParentCode) ?? null
    }, r = await this.apiPost("/tree/add", l), d = (((y = r.data) == null ? void 0 : y[n]) ?? "") || l[n], f = this.dtoToNode(
      r.data ?? { Code: d, Name: l.Name ?? "", ParentCode: (e == null ? void 0 : e.Code) ?? null },
      e ?? void 0
    );
    return d && !r.data && (f.Code = d), this._treeTableRef ? (this._treeTableRef.appendNode((e == null ? void 0 : e.Code) ?? null, f), this.treeSide.register(f, e)) : this.treeSide.appendChild((e == null ? void 0 : e.Code) ?? null, f), f;
  }
  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(e, t, n) {
    var f, c;
    const l = n ? Xe(n) : {}, r = {
      [((f = this.treeConfig) == null ? void 0 : f.CodeField) ?? "Code"]: e.Code,
      [((c = this.treeConfig) == null ? void 0 : c.NameField) ?? "Name"]: t,
      ...l
    }, s = await this.apiPost("/tree/update", r), d = this.dtoToNode(
      s.data ?? { ...e, Name: t },
      this.treeSide.findParent(e.Code)
    );
    this.treeSide.replaceNode(e.Code, d) || (e.Name = t);
  }
  /** 删除树节点（skipConfirm=true 时由调用方负责确认） */
  async deleteTreeNode(e, t = !1) {
    var l, r, s;
    if (!((l = this.treeConfig) != null && l.AllowDeleteWithChildren) && e.Children && e.Children.length > 0) {
      J.warning("该节点包含子节点，请先删除子节点");
      return;
    }
    t || await Se.confirm(`确定删除节点 "${e.Name}"？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定",
      cancelButtonText: "取消"
    });
    const n = await this.apiPost("/tree/delete", [e.Code]);
    if (!n.success) {
      J.error(n.message || "删除失败");
      return;
    }
    if (this.treeSide.removeNode(e.Code), (r = this._treeTableRef) != null && r.removeNode)
      try {
        this._treeTableRef.removeNode(null, e.Code);
      } catch {
        await this.loadTreeRoot();
      }
    else
      await this.loadTreeRoot();
    ((s = this.selectedNode) == null ? void 0 : s.Code) === e.Code && (this.treeSide.selectedNode.value = null, await this.loadPageWithoutTree());
  }
  /** 树节点执行自定义操作 */
  async executeTreeAction(e, t, n) {
    var s;
    const l = n ? Xe(n) : {}, r = await this.apiPost(`/tree/action/${e}`, {
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
      r[t] = n.data.IsValid;
      const s = t.charAt(0).toLowerCase() + t.slice(1);
      return s !== t && (r[s] = n.data.IsValid), e.Extra = { ...r }, J.success(n.data.IsValid === 1 ? "已启用" : "已禁用"), n.data;
    }
    return null;
  }
  /** 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新） */
  async toggleTreeNodeWithConfirm(e, t) {
    const n = this.enableField ?? "IsValid", l = e.Extra || {}, r = n.charAt(0).toLowerCase() + n.slice(1), d = (l[n] ?? l[r] ?? 1) === 1 ? "禁用" : "启用", f = (t == null ? void 0 : t.entityName) ?? e.Name;
    await Se.confirm(`确定${d}【${f}】？`, `${d}确认`, {
      type: "warning",
      confirmButtonText: `确定${d}`,
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
class yt {
  constructor(o) {
    // ──── 左树 ────
    z(this, "treeData", k([]));
    z(this, "selectedNode", k(null));
    // ──── 右侧关联数据 ────
    z(this, "associationData", k([]));
    // ──── 加载状态 ────
    z(this, "loading", k(!1));
    z(this, "saving", k(!1));
    // ──── 本地关联缓存（badge / 差集保存依据） ────
    z(this, "associationCache", Ce(/* @__PURE__ */ new Map()));
    z(this, "cacheLoaded", !1);
    // ──── API 注入 ────
    z(this, "api");
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
      return this.treeData.value = o, this.applyBadgesDeep(o), o;
    } catch (o) {
      return J.error(o.message || "加载树失败"), [];
    }
  }
  async loadChildren(o, e) {
    try {
      const t = await this.api.getTreeChildren(o.data.Code, o.level ?? 0);
      this.applyBadgesDeep(t), e(t);
    } catch (t) {
      J.error(t.message || "加载子节点失败"), e([]);
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
  /** 写入/清除单节点 Extra.badge（原地改 → 仅该节点重渲染，el-tree 不重置展开/懒加载态） */
  applyBadge(o) {
    const e = this.getCountForNode(o.Code), t = { ...o.Extra ?? {} };
    e > 0 ? t.badge = String(e) : delete t.badge, o.Extra = t;
  }
  /** 递归注入徽标（树根 / 懒加载子节点 resolve 前调用） */
  applyBadgesDeep(o) {
    var e;
    for (const t of o)
      this.applyBadge(t), (e = t.Children) != null && e.length && this.applyBadgesDeep(t.Children);
  }
  /** 按 Code 递归查找（懒加载子节点不在 treeData 时返回 null） */
  findNodeByCode(o, e) {
    var t;
    for (const n of o) {
      if (String(n.Code) === String(e)) return n;
      if ((t = n.Children) != null && t.length) {
        const l = this.findNodeByCode(n.Children, e);
        if (l) return l;
      }
    }
    return null;
  }
  /** 局部刷新单个节点徽标；找不到（如懒加载子节点未挂进 treeData）静默跳过，不退回整树替换 */
  refreshBadge(o) {
    if (!o) return;
    const e = this.findNodeByCode(this.treeData.value, o);
    e && this.applyBadge(e);
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
        J.error(e.message || "加载数据失败"), this.associationData.value = [];
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
      J.warning("请先选择左侧节点");
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
      J.success("保存成功");
    } catch (t) {
      J.error(t.message || "保存失败");
    } finally {
      this.refreshBadge(e), this.saving.value = !1;
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
class Ln extends yt {
  /** 可勾选的节点类型（如 ['menu']）—— 子类必须声明 */
  get selectableNodeTypes() {
    return [];
  }
}
class En extends yt {
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
    z(this, "linkApi");
    /** 搜索关键字（页面可绑定本地过滤） */
    z(this, "searchKey", k(""));
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
        J.error(t.message || "加载关联数据失败"), this.associationData.value = [];
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
      this.associationData.value.filter((c) => c.Linked).map((c) => c[this.linkedKeyField])
    ), r = [], s = [];
    for (const c of this.associationData.value) {
      const b = c[this.linkedKeyField], w = e.has(b), y = l.has(b);
      w && !y && r.push(c), !w && y && s.push(c);
    }
    for (const c of this.associationData.value)
      c.Linked = e.has(c[this.linkedKeyField]);
    const d = [
      ...r.map((c) => this.buildSavePayload(n, c, !0)),
      ...s.map((c) => this.buildSavePayload(n, c, !1))
    ];
    if (d.length === 0) return;
    const f = [];
    for (const c of d)
      try {
        await this.linkApi.save(c);
      } catch {
        const b = this.associationData.value.find(
          (w) => w[this.linkedKeyField] === c[this.linkedKeyField]
        );
        b && (b.Linked = !b.Linked), f.push(t(b ?? c));
      }
    f.length > 0 && J.error(`保存失败：${f.join("、")}`);
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
const Mn = [
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
function $a(a, o, e) {
  if (o === null || o === "")
    return [...a, e];
  const t = (n) => n.map((l) => l.Code === o ? { ...l, Children: [...l.Children ?? [], e] } : l.Children && l.Children.length > 0 ? { ...l, Children: t(l.Children) } : l);
  return t(a);
}
function Ra(a, o) {
  const e = [], t = (n) => {
    const l = [];
    for (const r of n)
      r.Code === o ? (e.push(r), Pa(r).forEach((s) => e.push(s))) : r.Children && r.Children.length > 0 ? l.push({ ...r, Children: t(r.Children) }) : l.push(r);
    return l;
  };
  return { tree: t(a), removed: e };
}
function Un(a, o, e, t) {
  const n = Ze(a, o);
  if (!n)
    return { tree: a, error: `节点 "${o}" 不存在` };
  if (o === e)
    return { tree: a, error: "不能移动到自己" };
  if (e !== null && e !== "") {
    if (!Ze(a, e))
      return { tree: a, error: `目标父节点 "${e}" 不存在` };
    if (mt(n, e))
      return { tree: a, error: "不能移动到自己的子树下（会形成循环）" };
  }
  if (t !== void 0 && t > 0) {
    const s = gt(n);
    if ((e === null || e === "" ? 0 : Va(a, e)) + 1 + s > t)
      return { tree: a, error: `移动后深度将超过限制 (${t})` };
  }
  const { tree: l } = Ra(a, o);
  return { tree: $a(l, e, {
    ...n,
    ParentCode: e
  }) };
}
function In(a, o, e) {
  const t = (n) => n.map((l) => l.Code === o ? { ...l, ...e } : l.Children && l.Children.length > 0 ? { ...l, Children: t(l.Children) } : l);
  return t(a);
}
function On(...a) {
  const o = [];
  for (const e of a)
    o.push(...e);
  return o;
}
function Kn(a, o) {
  const e = it(a), t = it(o), n = new Map(e.map((f) => [f.node.Code, f])), l = new Map(t.map((f) => [f.node.Code, f])), r = [], s = [], d = [];
  for (const [, f] of l) {
    const c = n.get(f.node.Code);
    if (!c)
      r.push(f.node);
    else if (!Ea(c.node, f.node)) {
      const b = Ma(c.node, f.node);
      d.push({ code: f.node.Code, changes: b });
    }
  }
  for (const [f] of n)
    l.has(f) || s.push(f);
  return { added: r, removed: s, updated: d };
}
function jn(a) {
  const o = [], e = Ba(a), t = new Set(e.map((l) => l.Code)), n = /* @__PURE__ */ new Map();
  for (const l of e)
    n.set(l.Code, (n.get(l.Code) ?? 0) + 1);
  for (const [l, r] of n)
    r > 1 && o.push(`Code "${l}" 重复 ${r} 次`);
  for (const l of e)
    l.ParentCode != null && l.ParentCode !== "" && !t.has(l.ParentCode) && o.push(`节点 "${l.Name}" 的 ParentCode "${l.ParentCode}" 不存在`);
  return La(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function Ba(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a), o;
}
function Ze(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = Ze(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function Pa(a) {
  const o = [], e = (t) => {
    o.push(t);
    for (const n of t.Children ?? []) e(n);
  };
  return e(a), o;
}
function mt(a, o) {
  for (const e of a.Children ?? [])
    if (e.Code === o || mt(e, o)) return !0;
  return !1;
}
function gt(a) {
  if (!a.Children || a.Children.length === 0) return 1;
  let o = 0;
  for (const e of a.Children)
    o = Math.max(o, gt(e));
  return o + 1;
}
function Va(a, o) {
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
function La(a) {
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
function it(a) {
  const o = [], e = (t, n) => {
    for (const l of t)
      o.push({ node: l, level: n }), l.Children && l.Children.length > 0 && e(l.Children, n + 1);
  };
  return e(a, 0), o;
}
function Ea(a, o) {
  return a.Code === o.Code && a.Name === o.Name && a.ParentCode === o.ParentCode && a.NodeType === o.NodeType && a.IsLeaf === o.IsLeaf && a.Sort === o.Sort;
}
function Ma(a, o) {
  const e = {};
  return a.Name !== o.Name && (e.Name = o.Name), a.ParentCode !== o.ParentCode && (e.ParentCode = o.ParentCode), a.NodeType !== o.NodeType && (e.NodeType = o.NodeType), a.IsLeaf !== o.IsLeaf && (e.IsLeaf = o.IsLeaf), a.Sort !== o.Sort && (e.Sort = o.Sort), e;
}
function Ua(a, o, e) {
  if (!a || a.length === 0) return [];
  const {
    codeField: t,
    nameField: n,
    parentCodeField: l,
    typeField: r,
    leafField: s,
    sortField: d,
    extraFields: f,
    rootParentCode: c
  } = o, b = (e == null ? void 0 : e.maxLevel) ?? 0, w = e == null ? void 0 : e.startFromCode, y = (e == null ? void 0 : e.currentLevel) ?? 0;
  if (b > 0 && y >= b) return [];
  const p = /* @__PURE__ */ new Map(), A = [];
  for (const h of a) {
    const x = ue(h, t), K = ue(h, n), ee = ue(h, l) ?? null, le = r ? ue(h, r) : void 0, re = s ? ue(h, s) : void 0, se = d ? ue(h, d) : void 0;
    let F;
    if (f && f.length > 0) {
      F = {};
      for (const q of f)
        F[q] = ue(h, q);
    }
    const E = {
      Code: x,
      Name: K,
      ParentCode: ee,
      NodeType: le,
      IsLeaf: re,
      Sort: se,
      Extra: F,
      Children: [],
      Raw: h
    };
    p.set(x, E);
  }
  for (const h of p.values())
    if (w && h.Code === w)
      A.push(h);
    else if (!w && (h.ParentCode === c || h.ParentCode === null || h.ParentCode === ""))
      A.push(h);
    else {
      const x = p.get(h.ParentCode ?? "");
      x && (x.Children = [...x.Children ?? [], h]);
    }
  return A.sort((h, x) => (h.Sort ?? 0) - (x.Sort ?? 0)), A;
}
function Ia(a, o, e) {
  const {
    codeField: t,
    nameField: n,
    parentCodeField: l,
    typeField: r,
    leafField: s,
    sortField: d,
    extraFields: f
  } = o, c = ue(a, t), b = ue(a, n), w = ue(a, l) ?? null, y = r ? ue(r, r) : void 0, p = s ? ue(s, s) : void 0, A = d ? ue(a, d) : void 0;
  let h;
  if (f && f.length > 0) {
    h = {};
    for (const x of f)
      h[x] = ue(a, x);
  }
  return {
    Code: c,
    Name: b,
    ParentCode: w,
    NodeType: y,
    IsLeaf: p,
    Sort: A,
    Extra: h,
    Children: [],
    Raw: a
  };
}
function Oa(a, o) {
  const e = {
    [o.codeField]: a.Code,
    [o.nameField]: a.Name,
    [o.parentCodeField]: a.ParentCode
  };
  return o.typeField && a.NodeType && (e[o.typeField] = a.NodeType), o.sortField && a.Sort !== void 0 && (e[o.sortField] = a.Sort), e;
}
function ot(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a), o;
}
function at(a) {
  const o = [], e = (t) => {
    for (const n of t)
      o.push(n), n.Children && n.Children.length > 0 && e(n.Children);
  };
  return e(a.Children ?? []), o;
}
function Ka(a) {
  return [a, ...at(a)];
}
function Ke(a, o) {
  const e = [];
  let t = a;
  for (; t && t.ParentCode; ) {
    const n = je(o, t.ParentCode);
    if (n)
      e.unshift(n), t = n;
    else
      break;
  }
  return e;
}
function ja(a, o) {
  return [...Ke(o, a).map((t) => t.Code), o.Code];
}
function Ya(a, o) {
  return [...Ke(o, a).map((t) => t.Name), o.Name];
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
function vt(a, o) {
  for (const e of a) {
    if (o(e)) return e;
    if (e.Children && e.Children.length > 0) {
      const t = vt(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function nt(a, o) {
  const e = [], t = (n) => {
    for (const l of n)
      o(l) && e.push(l), l.Children && l.Children.length > 0 && t(l.Children);
  };
  return t(a), e;
}
function Wa(a, o) {
  return o.ParentCode ? je(a, o.ParentCode) : null;
}
function qa(a, o) {
  return nt(a, (e) => e.NodeType === o);
}
function bt(a, o) {
  if (!o || !o.trim()) return [];
  const e = o.toLowerCase();
  return nt(a, (t) => t.Name.toLowerCase().includes(e));
}
function Ga(a, o) {
  const e = bt(a, o), t = /* @__PURE__ */ new Set();
  for (const n of e)
    t.add(n), Ke(n, a).forEach((r) => t.add(r));
  return Array.from(t);
}
function Ha(a, o) {
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
function Xa(a) {
  return at(a).length;
}
function Ja(a) {
  const o = (e, t) => {
    if (e.length === 0) return t;
    let n = t;
    for (const l of e)
      l.Children && l.Children.length > 0 && (n = Math.max(n, o(l.Children, t + 1)));
    return n;
  };
  return o(a, 0);
}
function Za(a) {
  return ot(a).length;
}
function Qa(a, o) {
  const e = [], t = (n, l) => {
    for (const r of n)
      l === o && e.push(r), r.Children && r.Children.length > 0 && t(r.Children, l + 1);
  };
  return t(a, 0), e;
}
function en(a) {
  const o = [], e = ot(a), t = new Set(e.map((l) => l.Code));
  for (const l of e)
    !l.Code && l.Code !== null && o.push(`节点 ${l.Name} 的 Code 为空`);
  const n = /* @__PURE__ */ new Map();
  for (const l of e)
    n.set(l.Code, (n.get(l.Code) ?? 0) + 1);
  for (const [l, r] of n)
    r > 1 && o.push(`Code "${l}" 重复 ${r} 次`);
  for (const l of e)
    l.ParentCode != null && l.ParentCode !== "" && !t.has(l.ParentCode) && o.push(`节点 "${l.Name}" 的 ParentCode "${l.ParentCode}" 不存在`);
  return Ct(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function Ct(a) {
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
function ue(a, o) {
  if (!a || !o) return;
  const e = o.split(".");
  let t = a;
  for (const n of e) {
    if (t == null) return;
    t = t[n];
  }
  return t;
}
const Yn = {
  // 构造
  buildTree: Ua,
  entityToNode: Ia,
  nodeToEntity: Oa,
  flattenTree: ot,
  // 遍历/查询
  getDescendants: at,
  getDescendantsWithSelf: Ka,
  getAncestors: Ke,
  getPath: ja,
  getPathNames: Ya,
  findNode: je,
  findNodeBy: vt,
  findNodesBy: nt,
  getParent: Wa,
  // 过滤/搜索
  filterByType: qa,
  search: bt,
  searchWithAncestors: Ga,
  filterTree: Ha,
  // 统计
  getChildrenCount: Xa,
  getDepth: Ja,
  getTotalCount: Za,
  getNodesAtLevel: Qa,
  // 验证
  validate: en,
  hasCycle: Ct
};
export {
  yt as AssociationTreeCore,
  Ln as CheckTreeCore,
  En as LinkTableCore,
  Je as MENU_CHANGED_EVENT,
  Mn as OrgNodeTypeExamples,
  za as SingleTableCore,
  Na as TreeSide,
  Vn as TreeTableCore,
  Vn as TreeTableLogic,
  ft as YzhApiClient,
  pn as YzhCard,
  sn as YzhDialog,
  hn as YzhEmptyState,
  Kt as YzhForm,
  nn as YzhFormDialog,
  ln as YzhPageLayout,
  no as YzhPagination,
  Jt as YzhSearchBar,
  fn as YzhStatusBadge,
  ut as YzhTable,
  oo as YzhToolbar,
  ct as YzhTree,
  un as YzhTreeTable,
  cn as YzhTreeTableCheckSelector,
  rn as YzhTreeTableLayout,
  dn as YzhTreeTableSelector,
  $a as addNode,
  Ua as buildTree,
  wn as deleteStorageFile,
  Kn as diff,
  Ia as entityToNode,
  kn as fileExists,
  qa as filterByType,
  Ha as filterTree,
  je as findNode,
  vt as findNodeBy,
  nt as findNodesBy,
  Ba as flatten,
  ot as flattenTree,
  Ke as getAncestors,
  Xa as getChildrenCount,
  Ja as getDepth,
  at as getDescendants,
  Ka as getDescendantsWithSelf,
  Cn as getFileUrl,
  Qa as getNodesAtLevel,
  Wa as getParent,
  ja as getPath,
  Ya as getPathNames,
  Za as getTotalCount,
  Ct as hasCycle,
  Tn as listFiles,
  ha as mapControlType,
  pa as mapSearchControlType,
  fa as mapSearchType,
  On as mergeRoots,
  Un as moveSubtree,
  Oa as nodeToEntity,
  An as notifyMenuChanged,
  Fn as onMenuChanged,
  Xe as pascalCaseFormData,
  Ra as removeSubtree,
  Pn as rowToFormData,
  bt as search,
  Ga as searchWithAncestors,
  pt as toCamelCase,
  ma as toFormFields,
  et as toFormLayoutCols,
  Fa as toPascalCase,
  yn as toRowActionButtons,
  ht as toRowActions,
  rt as toSearchFields,
  ya as toTableColumns,
  ga as toToolbarActions,
  mn as toTreeActions,
  pe as tokenStore,
  gn as treeItemToNode,
  Yn as treeUtils,
  In as updateNode,
  vn as uploadFile,
  bn as uploadFileBatch,
  xn as useAuth,
  Sn as useAuthState,
  Rn as useCheckTree,
  Nn as useConfirm,
  Bn as useLinkTable,
  _n as useMenuTree,
  Dn as useSingleTable,
  zn as useTable,
  $n as useTreeTable,
  en as validate,
  jn as validateTreeOps,
  Ae as yzhApi
};
