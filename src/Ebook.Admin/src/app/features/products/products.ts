import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { ProductItem, ProductStatus } from '../../core/api.types';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

const SEVERITY: Record<ProductStatus, Severity> = {
  Pipeline: 'info',
  AwaitingApproval: 'warn',
  Reworking: 'warn',
  Publishing: undefined,
  Live: 'success',
  Iterating: 'contrast',
  Retired: 'secondary',
};

@Component({
  selector: 'app-products',
  imports: [DatePipe, CurrencyPipe, RouterLink, TableModule, TagModule, ButtonModule, Loading],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class Products {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly products = signal<ProductItem[] | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    this.http.get<ProductItem[]>('/api/v1/products').subscribe({
      next: (data) => this.products.set(data),
      error: () => this.error.set('Falha ao carregar produtos.'),
    });
  }

  severity(status: ProductStatus): Severity {
    return SEVERITY[status];
  }

  open(p: ProductItem): void {
    void this.router.navigate(['/products', p.id]);
  }
}
