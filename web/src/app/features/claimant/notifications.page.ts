import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../api/api';
import { listMyNotifications } from '../../api/functions';
import { NotificationResponse } from '../../api/models';
import { NoticeService } from '../../core/notice.service';

/** Latest 50 notifications, created by the Kafka consumer (a few seconds after each event). */
@Component({
  selector: 'app-notifications',
  imports: [RouterLink, DatePipe],
  template: `
    <h1>Notifications</h1>
    <p><button type="button" class="secondary" (click)="load()">Refresh</button></p>
    @if (items().length) {
      <table>
        <thead><tr><th>When</th><th>Message</th><th>Claim</th></tr></thead>
        <tbody>
          @for (n of items(); track n.id) {
            <tr>
              <td>{{ n.createdAt | date: 'medium' }}</td>
              <td>{{ n.message }}</td>
              <td><a [routerLink]="['/claims', n.claimId]">{{ n.claimNumber }}</a></td>
            </tr>
          }
        </tbody>
      </table>
    } @else {
      <p class="muted">No notifications yet. They appear a few seconds after a claim changes.</p>
    }
  `,
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly notices = inject(NoticeService);
  protected readonly items = signal<NotificationResponse[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    try {
      this.items.set(await this.api.invoke(listMyNotifications));
    } catch (e) {
      this.notices.fromError(e);
    }
  }
}
