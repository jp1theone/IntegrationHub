import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { api, type Page } from '../services/api'

type Kind = 'clients'|'providers'|'integrations'|'incidents'|'migrations'|'users'
interface Field { name:string; label:string; kind?:'email'|'url'|'number'|'select'|'textarea'|'password'; required?:boolean; options?:string[]; source?:string; min?:number; max?:number }
const specs:Record<Kind,{title:string;fields:Field[]}>= {
  clients:{title:'Create client',fields:[{name:'code',label:'Client code',required:true},{name:'name',label:'Name',required:true},{name:'legalName',label:'Legal name',required:true},{name:'email',label:'Email',kind:'email',required:true},{name:'phone',label:'Phone'}]},
  providers:{title:'Register provider',fields:[{name:'code',label:'Provider code',required:true},{name:'name',label:'Name',required:true},{name:'description',label:'Description',kind:'textarea'},{name:'type',label:'Provider adapter',kind:'select',required:true,options:['FudoMock','ToteatMock','GenericRestMock','Fudo','Toteat','GenericRest']},{name:'baseUrl',label:'Base URL (no credentials)',kind:'url'},{name:'timeoutSeconds',label:'Timeout (seconds)',kind:'number',required:true,min:1,max:300}]},
  integrations:{title:'Create integration',fields:[{name:'clientId',label:'Client',kind:'select',required:true,source:'clients'},{name:'providerId',label:'Provider',kind:'select',required:true,source:'providers'},{name:'code',label:'Integration code',required:true},{name:'name',label:'Name',required:true},{name:'description',label:'Description',kind:'textarea'},{name:'type',label:'Integration type',kind:'select',required:true,options:['RestApi','Webhook','SdkSimulation']},{name:'environment',label:'Environment',kind:'select',required:true,options:['Development','Testing','Staging','Production']}]},
  incidents:{title:'Create incident',fields:[{name:'integrationId',label:'Integration',kind:'select',required:true,source:'integrations'},{name:'title',label:'Title',required:true},{name:'description',label:'Description',kind:'textarea',required:true},{name:'severity',label:'Severity',kind:'select',required:true,options:['Low','Medium','High','Critical']},{name:'errorCode',label:'Error code'}]},
  migrations:{title:'Plan migration',fields:[{name:'clientId',label:'Client',kind:'select',required:true,source:'clients'},{name:'integrationId',label:'Integration',kind:'select',required:true,source:'integrations'},{name:'sourceProvider',label:'Source provider',required:true},{name:'targetProvider',label:'Target provider',required:true},{name:'notes',label:'Notes',kind:'textarea'}]},
  users:{title:'Create user',fields:[{name:'email',label:'Email',kind:'email',required:true},{name:'fullName',label:'Full name',required:true},{name:'password',label:'Temporary password',kind:'password',required:true},{name:'role',label:'Role',kind:'select',required:true,options:['Administrator','Integration Analyst','Support Analyst','ReadOnly']}]},
}

export default function CreateRecordDialog({kind,onClose}:{kind:Kind;onClose:()=>void}) {
  const spec=specs[kind]
  const queryClient=useQueryClient()
  const optionQueries=useQuery({queryKey:['create-options',kind],enabled:spec.fields.some(field=>field.source),queryFn:async()=>{
    const sources=[...new Set(spec.fields.map(field=>field.source).filter(Boolean))] as string[]
    const entries=await Promise.all(sources.map(async source=>[source,(await api.get<Page<{id:string;name:string;code?:string}>>(`/${source}`,{params:{page:1,pageSize:100}})).data.items] as const))
    return Object.fromEntries(entries) as Record<string,{id:string;name:string;code?:string}[]>
  }})
  const schema=useMemo(()=>z.object(Object.fromEntries(spec.fields.map(field=>[field.name,field.required?(field.kind==='email'?z.email('Enter a valid email address.'):z.string().min(1,`${field.label} is required.`)):z.string().optional()]))),[spec])
  const {register,handleSubmit,formState:{errors,isSubmitting}}=useForm<Record<string,string|undefined>>({resolver:zodResolver(schema),defaultValues:{timeoutSeconds:'30'}})
  const create=useMutation({mutationFn:async(values:Record<string,string|undefined>)=>api.post(`/${kind}`,{...values,...(kind==='providers'?{timeoutSeconds:Number(values.timeoutSeconds)}:{})}),onSuccess:async()=>{await queryClient.invalidateQueries({queryKey:['records',`/${kind}`]});onClose()}})
  return <div className="dialog-backdrop" role="presentation" onMouseDown={event=>{if(event.target===event.currentTarget)onClose()}}><section className="form-dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title"><div className="dialog-header"><div><span className="eyebrow">WORKSPACE ACTION</span><h2 id="dialog-title">{spec.title}</h2></div><button className="icon-btn" onClick={onClose} aria-label="Close"><X size={18}/></button></div>{optionQueries.isError&&<div className="error-banner">Could not load related records. Try again in a moment.</div>}{create.isError&&<div className="error-banner">{getError(create.error)}</div>}<form onSubmit={handleSubmit(values=>create.mutate(values))}><div className="form-grid">{spec.fields.map(field=><label className={`form-field ${field.kind==='textarea'?'field-wide':''}`} key={field.name}><span>{field.label}{field.required?' *':''}</span>{field.kind==='select'?<select {...register(field.name)} defaultValue=""><option value="" disabled>Select {field.label.toLowerCase()}</option>{field.options?.map(value=><option key={value} value={value}>{value}</option>)}{field.source&&optionQueries.data?.[field.source]?.map(item=><option key={item.id} value={item.id}>{item.code?`${item.code} · `:''}{item.name}</option>)}</select>:field.kind==='textarea'?<textarea rows={3} {...register(field.name)}/>:<input type={field.kind??'text'} min={field.min} max={field.max} {...register(field.name)} />}{errors[field.name]?.message&&<small className="field-error">{String(errors[field.name]?.message)}</small>}</label>)}</div><div className="dialog-actions"><button type="button" className="button button-quiet" onClick={onClose}>Cancel</button><button type="submit" className="button button-primary" disabled={isSubmitting||create.isPending||optionQueries.isLoading}>{create.isPending?'Saving…':'Create record'}</button></div></form></section></div>
}

function getError(error:unknown){if(error&&typeof error==='object'&&'response'in error){const response=(error as {response?:{data?:{detail?:string;title?:string}}}).response?.data;return response?.detail??response?.title??'The record could not be saved.'}return 'The record could not be saved.'}
