import { yzhApi } from '@yzh-core/api/client'
import type { Page, PageParams } from '@yzh-core/types'

export interface ReportTemplate {
  id: number
  code?: string
  templateName: string
  isDefault: boolean
  remark?: string
  chapterCount?: number
  orgCode?: string
  standardCode?: string
  phaseCode?: string
  createDate?: string
}

export interface ReportSection {
  id: number
  code?: string
  sectionName: string
  sectionNameEn?: string
  sortOrder: number
  isActive: boolean
  remark?: string
  reportCode?: string
  orgCode?: string
}

export async function getReportTemplatePage(params: PageParams, filters?: any): Promise<Page<ReportTemplate>> {
  return yzhApi.post<Page<ReportTemplate>>('/api/ReportDefinition/Template/getPageData', params, { params: filters })
}

export async function getReportTemplate(code: string): Promise<ReportTemplate> {
  return yzhApi.get<ReportTemplate>(`/api/ReportDefinition/Template/getDetail?code=${code}`)
}

export async function saveReportTemplate(data: Partial<ReportTemplate>): Promise<any> {
  return yzhApi.post('/api/ReportDefinition/Template/save', data)
}

export async function deleteReportTemplate(id: number): Promise<any> {
  return yzhApi.post(`/api/ReportDefinition/Template/delete?id=${id}`)
}

export async function getReportSectionList(reportCode: string): Promise<ReportSection[]> {
  return yzhApi.get<ReportSection[]>(`/api/ReportDefinition/Section/list?reportCode=${reportCode}`)
}

export async function saveReportSection(data: Partial<ReportSection>): Promise<any> {
  return yzhApi.post('/api/ReportDefinition/Section/save', data)
}

export async function deleteReportSection(id: number): Promise<any> {
  return yzhApi.post(`/api/ReportDefinition/Section/delete?id=${id}`)
}
