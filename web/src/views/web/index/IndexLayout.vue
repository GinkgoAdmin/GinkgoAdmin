<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <MaintenanceGate>
    <div class="index-layout">
      <IndexHeader />
      <main class="main-content">
        <router-view />
      </main>
      <IndexFooter />
    </div>
  </MaintenanceGate>
</template>

<script setup lang="ts">
import IndexHeader from './components/IndexHeader.vue'
import IndexFooter from './components/IndexFooter.vue'
import MaintenanceGate from '../components/MaintenanceGate.vue'
</script>

<style scoped>
.index-layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: #ffffff;
  color: #1f2937;
}

.main-content {
  flex: 1;
  position: relative;
}

.index-layout * {
  box-sizing: border-box;
}
</style>
