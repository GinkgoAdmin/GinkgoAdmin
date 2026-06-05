<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="location-picker">
    <div class="location-inputs">
      <el-input-number 
        v-model="longitude" 
        :precision="7" 
        :step="0.0001" 
        :min="-180"
        :max="180"
        placeholder="经度"
        :disabled="disabled"
        style="width: 45%"
        @change="handleInputChange"
      />
      <el-input-number 
        v-model="latitude" 
        :precision="7" 
        :step="0.0001"
        :min="-90"
        :max="90"
        placeholder="纬度"
        :disabled="disabled"
        style="width: 45%"
        @change="handleInputChange"
      />
      <el-button 
        :icon="Location" 
        :disabled="disabled"
        @click="showMapDialog"
      >
        选择
      </el-button>
    </div>

    <!-- 地图选择对话框 -->
    <el-dialog
      v-model="dialogVisible"
      title="选择位置"
      width="800px"
      :close-on-click-modal="false"
      @opened="initMap"
    >
      <div class="map-container">
        <div class="map-search">
          <el-input
            v-model="searchKeyword"
            :placeholder="searchPlaceholder"
            clearable
            @keyup.enter="searchLocation"
          >
            <template #append>
              <el-button :icon="Search" @click="searchLocation" />
            </template>
          </el-input>
        </div>
        <div ref="mapRef" class="map-view"></div>
        <div class="map-info">
          <span>经度: {{ tempLongitude?.toFixed(7) || '-' }}</span>
          <span>纬度: {{ tempLatitude?.toFixed(7) || '-' }}</span>
          <span class="tip">点击地图选择位置，或拖动标记调整</span>
        </div>
      </div>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmLocation">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
/**
 * 经纬度坐标选择组件
 * 基于 Leaflet；默认高德路网瓦片（无需 key），可切换 OpenStreetMap
 * 
 * 使用示例:
 * <LocationPicker v-model="location" />
 * 
 * 数据格式:
 * {
 *   longitude: 116.397428,
 *   latitude: 39.90923
 * }
 * 
 * 功能:
 * - 手动输入经纬度
 * - 点击地图选择位置
 * - 拖动标记调整位置
 * - 地址搜索（使用 Nominatim 开源服务）
 */
import { ref, watch, onUnmounted, nextTick, computed } from 'vue'
import { Location, Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

// 修复 Leaflet 默认图标问题
// @ts-ignore
delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

export interface LocationValue {
  longitude: number | null
  latitude: number | null
}

/** 底图类型：gaode=高德路网瓦片（国内无需 key）；osm=OpenStreetMap */
export type LocationPickerTileProvider = 'gaode' | 'osm'

const props = withDefaults(defineProps<{
  modelValue?: LocationValue | null
  disabled?: boolean
  defaultCenter?: [number, number]
  defaultZoom?: number
  /** 地图瓦片源，默认高德（无需 API Key） */
  tileProvider?: LocationPickerTileProvider
}>(), {
  disabled: false,
  defaultCenter: () => [116.397428, 39.90923], // 北京天安门
  defaultZoom: 12,
  tileProvider: 'gaode'
})

const searchPlaceholder = computed(() =>
  props.tileProvider === 'gaode'
    ? '输入地址关键词搜索（开源地理编码，选点后可在高德底图上微调）'
    : '输入地址搜索（使用 Nominatim 服务）'
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: LocationValue | null): void
  (e: 'change', value: LocationValue | null): void
}>()

const longitude = ref<number | null>(null)
const latitude = ref<number | null>(null)
const dialogVisible = ref(false)
const mapRef = ref<HTMLElement | null>(null)
const searchKeyword = ref('')

// 临时坐标（对话框中使用）
const tempLongitude = ref<number | null>(null)
const tempLatitude = ref<number | null>(null)

let map: L.Map | null = null
let marker: L.Marker | null = null

// 监听外部值变化
watch(() => props.modelValue, (newVal) => {
  if (newVal) {
    longitude.value = newVal.longitude
    latitude.value = newVal.latitude
  } else {
    longitude.value = null
    latitude.value = null
  }
}, { immediate: true, deep: true })

