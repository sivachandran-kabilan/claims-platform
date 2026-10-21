import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
import { getExposureReport, getWorkloadReport } from '../../api/functions';
import { ExposureReport, WorkloadReport } from '../../api/models';
import { NoticeService } from '../../core/notice.service';

/** Manager reports: team workload and outstanding liability (native currency, never summed across currencies). */
@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe],
  template: `
    <h1>Manager dashboard</h1>
    <p><button type="button" class="secondary" (click)="load()">Refresh</button></p>

    @if (workload(); as w) {
      <h2>Team workload</h2>
      <table>
        <thead>
          <tr><th>Staff member</th><th>Role</th><th class="num">Open</th><th class="num">Under review</th><th class="num">Info requested</th>
              <th class="num">Approved</th><th class="num">Avg age (days)</th><th>Oldest claim</th></tr>
        </thead>
        <tbody>
          @for (s of w.staff; track s.userId) {
            <tr>
              <td>{{ s.displayName }}</td>
              <td>{{ s.role }}</td>
              <td class="num">{{ s.openClaims }}</td>
              <td class="num">{{ s.underReview }}</td>
              <td class="num">{{ s.infoRequested }}</td>
              <td class="num">{{ s.approved }}</td>
              <td class="num">{{ s.averageAgeDays ?? '—' }}</td>
              <td>{{ s.oldestClaimNumber ? s.oldestClaimNumber + ' (' + s.oldestAgeDays + ' d)' : '—' }}</td>
            </tr>
          }
        </tbody>
      </table>
      <p class="muted">
        Unassigned queue: {{ w.unassigned?.count ?? 0 }} claim(s)
        @if (w.unassigned?.oldestClaimNumber) {
          · average age {{ w.unassigned?.averageAgeDays }} d · oldest {{ w.unassigned?.oldestClaimNumber }} ({{ w.unassigned?.oldestAgeDays }} d)
        }
      </p>
    }

    @if (exposure(); as x) {
      <h2>Liability exposure (open claims)</h2>
      <table>
        <thead><tr><th>Market</th><th>Type</th><th>Status</th><th class="num">Claims</th><th class="num">Unreserved</th><th class="num">Outstanding</th></tr></thead>
        <tbody>
          @for (r of x.rows; track $index) {
            <tr>
              <td>{{ r.market }}</td>
              <td>{{ r.type }}</td>
              <td>{{ r.status }}</td>
              <td class="num">{{ r.claimCount }}</td>
              <td class="num">{{ r.unreservedCount }}</td>
              <td class="num">{{ r.outstandingReserve | number: '1.2-2' }} {{ r.currency }}</td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="muted">No open claims.</td></tr>
          }
        </tbody>
      </table>
      <h2>Totals by currency</h2>
      <table>
        <thead><tr><th>Currency</th><th class="num">Claims</th><th class="num">Outstanding</th></tr></thead>
        <tbody>
          @for (t of x.totals; track t.currency) {
            <tr><td>{{ t.currency }}</td><td class="num">{{ t.claimCount }}</td><td class="num">{{ t.outstandingReserve | number: '1.2-2' }}</td></tr>
          }
        </tbody>
      </table>
      <p class="muted">No cross-currency total: FX conversion is out of scope.</p>
    }
  `,
})
export class DashboardPage implements OnInit {
  private readonly api = inject(Api);
  private readonly notices = inject(NoticeService);
  protected readonly workload = signal<WorkloadReport | null>(null);
  protected readonly exposure = signal<ExposureReport | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    try {
      const [workload, exposure] = await Promise.all([
        this.api.invoke(getWorkloadReport),
        this.api.invoke(getExposureReport),
      ]);
      this.workload.set(workload);
      this.exposure.set(exposure);
    } catch (e) {
      this.notices.fromError(e);
    }
  }
}
