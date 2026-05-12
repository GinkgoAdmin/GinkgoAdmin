<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="address-picker">
    <el-cascader
      v-model="selectedCodes"
      :options="regionData"
      :props="cascaderProps"
      :placeholder="placeholder"
      :clearable="clearable"
      :disabled="disabled"
      :size="size"
      style="width: 100%"
      @change="handleChange"
    />
    <el-input
      v-if="showDetail"
      v-model="detailAddress"
      :placeholder="detailPlaceholder"
      :disabled="disabled"
      :size="size"
      style="margin-top: 8px"
      @input="handleDetailChange"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * 中国省市区三级联动地址选择组件
 * 基于 element-china-area-data 库
 * 
 * 使用示例:
 * <AddressPicker v-model="address" :show-detail="true" />
 * 
 * 数据格式:
 * {
 *   province: '北京市',
 *   city: '市辖区', 
 *   district: '朝阳区',
 *   address: '建国路88号',
 *   provinceCode: '11',
 *   cityCode: '1101',
 *   districtCode: '110105'
 * }
 */
import { ref, watch } from 'vue'
import { regionData, codeToText } from 'element-china-area-data'

export interface AddressValue {
  province?: string
  city?: string
  district?: string
  address?: string
  provinceCode?: string
  cityCode?: string
  districtCode?: string
}

interface RegionItem {
  value: string
  label: string
  children?: RegionItem[]
}

const props = withDefaults(defineProps<{
  modelValue?: AddressValue | null
  placeholder?: string
  detailPlaceholder?: string
  showDetail?: boolean
  clearable?: boolean
  disabled?: boolean
  size?: 'large' | 'default' | 'small'
}>(), {
  placeholder: '请选择省/市/区',
  detailPlaceholder: '请输入详细地址',
  showDetail: true,
  clearable: true,
  disabled: false,
  size: 'default'
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: AddressValue | null): void
  (e: 'change', value: AddressValue | null): void
}>()

const selectedCodes = ref<string[]>([])
const detailAddress = ref('')

const cascaderProps = {
  expandTrigger: 'hover' as const
}

function codeToName(code: string): string {
  return (codeToText as Record<string, string>)[code] || ''
}

watch(() => props.modelValue, (newVal) => {
  if (newVal) {
    if (newVal.provinceCode && newVal.cityCode && newVal.districtCode) {
      selectedCodes.value = [newVal.provinceCode, newVal.cityCode, newVal.districtCode]
    } else if (newVal.province || newVal.city || newVal.district) {
      const codes = findCodesByNames(newVal.province, newVal.city, newVal.district)
      selectedCodes.value = codes
    } else {
      selectedCodes.value = []
    }
    detailAddress.value = newVal.address || ''
  } else {
    selectedCodes.value = []
    detailAddress.value = ''
  }
}, { immediate: true, deep: true })

function findCodesByNames(province?: string, city?: string, district?: string): string[] {
  const codes: string[] = []
  if (!province) return codes
  
  const regions = regionData as unknown as RegionItem[]
  
  const provinceItem = regions.find((p) => p.label === province || p.label.includes(province))
  if (!provinceItem) return codes
  codes.push(provinceItem.value)
  
  if (!city || !provinceItem.children) return codes
  
  const cityItem = provinceItem.children.find((c) => c.label === city || c.label.includes(city))
  if (!cityItem) return codes
  codes.push(cityItem.value)
  
  if (!district || !cityItem.children) return codes
  
  const districtItem = cityItem.children.find((d) => d.label === district || d.label.includes(district))
  if (districtItem) {
    codes.push(districtItem.value)
  }
  
  return codes
}

function handleChange(codes: string[]) {
  emitValue(codes, detailAddress.value)
}

function handleDetailChange(value: string) {
  emitValue(selectedCodes.value, value)
}

function emitValue(codes: string[], detail: string) {
  if (codes.length === 0 && !detail) {
    emit('update:modelValue', null)
    emit('change', null)
    return
  }
  
  const provinceName = codes[0] ? codeToName(codes[0]) : ''
  const cityName = codes[1] ? codeToName(codes[1]) : ''
  const districtName = codes[2] ? codeToName(codes[2]) : ''
  
  const value: AddressValue = {
    province: provinceName || undefined,
    city: cityName || undefined,
    district: districtName || undefined,
    address: detail || undefined,
    provinceCode: codes[0] || undefined,
    cityCode: codes[1] || undefined,
    districtCode: codes[2] || undefined
  }
  
  emit('update:modelValue', value)
  emit('change', value)
}
</script>

<style scoped>
.address-picker {
  width: 100%;
}
</style>
