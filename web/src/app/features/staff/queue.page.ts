import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api } from '../../api/api';
import { assignClaim, getClaim, getClaimQueue } from '../../api/functions';
import { ClaimSummaryResponsePagedResult } from '../../api/models';
import { NoticeService } from '../../core/notice.service';

/** Unassigned Submitted claims, oldest first. "Assign to me" fetches the ETag, then assigns. */
@Component({
  selector: 'app-queue',
  imports: [RouterLink, DatePipe, DecimalPipe],
  template: `
    <h1>Unassigned queue</h1>
    @if (result(); as r) {
      @if (r.items?.length) {
        <table>
          <thead><tr><th>Claim</th><th>Type</th><th>Market</th><th class="num">Claimed</th><th>Submitted</th><th></th></tr></thead>
          <tbody>
            @for (c of r.items; track c.id) {
              <tr>
                <td><a [routerLink]="['/claims', c.id]">{{ c.number }}</a></td>
                <td>{{ c.type }}</td>
                <td>{{ c.market }}</td>
                <td class="num">{{ c.claimedAmount | number: '1.2-2' }} {{ c.currency }}</td>
                <td>{{ c.submittedAt | date: 'medium' }}</td>
                <td><button type="button" [disabled]="busy()" (click)="assign(c.id!)">Assign to me</button></td>
              </tr>
            }
          </tbody>
        </table>
        <div class="pager">
          <button type="button" class="secondary" [disabled]="page() === 1" (click)="go(page() - 1)">Previous</button>
          <span class="muted">Page {{ page() }} · {{ r.totalCount }} waiting</span>
          <button type="button" class="secondary" [disabled]="page() * pageSize >= (r.totalCount ?? 0)" (click)="go(page() + 1)">Next</button>
        </div>
      } @else {
        <p class="muted">The queue is empty.</p>
      }
    }
  `,
})
export class QueuePage implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly notices = inject(NoticeService);

  protected readonly pageSize = 20;
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly result = signal<ClaimSummaryResponsePagedResult | null>(null);

  ngOnInit(): void {
    void this.go(1);
  }

  async go(page: number): Promise<void> {
    try {
      this.result.set(await this.api.invoke(getClaimQueue, { page, pageSize: this.pageSize }));
      this.page.set(page);
    } catch (e) {
      this.notices.fromError(e);
    }
  }

  async assign(id: string): Promise<void> {
    this.busy.set(true);
    try {
      const current = await this.api.invoke$Response(getClaim, { id });
      await this.api.invoke(assignClaim, { id, 'If-Match': current.headers.get('ETag') ?? undefined, body: {} });
      this.notices.success('Claim assigned to you.');
      await this.router.navigate(['/claims', id]);
    } catch (e) {
      this.notices.fromError(e);
      await this.go(this.page());
    } finally {
      this.busy.set(false);
    }
  }
}
