import { yzhApi } from '@yzh-core/api/client'

export interface PromptTemplate {
  id: number
  promptCode: string
  promptName: string
  promptType: string
  skillTarget?: string
  template: string
  description?: string
  isActive: boolean
  createDate?: string
  creator?: string
}

export async function getPromptList(params: any): Promise<PromptTemplate[]> {
  return yzhApi.post<PromptTemplate[]>('/api/PromptTemplate/getList', params)
}

export async function savePrompt(data: Partial<PromptTemplate>): Promise<any> {
  return yzhApi.post('/api/PromptTemplate/save', data)
}

export async function deletePrompt(promptCode: string): Promise<any> {
  return yzhApi.post(`/api/PromptTemplate/delete?promptCode=${promptCode}`)
}

export async function activatePrompt(promptCode: string): Promise<any> {
  return yzhApi.post(`/api/PromptTemplate/activate?promptCode=${promptCode}`)
}
