import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api } from '../../api/api';
import { listClaims } from '../../api/functions';
import { ClaimStatus, ClaimSummaryResponsePagedResult, Market } from '../../api/models';
import { AuthService } from '../../core/auth.service';
import { NoticeService } from '../../core/notice.service';

/** All claims with server-side filters and paging (max page size 100 on the API). */
@Component({
  selector: 'app-all-claims',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <h1>All claims</h1>
    <div class="form inline">
      <label>Status
        <select [(ngModel)]="status" (change)="go(1)">
          <option [ngValue]="undefined">Any</option>
          @for (s of statuses; track s) { <option [ngValue]="s">{{ s }}</option> }
        </select>
      </label>
      <label>Market
        <select [(ngModel)]="market" (change)="go(1)">
          <option [ngValue]="undefined">Any</option>
          @for (m of markets; track m) { <option [ngValue]="m">{{ m }}</option> }
        </select>
      </label>
      <label><span><input type="checkbox" [(ngModel)]="mine" (change)="go(1)" /> Assigned to me</span></label>
    </div>
    @if (result(); as r) {
      <table>
        <thead><tr><th>Claim</th><th>Type</th><th>Market</th><th>Status</th><th class="num">Outstanding</th><th>Submitted</th></tr></thead>
        <tbody>
          @for (c of r.items; track c.id) {
            <tr>
              <td><a [routerLink]="['/claims', c.id]">{{ c.number }}</a></td>
              <td>{{ c.type }}</td>
              <td>{{ c.market }}</td>
              <td><span class="status">{{ c.status }}</span></td>
              <td class="num">{{ c.outstandingReserve | number: '1.2-2' }} {{ c.currency }}</td>
              <td>{{ c.submittedAt | date: 'medium' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No claims match.</td></tr>
          }
        </tbody>
      </table>
      <div class="pager">
        <button type="button" class="secondary" [disabled]="page() === 1" (click)="go(page() - 1)">Previous</button>
        <span class="muted">Page {{ page() }} · {{ r.totalCount }} claims</span>
        <button type="button" class="secondary" [disabled]="page() * pageSize >= (r.totalCount ?? 0)" (click)="go(page() + 1)">Next</button>
      </div>
    }
  `,
})
export class AllClaimsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly auth = inject(AuthService);
  private readonly notices = inject(NoticeService);

  protected readonly statuses: ClaimStatus[] = ['Submitted', 'UnderReview', 'InfoRequested', 'Approved', 'Rejected', 'Settled'];
  protected readonly markets: Market[] = ['Singapore', 'Australia', 'HongKong', 'Malaysia', 'Thailand', 'Philippines'];
  protected status?: ClaimStatus;
  protected market?: Market;
  protected mine = false;

  protected readonly pageSize = 20;
  protected readonly page = signal(1);
  protected readonly result = signal<ClaimSummaryResponsePagedResult | null>(null);

  ngOnInit(): void {
    void this.go(1);
  }

  async go(page: number): Promise<void> {
    try {
      this.result.set(await this.api.invoke(listClaims, {
        page,
        pageSize: this.pageSize,
        status: this.status,
        market: this.market,
        assignedTo: this.mine ? (this.auth.userId() ?? undefined) : undefined,
      }));
      this.page.set(page);
    } catch (e) {
      this.notices.fromError(e);
    }
  }
}
