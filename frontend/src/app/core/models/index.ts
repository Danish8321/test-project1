export interface Customer {
  id: string;
  name: string;
  email: string;
  customerType: 'Circle' | 'ExternalWallet';
  createdAt: string;
}

export interface FundingAccount {
  id: string;
  customerId: string;
  currency: string;
  createdAt: string;
}

export interface Deposit {
  id: string;
  customerId: string;
  fundingAccountId: string;
  circlePaymentIntentId: string;
  amount: number;
  status: 'Pending' | 'Completed' | 'Failed' | 'Cancelled';
  createdAt: string;
  updatedAt: string;
}

export interface Withdrawal {
  id: string;
  customerId: string;
  fundingAccountId: string;
  circlePayoutId: string;
  amount: number;
  status: 'Pending' | 'Completed' | 'Failed';
  createdAt: string;
  updatedAt: string;
}

export interface LedgerEntry {
  id: string;
  fundingAccountId: string;
  entryType: 'Credit' | 'Debit';
  amount: number;
  referenceId: string;
  createdAt: string;
}

export interface WebhookEvent {
  id: string;
  circleEventId: string;
  eventType: string;
  payload: unknown;
  status: string;
  createdAt: string;
  processedAt?: string;
}

export interface HealthStatus {
  db: string;
  circle: string;
  timestamp: string;
}

export interface ReconciliationResult {
  runAt: string;
  totalDeposits: number;
  matchedDeposits: number;
  unmatchedDeposits: number;
  totalWithdrawals: number;
  matchedWithdrawals: number;
  unmatchedWithdrawals: number;
  mismatches: string[];
}

export interface BalanceResponse {
  balance: number;
}
