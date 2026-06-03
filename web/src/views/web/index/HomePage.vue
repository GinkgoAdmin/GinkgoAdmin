<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="index-home">
    <!-- 氛围背景：柔和蓝紫光晕 -->
    <div class="home-ambient" aria-hidden="true">
      <div class="orb orb--blue"></div>
      <div class="orb orb--violet"></div>
      <div class="orb orb--rose"></div>
      <div class="home-glow"></div>
    </div>

    <div class="home-inner">
      <section class="vision-section">
        <!-- 文案 -->
        <div class="vision-copy">
          <div class="eyebrow-pill">
            <span class="eyebrow-dot"></span>
            致每一位创造者
          </div>

          <h1 class="vision-title">
            <span class="title-line">把省下的时间，</span>
            <span class="title-line title-line--accent">还给山海与所爱</span>
          </h1>

          <p class="vision-lead">
            愿你能从屏幕里抬起头，赴一场久违的远行，
            陪一段被错过的时光——<br />
            让效率成全生活，让所爱不必久等。
          </p>

          <p class="moment-line" aria-label="美好瞬间">
            <template v-for="(m, i) in moments" :key="m">
              <span v-if="i > 0" class="moment-sep">·</span>
              <span class="moment-word">{{ m }}</span>
            </template>
          </p>
        </div>

        <!-- 图片：杂志式拼贴，图片不被遮挡 -->
        <div class="vision-gallery" aria-label="远方与归处">
          <figure
            v-for="(item, index) in gallery"
            :key="item.alt"
            class="gallery-card"
            :class="`gallery-card--${index + 1}`"
            :style="{ '--delay': `${0.15 + index * 0.1}s` }"
          >
            <div class="gallery-frame">
              <img :src="item.src" :alt="item.alt" loading="lazy" decoding="async" />
            </div>
            <figcaption class="gallery-meta">
              <span class="gallery-caption">{{ item.caption }}</span>
              <span class="gallery-mood">{{ item.mood }}</span>
            </figcaption>
          </figure>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
const moments = ['奔赴山海', '归家的灯', '慢下来的周末', '与所爱同行']

const gallery = [
  {
    src: 'https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=720&h=960&fit=crop&q=85',
    alt: '雪山云海',
    caption: '去赴一场山海',
    mood: '辽阔的远方',
  },
  {
    src: 'https://images.unsplash.com/photo-1501785888041-af3ef285b470?w=640&h=480&fit=crop&q=85',
    alt: '湖畔晨光',
    caption: '把清晨留给自己',
    mood: '清晨的微光',
  },
  {
    src: 'https://images.unsplash.com/photo-1469474968028-56623f02e42e?w=640&h=480&fit=crop&q=85',
    alt: '林间光影',
    caption: '走进久违的山林',
    mood: '绿意与呼吸',
  },
]
</script>

<style scoped>
.index-home {
  position: relative;
  height: calc(100dvh - 64px - 72px);
  min-height: 440px;
  max-height: calc(100dvh - 64px - 72px);
  overflow: hidden;
  /* 顶部贴近白色页头，向下过渡到柔和蓝紫，呼应深色页脚 */
  background: linear-gradient(180deg, #ffffff 0%, #f5f6ff 36%, #eef0fd 68%, #e7e9f8 100%);
}

/* 氛围层 */
.home-ambient {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.5;
  animation: orb-drift 20s ease-in-out infinite;
}

.orb--blue {
  width: 460px;
  height: 460px;
  top: -130px;
  left: -90px;
  background: radial-gradient(circle, rgba(96, 165, 250, 0.5), transparent 70%);
}

.orb--violet {
  width: 400px;
  height: 400px;
  top: 14%;
  right: -110px;
  background: radial-gradient(circle, rgba(167, 139, 250, 0.45), transparent 70%);
  animation-delay: -7s;
}

.orb--rose {
  width: 340px;
  height: 340px;
  bottom: -120px;
  left: 32%;
  background: radial-gradient(circle, rgba(244, 182, 215, 0.4), transparent 70%);
  animation-delay: -13s;
}

@keyframes orb-drift {
  0%, 100% { transform: translate(0, 0) scale(1); }
  50% { transform: translate(14px, -18px) scale(1.06); }
}

.home-glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(ellipse 70% 50% at 50% 32%, rgba(255, 255, 255, 0.6), transparent 60%);
}

/* 内容：整体略向上 */
.home-inner {
  position: relative;
  z-index: 1;
  height: 100%;
  max-width: 1160px;
  margin: 0 auto;
  padding: 0 clamp(1rem, 3vw, 2rem) clamp(1.5rem, 6vh, 4rem);
  display: flex;
  align-items: center;
}

.vision-section {
  width: 100%;
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
  gap: clamp(1.5rem, 4vw, 3.25rem);
  align-items: center;
}

