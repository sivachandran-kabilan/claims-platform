import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { NoticeService } from '../../core/notice.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <h1>Log in</h1>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label>Email <input type="email" formControlName="email" autocomplete="username" /></label>
      <label>Password <input type="password" formControlName="password" autocomplete="current-password" /></label>
      @if (error()) { <p class="notice error">{{ error() }}</p> }
      <button type="submit" [disabled]="form.invalid || busy()">Log in</button>
    </form>
    <p class="muted">New claimant? <a routerLink="/register">Create an account</a></p>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notices = inject(NoticeService);

  /** Set by the guard/interceptor when a protected page needed a login. */
  readonly returnUrl = input<string>();
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.login(email, password);
      this.notices.dismiss();
      await this.router.navigateByUrl(this.returnUrl() || this.auth.homeRoute());
    } catch (e) {
      // The API returns the same 401 for unknown email, wrong password and lockout.
      const status = e instanceof HttpErrorResponse ? e.status : 0;
      this.error.set(
        status === 401 ? 'Invalid email or password.'
          : status === 429 ? 'Too many attempts. Wait a minute and try again.'
          : status === 0 ? 'The API is not reachable. Is it running?'
          : 'Login failed.',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