// 处理输入变化
function handleInputChange() {
  emitValue()
}

// 发送值变化
function emitValue() {
  const value: LocationValue | null = 
    (longitude.value !== null && latitude.value !== null)
      ? { longitude: longitude.value, latitude: latitude.value }
      : null
  emit('update:modelValue', value)
  emit('change', value)
}

// 显示地图对话框
function showMapDialog() {
  tempLongitude.value = longitude.value
  tempLatitude.value = latitude.value
  dialogVisible.value = true
}

// 初始化地图
function initMap() {
  destroyMap()
  nextTick(() => {
    if (!mapRef.value) return

    // 确定初始中心点
    const center: [number, number] = 
      (tempLatitude.value && tempLongitude.value)
        ? [tempLatitude.value, tempLongitude.value]
        : [props.defaultCenter[1], props.defaultCenter[0]]

    // 创建地图
    map = L.map(mapRef.value).setView(center, props.defaultZoom)

    // 高德路网瓦片（webrd 子域，无需 key）；或 OpenStreetMap
    const isGaode = props.tileProvider === 'gaode'
    L.tileLayer(
      isGaode
        ? 'https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}'
        : 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      {
        subdomains: isGaode ? ['1', '2', '3', '4'] : ['a', 'b', 'c'],
        attribution: isGaode
          ? '&copy; <a href="https://www.amap.com/">高德地图</a>'
          : '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: isGaode ? 18 : 19
      }
    ).addTo(map)

    // 如果有初始坐标，添加标记
    if (tempLatitude.value && tempLongitude.value) {
      addMarker(tempLatitude.value, tempLongitude.value)
    }

    // 点击地图选择位置
    map.on('click', (e: L.LeafletMouseEvent) => {
      const { lat, lng } = e.latlng
      tempLatitude.value = lat
      tempLongitude.value = lng
      addMarker(lat, lng)
    })
  })
}

// 添加/更新标记
function addMarker(lat: number, lng: number) {
  if (!map) return

  if (marker) {
    marker.setLatLng([lat, lng])
  } else {
    marker = L.marker([lat, lng], { draggable: true }).addTo(map)
    
    // 拖动标记更新坐标
    marker.on('dragend', () => {
      const pos = marker!.getLatLng()
      tempLatitude.value = pos.lat
      tempLongitude.value = pos.lng
    })
  }
}

// 搜索位置（使用 Nominatim 开源地理编码服务）
async function searchLocation() {
  if (!searchKeyword.value.trim()) return

  try {
    const response = await fetch(
      `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(searchKeyword.value)}&limit=1`,
      {
        headers: {
          'Accept-Language': 'zh-CN,zh'
        }
      }
    )
    const results = await response.json()

    if (results.length > 0) {
      const { lat, lon } = results[0]
      const latitude = parseFloat(lat)
      const longitude = parseFloat(lon)
      
      tempLatitude.value = latitude
      tempLongitude.value = longitude
      
      if (map) {
        map.setView([latitude, longitude], 15)
        addMarker(latitude, longitude)
      }
    } else {
      ElMessage.warning('未找到该地址')
    }
  } catch (error) {
    ElMessage.error('搜索失败，请稍后重试')
  }
}

// 确认选择
function confirmLocation() {
  longitude.value = tempLongitude.value
  latitude.value = tempLatitude.value
  emitValue()
  dialogVisible.value = false
}

// 清理地图
function destroyMap() {
  if (map) {
    map.remove()
    map = null
    marker = null
  }
}

// 对话框关闭时清理
watch(dialogVisible, (visible) => {
  if (!visible) {
    destroyMap()
  }
})

onUnmounted(() => {
  destroyMap()
})
</script>

<style scoped>
.location-picker {
  width: 100%;
}

.location-inputs {
  display: flex;
  gap: 8px;
  align-items: center;
}

.map-container {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.map-search {
  width: 100%;
}

.map-view {
  width: 100%;
  height: 400px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
}

.map-info {
  display: flex;
  gap: 20px;
  font-size: 14px;
  color: var(--el-text-color-secondary);
}

.map-info .tip {
  margin-left: auto;
  color: var(--el-text-color-placeholder);
}
</style>
