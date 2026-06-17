import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';
import { TomoLogo } from '../../shared/tomo-logo';

@Component({
  selector: 'app-login',
  imports: [
    FormsModule,
    TranslocoDirective,
    TomoLogo,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    MessageModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly t = inject(TranslocoService);

  username = '';
  password = '';
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);

  submit(): void {
    this.error.set(null);
    this.loading.set(true);
    this.auth.login(this.username, this.password).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => {
        this.error.set(this.t.translate('login.invalid'));
        this.loading.set(false);
      },
    });
  }
}
