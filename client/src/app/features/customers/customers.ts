import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { Customer, FundingAccount } from '../../core/models';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customers.html',
  styleUrl: './customers.css',
})
export class Customers implements OnInit {
  private readonly api = inject(ApiService);

  readonly customers = signal<Customer[]>([]);
  readonly selected = signal<Customer | null>(null);
  readonly fundingAccounts = signal<FundingAccount[]>([]);
  readonly showCreateCustomer = signal(false);
  readonly showCreateAccount = signal(false);
  readonly error = signal<string | null>(null);

  newCustomer = { name: '', email: '', customerType: 'Circle' };
  newAccount = { currency: 'USDC' };

  ngOnInit() {
    this.api.get<Customer[]>('/customers').subscribe(c => this.customers.set(c));
  }

  select(c: Customer) {
    this.selected.set(c);
    this.api.get<FundingAccount[]>(`/customers/${c.id}/funding-accounts`)
      .subscribe(fa => this.fundingAccounts.set(fa));
  }

  createCustomer() {
    this.api.post<Customer>('/customers', this.newCustomer).subscribe({
      next: (c) => {
        this.customers.update(list => [...list, c]);
        this.newCustomer = { name: '', email: '', customerType: 'Circle' };
        this.showCreateCustomer.set(false);
        this.error.set(null);
      },
      error: () => this.error.set('Failed to create customer')
    });
  }

  createFundingAccount() {
    const cust = this.selected();
    if (!cust) return;
    this.api.post<FundingAccount>(`/customers/${cust.id}/funding-accounts`, this.newAccount).subscribe({
      next: (fa) => {
        this.fundingAccounts.update(list => [...list, fa]);
        this.showCreateAccount.set(false);
        this.error.set(null);
      },
      error: () => this.error.set('Failed to create funding account')
    });
  }
}
