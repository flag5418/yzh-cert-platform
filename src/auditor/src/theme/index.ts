import type { GlobalThemeOverrides } from 'naive-ui'

/**
 * 审核员端主题配置
 * 品牌主色与 styles/global.css 中的 --yzh-primary 保持一致
 */
export const brandColor = '#1a5fb4'

export const themeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: brandColor,
    primaryColorHover: '#3584e4',
    primaryColorPressed: '#144a8f',
    primaryColorSuppl: '#3584e4',
    successColor: '#26a269',
    warningColor: '#e5a50a',
    errorColor: '#c01c28',
    borderRadius: '6px'
  }
}
