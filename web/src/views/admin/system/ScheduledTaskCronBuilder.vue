<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="cron-builder">
    <div class="cron-builder-row">
      <el-radio-group v-model="localForm.mode">
        <el-radio-button value="daily">每天</el-radio-button>
        <el-radio-button value="weekly">每周</el-radio-button>
        <el-radio-button value="monthly">每月</el-radio-button>
        <el-radio-button value="interval">按间隔</el-radio-button>
      </el-radio-group>
    </div>

    <div v-if="localForm.mode === 'daily'" class="cron-builder-grid">
      <span>每天</span>
      <el-select v-model="localForm.hour" style="width: 100px">
        <el-option v-for="n in 24" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 时`" :value="n - 1" />
      </el-select>
      <el-select v-model="localForm.minute" style="width: 100px">
        <el-option v-for="n in 60" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 分`" :value="n - 1" />
      </el-select>
      <span>执行</span>
    </div>

    <div v-else-if="localForm.mode === 'weekly'" class="cron-builder-grid">
      <span>每周</span>
      <el-select v-model="localForm.weekDay" style="width: 120px">
        <el-option label="周日" :value="0" />
        <el-option label="周一" :value="1" />
        <el-option label="周二" :value="2" />
        <el-option label="周三" :value="3" />
        <el-option label="周四" :value="4" />
        <el-option label="周五" :value="5" />
        <el-option label="周六" :value="6" />
      </el-select>
      <el-select v-model="localForm.hour" style="width: 100px">
        <el-option v-for="n in 24" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 时`" :value="n - 1" />
      </el-select>
      <el-select v-model="localForm.minute" style="width: 100px">
        <el-option v-for="n in 60" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 分`" :value="n - 1" />
      </el-select>
      <span>执行</span>
    </div>

    <div v-else-if="localForm.mode === 'monthly'" class="cron-builder-grid">
      <span>每月</span>
      <el-select v-model="localForm.monthDay" style="width: 120px">
        <el-option v-for="n in 31" :key="n" :label="`${n} 日`" :value="n" />
      </el-select>
      <el-select v-model="localForm.hour" style="width: 100px">
        <el-option v-for="n in 24" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 时`" :value="n - 1" />
      </el-select>
      <el-select v-model="localForm.minute" style="width: 100px">
        <el-option v-for="n in 60" :key="n - 1" :label="`${String(n - 1).padStart(2, '0')} 分`" :value="n - 1" />
      </el-select>
      <span>执行</span>
    </div>

    <div v-else class="cron-builder-grid">
      <span>每隔</span>
      <el-input-number v-model="localForm.intervalMinutes" :min="1" :max="1440" :step="5" style="width: 140px" />
      <span>分钟执行一次</span>
    </div>

    <div class="cron-preview">
      <el-tag type="primary" effect="light">{{ humanText }}</el-tag>
      <code>{{ modelValue }}</code>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { buildCronFromForm, cronToHuman, parseCronToForm, type CronFormValue } from './scheduled-task-cron'

const props = defineProps<{
  modelValue: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const localForm = reactive<CronFormValue>(parseCronToForm(props.modelValue))

watch(() => props.modelValue, (val) => {
  const next = parseCronToForm(val)
  localForm.mode = next.mode
  localForm.hour = next.hour
  localForm.minute = next.minute
  localForm.weekDay = next.weekDay
  localForm.monthDay = next.monthDay
  localForm.intervalMinutes = next.intervalMinutes
})

watch(localForm, () => {
  emit('update:modelValue', buildCronFromForm(localForm))
}, { deep: true })

const humanText = computed(() => cronToHuman(props.modelValue))
</script>

<style scoped>
.cron-builder {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.cron-builder-row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.cron-builder-grid {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.cron-preview {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  color: var(--el-text-color-secondary);
}

.cron-preview code {
  padding: 4px 8px;
  border-radius: 4px;
  background: var(--el-fill-color-light);
}
</style>
