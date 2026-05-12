<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="hero-bg" aria-hidden="true">
    <!-- 渐变底色 -->
    <div class="hero-bg__gradient"></div>

    <!-- 科技网格（双层：主线 + 细线） -->
    <svg class="hero-bg__grid" width="100%" height="100%" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <!-- 细线网格 -->
        <pattern id="hbg-sm" width="40" height="40" patternUnits="userSpaceOnUse">
          <path d="M40 0H0v40" fill="none" stroke="rgba(148,163,184,0.04)" stroke-width="0.5" />
        </pattern>
        <!-- 粗线网格 -->
        <pattern id="hbg-lg" width="120" height="120" patternUnits="userSpaceOnUse">
          <path d="M120 0H0v120" fill="none" stroke="rgba(148,163,184,0.09)" stroke-width="0.8" />
          <!-- 交叉点发光圆点 -->
          <circle cx="0" cy="0" r="1.5" fill="rgba(96,165,250,0.25)" />
          <circle cx="120" cy="0" r="1.5" fill="rgba(96,165,250,0.25)" />
          <circle cx="0" cy="120" r="1.5" fill="rgba(96,165,250,0.25)" />
          <circle cx="120" cy="120" r="1.5" fill="rgba(96,165,250,0.25)" />
        </pattern>
        <!-- 径向遮罩让网格从中心明亮向四周淡化 -->
        <radialGradient id="hbg-mask" cx="50%" cy="40%" r="65%">
          <stop offset="0%" stop-color="white" stop-opacity="1" />
          <stop offset="100%" stop-color="white" stop-opacity="0" />
        </radialGradient>
        <mask id="hbg-grid-mask">
          <rect width="100%" height="100%" fill="url(#hbg-mask)" />
        </mask>
      </defs>
      <g mask="url(#hbg-grid-mask)">
        <rect width="100%" height="100%" fill="url(#hbg-sm)" />
        <rect width="100%" height="100%" fill="url(#hbg-lg)" />
      </g>
    </svg>

    <!-- 水平扫描光线 -->
    <div class="hero-bg__scanline"></div>

    <!-- 垂直脉冲光柱 -->
    <div class="hero-bg__beam hero-bg__beam--1"></div>
    <div class="hero-bg__beam hero-bg__beam--2"></div>

    <!-- 光晕球（更亮） -->
    <div class="hero-bg__orb hero-bg__orb--1 gw-float"></div>
    <div class="hero-bg__orb hero-bg__orb--2 gw-float-slow"></div>
    <div class="hero-bg__orb hero-bg__orb--3 gw-float"></div>

    <!-- 浮动微粒 -->
    <div class="hero-bg__particles">
      <span v-for="n in 12" :key="n" class="hero-bg__dot" :style="dotStyle(n)"></span>
    </div>

    <!-- 噪点纹理 -->
    <svg class="hero-bg__noise" width="100%" height="100%">
      <filter id="hbg-noise"><feTurbulence type="fractalNoise" baseFrequency="0.65" numOctaves="3" stitchTiles="stitch" /><feColorMatrix type="saturate" values="0" /></filter>
      <rect width="100%" height="100%" filter="url(#hbg-noise)" opacity="0.025" />
    </svg>
  </div>
</template>

<script setup lang="ts">
// 为浮动微粒生成随机位置和动画参数
function dotStyle(n: number) {
  const seed = n * 7 + 3
  const left = ((seed * 17) % 100)
  const top = ((seed * 13) % 90) + 5
  const size = 1.5 + (n % 3)
  const dur = 4 + (n % 5) * 2
  const delay = -(n % 7) * 1.2
  const opacity = 0.15 + (n % 4) * 0.1
  return {
    left: `${left}%`,
    top: `${top}%`,
    width: `${size}px`,
    height: `${size}px`,
    '--dot-dur': `${dur}s`,
    '--dot-delay': `${delay}s`,
    opacity,
  }
}
</script>

