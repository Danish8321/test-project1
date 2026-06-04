import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  styleUrl: './app.css',
  template: `
    <nav>
      <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a>
      <a routerLink="/customers" routerLinkActive="active">Customers</a>
      <a routerLink="/deposits" routerLinkActive="active">Deposits</a>
      <a routerLink="/withdrawals" routerLinkActive="active">Withdrawals</a>
      <a routerLink="/ledger" routerLinkActive="active">Ledger</a>
      <a routerLink="/webhooks" routerLinkActive="active">Webhooks</a>
      <a routerLink="/reconciliation" routerLinkActive="active">Reconciliation</a>
    </nav>
    <main>
      <router-outlet />
    </main>
  `
})
export class App {}
