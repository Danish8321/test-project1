import { Component, input } from '@angular/core';

@Component({
  selector: 'app-amount',
  standalone: true,
  template: `<span>{{ amount() }} {{ currency() }}</span>`,
})
export class Amount {
  amount = input<number>(0);
  currency = input<string>('USDC');
}