<style scoped>
.hero-bg {
  position: absolute; inset: 0; overflow: hidden; z-index: 0;
  background: linear-gradient(160deg, #020617 0%, #0a1628 30%, #0f172a 50%, #131b3a 70%, #1a1040 100%);
}

/* 渐变层：侧面蓝紫氛围光，不溢出顶部和底部 */
.hero-bg__gradient {
  position: absolute; inset: 0;
  background:
    radial-gradient(ellipse 80% 50% at 50% 35%, rgba(59,130,246,0.18), transparent 60%),
    radial-gradient(ellipse 60% 55% at 85% 55%, rgba(124,58,237,0.14), transparent 55%),
    radial-gradient(ellipse 50% 40% at 15% 60%, rgba(6,182,212,0.08), transparent 50%);
}

/* 科技网格 */
.hero-bg__grid { position: absolute; inset: 0; }

/* 水平扫描光线 */
.hero-bg__scanline {
  position: absolute; left: 0; right: 0; height: 1px;
  background: linear-gradient(90deg, transparent 0%, rgba(96,165,250,0.5) 20%, rgba(96,165,250,0.8) 50%, rgba(96,165,250,0.5) 80%, transparent 100%);
  box-shadow: 0 0 20px 4px rgba(96,165,250,0.15);
  animation: hbg-scan 6s ease-in-out infinite;
  opacity: 0.6;
}
@keyframes hbg-scan {
  0%, 100% { top: 10%; opacity: 0; }
  5%  { opacity: 0.6; }
  50% { top: 85%; opacity: 0.4; }
  95% { opacity: 0; }
}

/* 垂直脉冲光柱 */
.hero-bg__beam {
  position: absolute; top: 0; width: 1px; height: 100%;
  opacity: 0;
  animation: hbg-beam 8s ease-in-out infinite;
}
.hero-bg__beam--1 {
  left: 30%;
  background: linear-gradient(180deg, transparent 0%, rgba(96,165,250,0.3) 30%, rgba(96,165,250,0.5) 50%, rgba(96,165,250,0.3) 70%, transparent 100%);
  box-shadow: 0 0 12px 2px rgba(96,165,250,0.1);
  animation-delay: 0s;
}
.hero-bg__beam--2 {
  left: 72%;
  background: linear-gradient(180deg, transparent 0%, rgba(139,92,246,0.2) 30%, rgba(139,92,246,0.4) 50%, rgba(139,92,246,0.2) 70%, transparent 100%);
  box-shadow: 0 0 12px 2px rgba(139,92,246,0.08);
  animation-delay: -4s;
}
@keyframes hbg-beam {
  0%, 100% { opacity: 0; }
  15%, 85% { opacity: 0.5; }
  50%      { opacity: 0.8; }
}

/* 光晕球 */
.hero-bg__orb { position: absolute; border-radius: 50%; filter: blur(80px); pointer-events: none; }
.hero-bg__orb--1 {
  width: 550px; height: 550px; top: -140px; left: -100px;
  background: radial-gradient(circle, rgba(59,130,246,0.22), transparent 70%);
}
.hero-bg__orb--2 {
  width: 420px; height: 420px; top: 35%; right: -80px;
  background: radial-gradient(circle, rgba(124,58,237,0.18), transparent 70%);
  animation-delay: -4s;
}
.hero-bg__orb--3 {
  width: 380px; height: 380px; bottom: -100px; left: 25%;
  background: radial-gradient(circle, rgba(6,182,212,0.15), transparent 70%);
  animation-delay: -2s;
}

/* 浮动微粒 */
.hero-bg__particles { position: absolute; inset: 0; pointer-events: none; }
.hero-bg__dot {
  position: absolute; border-radius: 50%;
  background: rgba(148,163,184,0.6);
  animation: hbg-dot var(--dot-dur, 5s) ease-in-out infinite;
  animation-delay: var(--dot-delay, 0s);
}
@keyframes hbg-dot {
  0%, 100% { transform: translateY(0) scale(1); opacity: var(--dot-opacity, 0.3); }
  50%      { transform: translateY(-20px) scale(1.5); opacity: calc(var(--dot-opacity, 0.3) + 0.2); }
}

/* 噪点 */
.hero-bg__noise { position: absolute; inset: 0; pointer-events: none; mix-blend-mode: overlay; }
</style>
