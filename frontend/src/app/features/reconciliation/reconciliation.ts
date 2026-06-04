import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { ReconciliationResult } from '../../core/models';

@Component({
  selector: 'app-reconciliation',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './reconciliation.html',
  styleUrl: './reconciliation.css',
})
export class Reconciliation {
  private readonly api = inject(ApiService);

  readonly result = signal<ReconciliationResult | null>(null);
  readonly running = signal(false);
  readonly error = signal<string | null>(null);

  run() {
    this.running.set(true);
    this.error.set(null);
    this.api.post<ReconciliationResult>('/reconciliation/run', {}).subscribe({
      next: (r) => {
        this.result.set(r);
        this.running.set(false);
      },
      error: () => {
        this.error.set('Reconciliation failed — check API logs');
        this.running.set(false);
      }
    });
  }
}
