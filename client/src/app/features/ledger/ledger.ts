import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { Customer, FundingAccount, LedgerEntry, BalanceResponse } from '../../core/models';

interface AccountOption extends FundingAccount {
  customerName: string;
}

@Component({
  selector: 'app-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ledger.html',
  styleUrl: './ledger.css',
})
export class Ledger implements OnInit {
  private readonly api = inject(ApiService);

  readonly accounts = signal<AccountOption[]>([]);
  readonly entries = signal<LedgerEntry[]>([]);
  readonly balance = signal<number | null>(null);
  readonly loading = signal(false);
  selectedAccountId = '';

  ngOnInit() {
    this.api.get<Customer[]>('/customers').subscribe(customers => {
      if (customers.length === 0) return;
      forkJoin(
        customers.map(c =>
          this.api.get<FundingAccount[]>(`/customers/${c.id}/funding-accounts`)
        )
      ).subscribe(results => {
        this.accounts.set(
          results.flatMap((fas, i) =>
            fas.map(fa => ({ ...fa, customerName: customers[i].name }))
          )
        );
      });
    });
  }

  onAccountChange(id: string) {
    this.selectedAccountId = id;
    if (!id) { this.entries.set([]); this.balance.set(null); return; }
    this.loading.set(true);
    this.api.get<LedgerEntry[]>(`/funding-accounts/${id}/ledger`).subscribe(e => {
      this.entries.set(e);
      this.loading.set(false);
    });
    this.api.get<BalanceResponse>(`/funding-accounts/${id}/balance`).subscribe(b =>
      this.balance.set(b.balance)
    );
  }
}
