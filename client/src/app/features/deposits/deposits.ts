import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { Deposit, Customer, FundingAccount } from '../../core/models';

@Component({
  selector: 'app-deposits',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './deposits.html',
  styleUrl: './deposits.css',
})
export class Deposits implements OnInit {
  private readonly api = inject(ApiService);

  readonly deposits = signal<Deposit[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly fundingAccounts = signal<FundingAccount[]>([]);
  readonly showForm = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  form = { customerId: '', fundingAccountId: '', amount: 0 };

  ngOnInit() {
    this.api.get<Deposit[]>('/deposits').subscribe(d => this.deposits.set(d));
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
    this.api.post<Deposit>('/deposits', this.form).subscribe({
      next: (d) => {
        this.deposits.update(list => [d, ...list]);
        this.form = { customerId: '', fundingAccountId: '', amount: 0 };
        this.fundingAccounts.set([]);
        this.showForm.set(false);
        this.submitting.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error ?? 'Failed to create deposit');
        this.submitting.set(false);
      }
    });
  }
}
