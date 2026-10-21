import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Api } from '../../api/api';
import { submitClaim } from '../../api/functions';
import { ClaimType, Market } from '../../api/models';
import { NoticeService } from '../../core/notice.service';

/**
 * Report a claim. One Idempotency-Key per form: a double click or a retry after a timeout returns the
 * same claim instead of creating a duplicate. Currency is not asked for: the API derives it from the market.
 */
@Component({
  selector: 'app-new-claim',
  imports: [ReactiveFormsModule],
  template: `
    <h1>Report a claim</h1>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label>Type
        <select formControlName="type">@for (t of types; track t) { <option [value]="t">{{ t }}</option> }</select>
      </label>
      <label>Market
        <select formControlName="market">@for (m of markets; track m) { <option [value]="m">{{ m }}</option> }</select>
      </label>
      <label>Policy number <input formControlName="policyNumber" placeholder="POL-123" /></label>
      <label>Incident date <input type="date" formControlName="incidentDate" [max]="today" /></label>
      <label>What happened? <textarea rows="4" formControlName="description"></textarea></label>
      <label>Contact phone <input formControlName="contactPhone" placeholder="+6591234567" /></label>
      <label>Estimated loss (in the market's currency)
        <input type="number" min="0.01" step="0.01" formControlName="claimedAmount" />
      </label>
      <button type="submit" [disabled]="form.invalid || busy()">Submit claim</button>
    </form>
  `,
})
export class NewClaimPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly notices = inject(NoticeService);
  private readonly idempotencyKey = crypto.randomUUID();

  protected readonly types: ClaimType[] = ['Motor', 'Property'];
  protected readonly markets: Market[] = ['Singapore', 'Australia', 'HongKong', 'Malaysia', 'Thailand', 'Philippines'];
  protected readonly today = new Date().toISOString().slice(0, 10);
  protected readonly busy = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    type: ['Motor' as ClaimType, Validators.required],
    market: ['Singapore' as Market, Validators.required],
    policyNumber: ['', [Validators.required, Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9/-]+$/)]],
    incidentDate: [this.today, Validators.required],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    contactPhone: ['', [Validators.required, Validators.pattern(/^\+?[0-9]{6,20}$/)]],
    claimedAmount: [0, [Validators.required, Validators.min(0.01)]],
  });

  async submit(): Promise<void> {
    this.busy.set(true);
    try {
      const claim = await this.api.invoke(submitClaim, {
        'Idempotency-Key': this.idempotencyKey,
        body: this.form.getRawValue(),
      });
      this.notices.success(`Claim ${claim.number} submitted.`);
      await this.router.navigate(['/claims', claim.id]);
    } catch (e) {
      this.notices.fromError(e);
    } finally {
      this.busy.set(false);
    }
  }
}
