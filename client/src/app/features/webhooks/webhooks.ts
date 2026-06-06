import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { WebhookEvent } from '../../core/models';

@Component({
  selector: 'app-webhooks',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './webhooks.html',
  styleUrl: './webhooks.css',
})
export class Webhooks implements OnInit {
  private readonly api = inject(ApiService);

  readonly webhooks = signal<WebhookEvent[]>([]);
  readonly expandedId = signal<string | null>(null);

  ngOnInit() {
    this.api.get<WebhookEvent[]>('/webhooks').subscribe(wh => this.webhooks.set(wh));
  }

  toggleExpand(id: string) {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  formatPayload(payload: unknown): string {
    try {
      return JSON.stringify(payload, null, 2);
    } catch {
      return String(payload);
    }
  }
}
