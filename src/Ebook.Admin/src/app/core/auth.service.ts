import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { LoginResult } from './api.types';

const TOKEN_KEY = 'ebook.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly isAuthenticated = signal(this.token !== null);

  get token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  login(username: string, password: string) {
    return this.http
      .post<LoginResult>('/api/v1/auth/login', { username, password })
      .pipe(
        tap((result) => {
          localStorage.setItem(TOKEN_KEY, result.token);
          this.isAuthenticated.set(true);
        }),
      );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.isAuthenticated.set(false);
    void this.router.navigateByUrl('/login');
  }
}
