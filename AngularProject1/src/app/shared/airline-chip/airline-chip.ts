import { Component, computed, input } from '@angular/core';

/** Muted, distinguishable chip colours; each airline code always maps to the same one. */
const PALETTE = [
  ['#E8EEFC', '#1E3A8A'], ['#E6F4EA', '#14532D'], ['#FDF0E1', '#9A3412'], ['#F3E8FD', '#6B21A8'],
  ['#E0F2F1', '#115E59'], ['#FCE8EC', '#9F1239'], ['#FEF7D7', '#854D0E'], ['#E7F0F8', '#0C4A6E'],
  ['#EDE9FE', '#4C1D95'], ['#ECEFF1', '#334155'],
] as const;

@Component({
  selector: 'app-airline-chip',
  standalone: false,
  template: `
    <span class="chip" [style.background]="colors()[0]" [style.color]="colors()[1]" [title]="name()">
      <span class="code">{{ code() }}</span>
      <span class="name">{{ name() }}</span>
    </span>
  `,
  styles: `
    .chip { display: inline-flex; align-items: center; gap: 6px; max-width: 220px; padding: 2px 10px 2px 4px;
            border-radius: 999px; font-size: 12px; line-height: 20px; white-space: nowrap; }
    .code { font-weight: 700; font-size: 11px; padding: 0 6px; border-radius: 999px; background: rgb(255 255 255 / 0.7); }
    .name { overflow: hidden; text-overflow: ellipsis; font-weight: 500; }
  `,
})
export class AirlineChip {
  readonly code = input.required<string>();
  readonly name = input('');

  protected readonly colors = computed(() => {
    let hash = 0;
    for (const char of this.code()) {
      hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
    }
    return PALETTE[hash % PALETTE.length];
  });
}