/* 文案区 */
.vision-copy {
  min-width: 0;
  animation: fade-up 0.9s ease-out both;
}

.eyebrow-pill {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 1.15rem;
  padding: 6px 15px 6px 11px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.75);
  border: 1px solid rgba(255, 255, 255, 0.95);
  box-shadow: 0 6px 22px rgba(99, 102, 241, 0.1);
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.16em;
  color: #6366f1;
  backdrop-filter: blur(8px);
}

.eyebrow-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  box-shadow: 0 0 10px rgba(139, 92, 246, 0.55);
}

.vision-title {
  margin: 0 0 1.15rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.title-line {
  display: block;
  font-size: clamp(1.85rem, 4.2vw, 2.75rem);
  font-weight: 800;
  line-height: 1.18;
  letter-spacing: -0.01em;
  color: #1e293b;
}

.title-line--accent {
  background: linear-gradient(120deg, #3b82f6 0%, #8b5cf6 45%, #ec4899 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.vision-lead {
  margin: 0 0 1.5rem;
  padding-left: 1rem;
  border-left: 2px solid;
  border-image: linear-gradient(180deg, #818cf8, #f0abfc) 1;
  font-size: clamp(0.9rem, 1.5vw, 1rem);
  line-height: 1.9;
  color: #64748b;
  max-width: 27rem;
}

/* 关键词：克制的点号分隔短句，去圆角胶囊与图标 */
.moment-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.1rem 0.15rem;
  margin: 0;
  font-size: 0.875rem;
  font-weight: 500;
  letter-spacing: 0.06em;
  color: #475569;
}

.moment-word {
  transition: color 0.2s ease;
}

.moment-word:hover {
  color: #6366f1;
}

.moment-sep {
  margin: 0 0.5rem;
  color: #cbd5e1;
  user-select: none;
}

/* 图片拼贴：Bento，图片完整呈现不被遮挡 */
.vision-gallery {
  display: grid;
  grid-template-columns: 1.12fr 0.88fr;
  grid-template-rows: 1fr 1fr;
  gap: 0.85rem;
  height: min(56vh, 400px);
  min-height: 240px;
}

.gallery-card {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  animation: fade-up 0.9s ease-out both;
  animation-delay: var(--delay, 0s);
}

.gallery-card--1 {
  grid-row: span 2;
}

.gallery-frame {
  position: relative;
  flex: 1;
  min-height: 0;
  border-radius: 20px;
  overflow: hidden;
  background: #eef0fd;
  box-shadow:
    0 2px 6px rgba(30, 41, 59, 0.05),
    0 18px 40px rgba(79, 70, 229, 0.12);
  transition: transform 0.55s cubic-bezier(0.22, 1, 0.36, 1), box-shadow 0.55s ease;
}

.gallery-card:hover .gallery-frame {
  transform: translateY(-5px);
  box-shadow:
    0 6px 12px rgba(30, 41, 59, 0.08),
    0 26px 52px rgba(79, 70, 229, 0.2);
}

/* 仅做精致描边，不覆盖图片画面 */
.gallery-frame::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.5);
  pointer-events: none;
}

.gallery-frame img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  transition: transform 0.85s cubic-bezier(0.22, 1, 0.36, 1);
}

.gallery-card:hover .gallery-frame img {
  transform: scale(1.05);
}

.gallery-meta {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0 4px;
}

.gallery-caption {
  font-size: 0.8125rem;
  font-weight: 700;
  color: #334155;
  letter-spacing: 0.02em;
}

.gallery-mood {
  font-size: 0.6875rem;
  color: #94a3b8;
  white-space: nowrap;
}

@keyframes fade-up {
  from { opacity: 0; transform: translateY(18px); }
  to { opacity: 1; transform: translateY(0); }
}

/* 响应式 */
@media (max-width: 900px) {
  .index-home {
    height: auto;
    max-height: none;
    min-height: calc(100dvh - 64px - 72px);
    overflow: auto;
  }

  .home-inner {
    padding: 1.75rem 1rem 2rem;
    align-items: flex-start;
  }

  .vision-section {
    grid-template-columns: 1fr;
    gap: 1.75rem;
  }

  .vision-gallery {
    height: 280px;
  }

  .gallery-card--1 {
    grid-row: span 1;
  }
}

@media (max-width: 480px) {
  .vision-gallery {
    height: 240px;
    gap: 0.6rem;
  }

  .gallery-frame {
    border-radius: 14px;
  }

  .gallery-meta {
    flex-direction: column;
    align-items: flex-start;
    gap: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .orb,
  .gallery-card,
  .vision-copy {
    animation: none;
  }

  .gallery-card:hover .gallery-frame,
  .gallery-card:hover .gallery-frame img {
    transform: none;
  }
}
</style>
