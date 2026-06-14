import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { MessageService } from 'primeng/api';
import { SettingMap } from '../../core/api.types';

interface SettingRow {
  key: string;
  value: string;
}

@Component({
  selector: 'app-settings',
  imports: [FormsModule, CardModule, ButtonModule, InputTextModule, TextareaModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  private readonly http = inject(HttpClient);
  private readonly messages = inject(MessageService);

  readonly rows = signal<SettingRow[] | null>(null);

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
      next: (map) => this.rows.set(Object.entries(map).map(([key, value]) => ({ key, value }))),
      error: () => this.messages.add({ severity: 'error', summary: 'Falha ao carregar configurações.' }),
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
    this.http.put(`/api/v1/settings/${encodeURIComponent(key)}`, { valueJson }).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: `"${key}" salvo.` });
        done?.();
      },
      error: () => this.messages.add({ severity: 'error', summary: `Falha ao salvar "${key}".` }),
    });
  }
}
