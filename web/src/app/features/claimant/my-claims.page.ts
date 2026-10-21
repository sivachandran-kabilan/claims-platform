import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../api/api';
import { listMyClaims } from '../../api/functions';
import { ClaimSummaryResponsePagedResult } from '../../api/models';
import { NoticeService } from '../../core/notice.service';

@Component({
  selector: 'app-my-claims',
  imports: [RouterLink, DatePipe, DecimalPipe],
  template: `
    <h1>My claims</h1>
    <p><a routerLink="/claims/new"><button type="button">Report a claim</button></a></p>
    @if (result(); as r) {
      @if (r.items?.length) {
        <table>
          <thead><tr><th>Claim</th><th>Type</th><th>Market</th><th>Status</th><th class="num">Claimed</th><th>Submitted</th></tr></thead>
          <tbody>
            @for (c of r.items; track c.id) {
              <tr>
                <td><a [routerLink]="['/claims', c.id]">{{ c.number }}</a></td>
                <td>{{ c.type }}</td>
                <td>{{ c.market }}</td>
                <td><span class="status">{{ c.status }}</span></td>
                <td class="num">{{ c.claimedAmount | number: '1.2-2' }} {{ c.currency }}</td>
                <td>{{ c.submittedAt | date: 'medium' }}</td>
              </tr>
            }
          </tbody>
        </table>
        <div class="pager">
          <button type="button" class="secondary" [disabled]="page() === 1" (click)="go(page() - 1)">Previous</button>
          <span class="muted">Page {{ page() }} · {{ r.totalCount }} claims</span>
          <button type="button" class="secondary" [disabled]="page() * pageSize >= (r.totalCount ?? 0)" (click)="go(page() + 1)">Next</button>
        </div>
      } @else {
        <p class="muted">You have not reported any claims yet.</p>
      }
    }
  `,
})
export class MyClaimsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly notices = inject(NoticeService);

  protected readonly pageSize = 20;
  protected readonly page = signal(1);
  protected readonly result = signal<ClaimSummaryResponsePagedResult | null>(null);

  ngOnInit(): void {
    void this.go(1);
  }

  async go(page: number): Promise<void> {
    try {
      this.result.set(await this.api.invoke(listMyClaims, { page, pageSize: this.pageSize }));
      this.page.set(page);
    } catch (e) {
      this.notices.fromError(e);
    }
  }
}
