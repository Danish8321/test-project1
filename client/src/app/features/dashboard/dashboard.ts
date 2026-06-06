import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { Deposit, Withdrawal, WebhookEvent, HealthStatus } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(ApiService);

  readonly health = signal<HealthStatus | null>(null);
  readonly deposits = signal<Deposit[]>([]);
  readonly withdrawals = signal<Withdrawal[]>([]);
  readonly webhooks = signal<WebhookEvent[]>([]);

  ngOnInit() {
    this.api.get<HealthStatus>('/health').subscribe(h => this.health.set(h));
    this.api.get<Deposit[]>('/deposits').subscribe(d => this.deposits.set(d.slice(0, 5)));
    this.api.get<Withdrawal[]>('/withdrawals').subscribe(w => this.withdrawals.set(w.slice(0, 5)));
    this.api.get<WebhookEvent[]>('/webhooks').subscribe(wh => this.webhooks.set(wh.slice(0, 5)));
  }
}
