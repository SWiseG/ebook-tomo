import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SettingMap } from '../../core/api.types';

interface SettingRow {
  key: string;
  value: string;
}

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  private readonly http = inject(HttpClient);

  readonly rows = signal<SettingRow[] | null>(null);
  readonly notice = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  newKey = '';
  newValue = '{}';

  readonly knownKeys = [
    'ai.monthlyCallCap',
    'discovery.categories',
    'discovery.topN',
    'discovery.scoreWeights',
    'publishing.requiresApproval',
    'portfolio.minActiveProducts',
  ];

  constructor() {
    this.load();
  }

  load(): void {
    this.http.get<SettingMap>('/api/v1/settings').subscribe({
      next: (map) =>
        this.rows.set(Object.entries(map).map(([key, value]) => ({ key, value }))),
      error: () => this.error.set('Falha ao carregar configurações.'),
    });
  }

  save(row: SettingRow): void {
    this.put(row.key, row.value);
  }

  add(): void {
    const key = this.newKey.trim();
    if (!key) {
      return;
    }
    this.put(key, this.newValue, () => {
      this.newKey = '';
      this.newValue = '{}';
      this.load();
    });
  }

  private put(key: string, valueJson: string, done?: () => void): void {
    this.notice.set(null);
    this.error.set(null);
    this.http.put(`/api/v1/settings/${encodeURIComponent(key)}`, { valueJson }).subscribe({
      next: () => {
        this.notice.set(`"${key}" salvo.`);
        done?.();
      },
      error: () => this.error.set(`Falha ao salvar "${key}".`),
    });
  }
}
