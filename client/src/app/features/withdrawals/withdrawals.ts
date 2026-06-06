import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { Withdrawal, Customer, FundingAccount } from '../../core/models';

@Component({
  selector: 'app-withdrawals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './withdrawals.html',
  styleUrl: './withdrawals.css',
})
export class Withdrawals implements OnInit {
  private readonly api = inject(ApiService);

  readonly withdrawals = signal<Withdrawal[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly fundingAccounts = signal<FundingAccount[]>([]);
  readonly showForm = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  form = { customerId: '', fundingAccountId: '', amount: 0, destinationAddress: '' };

  ngOnInit() {
    this.api.get<Withdrawal[]>('/withdrawals').subscribe(w => this.withdrawals.set(w));
    this.api.get<Customer[]>('/customers').subscribe(c => this.customers.set(c));
  }

  onCustomerChange(id: string) {
    this.form.customerId = id;
    this.form.fundingAccountId = '';
    if (!id) { this.fundingAccounts.set([]); return; }
    this.api.get<FundingAccount[]>(`/customers/${id}/funding-accounts`)
      .subscribe(fa => this.fundingAccounts.set(fa));
  }

  submit() {
    this.submitting.set(true);
    this.error.set(null);
    this.api.post<Withdrawal>('/withdrawals', this.form).subscribe({
      next: (w) => {
        this.withdrawals.update(list => [w, ...list]);
        this.form = { customerId: '', fundingAccountId: '', amount: 0, destinationAddress: '' };
        this.fundingAccounts.set([]);
        this.showForm.set(false);
        this.submitting.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Failed to create withdrawal');
        this.submitting.set(false);
      }
    });
  }
}
