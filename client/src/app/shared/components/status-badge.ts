import { Component, input } from '@angular/core';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="badge badge-{{status().toLowerCase()}}">{{ status() }}</span>`,
})
export class StatusBadge {
  status = input<string>('');
}
