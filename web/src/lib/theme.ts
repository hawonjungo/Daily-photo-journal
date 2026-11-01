const THEME_STORAGE = 'relifes.theme'

export type Theme = 'system' | 'light' | 'dark'

export function getTheme(): Theme {
  const stored = localStorage.getItem(THEME_STORAGE)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

export function applyTheme(theme: Theme) {
  const dark = theme === 'dark' || (theme === 'system' && matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.classList.toggle('dark', dark)
}

export function setTheme(theme: Theme) {
  localStorage.setItem(THEME_STORAGE, theme)
  applyTheme(theme)
}

export function watchSystemTheme() {
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => applyTheme(getTheme()))
}
