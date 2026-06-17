import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SettingMap } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

interface SettingRow {
  key: string;
  value: string;
}

@Component({
  selector: 'app-settings',
  imports: [
    FormsModule,
    TranslocoDirective,
    CardModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    Loading,
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

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
      error: () => this.notify.error(this.t.translate('settings.loadError')),
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
        this.notify.success(this.t.translate('settings.saved'), key);
        done?.();
      },
      error: () => this.notify.error(this.t.translate('settings.saveError', { key })),
    });
  }
}
