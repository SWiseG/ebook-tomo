import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ProductItem, ProductStatus } from '../../core/api.types';

const STATUS_CHIP: Record<ProductStatus, string> = {
  Pipeline: 'chip--info',
  AwaitingApproval: 'chip--warn',
  Reworking: 'chip--warn',
  Publishing: 'chip--primary',
  Live: 'chip--success',
  Iterating: 'chip--accent',
  Retired: 'chip',
};

@Component({
  selector: 'app-products',
  imports: [DatePipe, CurrencyPipe, RouterLink],
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

  chip(status: ProductStatus): string {
    return STATUS_CHIP[status];
  }

  open(p: ProductItem): void {
    void this.router.navigate(['/products', p.id]);
  }
}
