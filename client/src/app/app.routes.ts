import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard').then(m => m.Dashboard)
  },
  {
    path: 'customers',
    loadComponent: () =>
      import('./features/customers/customers').then(m => m.Customers)
  },
  {
    path: 'deposits',
    loadComponent: () =>
      import('./features/deposits/deposits').then(m => m.Deposits)
  },
  {
    path: 'withdrawals',
    loadComponent: () =>
      import('./features/withdrawals/withdrawals').then(m => m.Withdrawals)
  },
  {
    path: 'ledger',
    loadComponent: () =>
      import('./features/ledger/ledger').then(m => m.Ledger)
  },
  {
    path: 'webhooks',
    loadComponent: () =>
      import('./features/webhooks/webhooks').then(m => m.Webhooks)
  },
  {
    path: 'reconciliation',
    loadComponent: () =>
      import('./features/reconciliation/reconciliation').then(m => m.Reconciliation)
  }
];
