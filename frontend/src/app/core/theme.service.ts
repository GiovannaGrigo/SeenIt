import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  readonly isDark = signal(false);

  constructor() {
    const savedTheme = isPlatformBrowser(this.platformId)
      ? window.localStorage.getItem('seenit-theme')
      : null;
    this.setTheme(savedTheme === 'dark' ? 'dark' : 'light');
  }

  toggle(): void {
    this.setTheme(this.isDark() ? 'light' : 'dark');
  }

  private setTheme(theme: Theme): void {
    this.isDark.set(theme === 'dark');
    this.document.documentElement.classList.toggle('dark-theme', theme === 'dark');
    this.document.documentElement.style.colorScheme = theme;

    if (isPlatformBrowser(this.platformId)) {
      window.localStorage.setItem('seenit-theme', theme);
    }
  }
}