import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/api';
import { register } from '../../api/functions';
import { NoticeService } from '../../core/notice.service';

/** Self-registration always creates a Claimant (the request has no role). Then the user logs in. */
@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <h1>Create a claimant account</h1>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label>Full name <input formControlName="displayName" autocomplete="name" /></label>
      <label>Email <input type="email" formControlName="email" autocomplete="email" /></label>
      <label>
        Password (at least 10 characters)
        <input type="password" formControlName="password" autocomplete="new-password" />
      </label>
      <button type="submit" [disabled]="form.invalid || busy()">Create account</button>
    </form>
    <p class="muted">Already registered? <a routerLink="/login">Log in</a></p>
  `,
})
export class RegisterPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly notices = inject(NoticeService);

  protected readonly busy = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
  });

  async submit(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.invoke(register, { body: this.form.getRawValue() });
      this.notices.success('Account created. Please log in.');
      await this.router.navigate(['/login']);
    } catch (e) {
      this.notices.fromError(e);
    } finally {
      this.busy.set(false);
    }
  }
}
