import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api } from '../../api/api';
import { StrictHttpResponse } from '../../api/strict-http-response';
import {
  addClaimDocument, approveClaim, assessClaim, assignClaim, getClaim, rejectClaim, rejectClaimForNoResponse,
  requestClaimInformation, respondToClaimInformation, settleClaim,
} from '../../api/functions';
import { ClaimDetailResponse } from '../../api/models';
import { AuthService } from '../../core/auth.service';
import { NoticeService } from '../../core/notice.service';

/** Any generated claim-action function: (http, rootUrl, { id, If-Match, body? }) → updated claim. */
type ClaimAction<P> = (http: any, rootUrl: string, params: P) => import('rxjs').Observable<StrictHttpResponse<ClaimDetailResponse>>;

/**
 * One claim page for every role. The ETag from GET is sent as If-Match with each action; a 412 means
 * someone else changed the claim, so the page reloads. Which actions show is UX only: the API decides.
 */
@Component({
  selector: 'app-claim-detail',
  imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './claim-detail.page.html',
})
export class ClaimDetailPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly notices = inject(NoticeService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  protected readonly claim = signal<ClaimDetailResponse | null>(null);
  protected readonly busy = signal(false);
  private etag: string | null = null;

  // Form fields for the action panel
  protected question = '';
  protected response = '';
  protected reserve: number | null = null;
  protected approvedAmount: number | null = null;
  protected settleAmount: number | null = null;
  protected rejectReason = '';
  protected docName = '';
  protected docType = 'application/pdf';
  protected docSizeKb: number | null = null;

  protected readonly isClaimant = computed(() => this.auth.hasRole('Claimant'));
  protected readonly isTerminal = computed(() => ['Rejected', 'Settled'].includes(this.claim()?.status ?? ''));
  /** Staff act on claims assigned to them; managers on any claim (mirrors the API rule). */
  protected readonly canWork = computed(
    () => this.auth.hasRole('ClaimManager') || (this.auth.hasRole('ClaimStaff') && this.claim()?.assignedToUserId === this.auth.userId()),
  );

  constructor() {
    effect(() => void this.load(this.id()));
  }

  async load(id = this.id()): Promise<void> {
    try {
      this.apply(await this.api.invoke$Response(getClaim, { id }));
    } catch (e) {
      this.notices.fromError(e);
      if (e instanceof HttpErrorResponse && e.status === 404) await this.router.navigateByUrl(this.auth.homeRoute());
    }
  }

  protected assignToMe = () => this.run(assignClaim, {}, 'Claim assigned to you.');
  protected requestInfo = () => this.run(requestClaimInformation, { question: this.question }, 'Information requested.');
  protected respond = () => this.run(respondToClaimInformation, { response: this.response }, 'Response sent.');
  protected assess = () => this.run(assessClaim, { reserve: this.reserve }, 'Reserve saved.');
  protected approve = () => this.run(approveClaim, { approvedAmount: this.approvedAmount }, 'Claim approved.');
  protected reject = () => this.run(rejectClaim, { reason: this.rejectReason }, 'Claim rejected.');
  protected rejectNoResponse = () => this.run(rejectClaimForNoResponse, undefined, 'Claim rejected for no response.');
  protected settle = () => this.run(settleClaim, { amount: this.settleAmount }, 'Claim settled.');
  protected addDocument = () =>
    this.run(
      addClaimDocument,
      // Metadata only (file storage is out of scope): storageRef stands in for a blob reference.
      { fileName: this.docName, contentType: this.docType, sizeBytes: Math.round((this.docSizeKb ?? 0) * 1024), storageRef: `local://${crypto.randomUUID()}` },
      'Document added.',
    );

  private async run<P extends { id: string; 'If-Match'?: string }>(
    fn: ClaimAction<P>, body: object | undefined, success: string,
  ): Promise<void> {
    this.busy.set(true);
    try {
      const params = { id: this.id(), 'If-Match': this.etag ?? undefined, ...(body ? { body } : {}) } as P;
      this.apply(await this.api.invoke$Response(fn, params));
      this.notices.success(success);
      this.resetForms();
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 412) {
        this.notices.error('This claim was changed by someone else. The latest version has been loaded; please try again.');
        await this.load();
      } else {
        this.notices.fromError(e);
      }
    } finally {
      this.busy.set(false);
    }
  }

  private apply(response: StrictHttpResponse<ClaimDetailResponse>): void {
    this.claim.set(response.body);
    this.etag = response.headers.get('ETag');
  }

  private resetForms(): void {
    this.question = this.response = this.rejectReason = this.docName = '';
    this.reserve = this.approvedAmount = this.settleAmount = this.docSizeKb = null;
  }
}
