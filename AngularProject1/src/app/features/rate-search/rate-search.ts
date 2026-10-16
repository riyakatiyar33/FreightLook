import { Component, computed, DestroyRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { startWith } from 'rxjs';
import { Airport } from '../../models/airport';
import { RateSearchResult, SLAB_CODES, SlabCode, Surcharge } from '../../models/rate';
import { AirportService } from '../../services/airport.service';
import { RateService } from '../../services/rate.service';

/** Surcharge columns in the results grid; some combine two surcharge types. */
const SURCHARGE_COLUMNS = [
  { id: 'fsc', label: 'FSC', types: ['FSC'] },
  { id: 'ssc', label: 'SSC/SEC', types: ['SSC', 'SEC'] },
  { id: 'xray', label: 'XRAY', types: ['XRAY'] },
  { id: 'misc', label: 'MISC', types: ['MISC'] },
  { id: 'ctg', label: 'CTG', types: ['CTG'] },
  { id: 'ams', label: 'AMS/AWB', types: ['AMS', 'AWB'] },
] as const;

type SurchargeColumnId = (typeof SURCHARGE_COLUMNS)[number]['id'];

interface ChargeLine {
  text: string;
  min: string | null;
}

/** Search result plus pre-formatted surcharge cells (computed once, not on every change detection). */
interface RateRow extends RateSearchResult {
  charges: Record<SurchargeColumnId, ChargeLine[]>;
  chargeSort: Record<SurchargeColumnId, number>;
  breakdown: string;
}

const AIRPORT_CODE = /^[A-Za-z]{3}$/;
const number = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 3 });

function differentAirports(group: AbstractControl): ValidationErrors | null {
  const origin = String(group.get('origin')?.value ?? '').trim().toUpperCase();
  const destination = String(group.get('destination')?.value ?? '').trim().toUpperCase();
  return origin && origin === destination ? { sameAirport: true } : null;
}

@Component({
  selector: 'app-rate-search',
  standalone: false,
  templateUrl: './rate-search.html',
  styleUrl: './rate-search.css',
})
export class RateSearch implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly airportService = inject(AirportService);
  private readonly rateService = inject(RateService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild(MatSort, { static: true }) private sort!: MatSort;
  @ViewChild(MatPaginator, { static: true }) private paginator!: MatPaginator;

  protected readonly slabCodes = SLAB_CODES;
  protected readonly surchargeColumns = SURCHARGE_COLUMNS;
  protected readonly displayedColumns = [
    'airline', 'commodity', 'origin', 'destination', 'min',
    ...SLAB_CODES.map(code => this.slabColumn(code)),
    ...SURCHARGE_COLUMNS.map(column => column.id),
    'total',
  ];

  protected readonly form = this.fb.group(
    {
      origin: ['', [Validators.required, Validators.pattern(AIRPORT_CODE)]],
      destination: ['', [Validators.required, Validators.pattern(AIRPORT_CODE)]],
      weight: this.fb.control<number | null>(null, [Validators.min(0.01), Validators.max(1_000_000)]),
    },
    { validators: differentAirports },
  );

  protected readonly dataSource = new MatTableDataSource<RateRow>([]);

  private readonly airports = signal<Airport[]>([]);
  private readonly originTerm = toSignal(this.form.controls.origin.valueChanges.pipe(startWith('')), { initialValue: '' });
  private readonly destinationTerm = toSignal(this.form.controls.destination.valueChanges.pipe(startWith('')), { initialValue: '' });

  protected readonly originOptions = computed(() => this.filterAirports(this.originTerm()));
  protected readonly destinationOptions = computed(() => this.filterAirports(this.destinationTerm()));
  protected readonly originHint = computed(() => this.airportLabel(this.originTerm()));
  protected readonly destinationHint = computed(() => this.airportLabel(this.destinationTerm()));

  protected readonly loading = signal(false);
  protected readonly searched = signal(false);
  protected readonly resultCount = signal(0);
  /** The query behind the rows currently shown (not the form, which the user may be editing). */
  protected readonly lastQuery = signal<{ origin: string; destination: string; weight: number | null } | null>(null);

  ngOnInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
    this.dataSource.sortingDataAccessor = (row, column) => this.sortValue(row, column);

    this.airportService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: airports => this.airports.set(airports) });

    // Deep links (e.g. from the upload page): /rates?origin=DEL&destination=DOH&weight=100
    const params = this.route.snapshot.queryParamMap;
    const origin = params.get('origin');
    const destination = params.get('destination');
    if (origin && destination) {
      const weight = Number(params.get('weight'));
      this.form.setValue({ origin, destination, weight: weight > 0 ? weight : null });
      this.search();
    }
  }

  protected search(): void {
    this.normalize('origin');
    this.normalize('destination');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { origin, destination, weight } = this.form.getRawValue();
    const query = { origin, destination, weight: weight || null };

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { origin, destination, weight: query.weight ?? undefined },
      replaceUrl: true,
    });

    this.loading.set(true);
    this.rateService
      .search(query)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: response => {
          this.dataSource.data = response.results.map(result => this.toRow(result, response.weight));
          this.paginator.firstPage();
          this.resultCount.set(response.count);
          this.lastQuery.set({ origin: response.origin, destination: response.destination, weight: response.weight });
          this.searched.set(true);
          this.loading.set(false);
        },
        // The HTTP error interceptor shows the snackbar.
        error: () => {
          this.dataSource.data = [];
          this.resultCount.set(0);
          this.lastQuery.set(query);
          this.searched.set(true);
          this.loading.set(false);
        },
      });
  }

  protected reset(): void {
    this.form.reset();
    this.dataSource.data = [];
    this.sort.sort({ id: '', start: 'asc', disableClear: false });
    this.resultCount.set(0);
    this.lastQuery.set(null);
    this.searched.set(false);
    this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  protected swap(): void {
    const { origin, destination } = this.form.getRawValue();
    this.form.patchValue({ origin: destination, destination: origin });
  }

  /** Upper-cases a typed code; a unique city/name match (e.g. "Doha") is replaced by its airport code. */
  private normalize(field: 'origin' | 'destination'): void {
    const control = this.form.controls[field];
    const value = control.value.trim();
    if (!value) {
      return;
    }
    if (AIRPORT_CODE.test(value)) {
      if (value.toUpperCase() !== control.value) {
        control.setValue(value.toUpperCase());
      }
      return;
    }
    const matches = this.filterAirports(value);
    if (matches.length === 1) {
      control.setValue(matches[0].code);
    }
  }

  protected slabColumn(code: SlabCode): string {
    return `slab_${code.replace('+', '')}`;
  }

  private filterAirports(term: string): Airport[] {
    const query = term.trim().toLowerCase();
    const airports = this.airports();
    if (!query) {
      return airports.slice(0, 50);
    }
    return airports
      .filter(a =>
        a.code.toLowerCase().startsWith(query) ||
        (a.city ?? '').toLowerCase().includes(query) ||
        a.name.toLowerCase().includes(query) ||
        (a.country ?? '').toLowerCase().includes(query))
      .sort((a, b) => Number(!a.code.toLowerCase().startsWith(query)) - Number(!b.code.toLowerCase().startsWith(query)))
      .slice(0, 50);
  }

  private airportLabel(code: string): string {
    const airport = this.airports().find(a => a.code === code.trim().toUpperCase());
    return airport ? [airport.city, airport.name].filter(Boolean).join(' · ') : '';
  }

  private toRow(result: RateSearchResult, weight: number | null): RateRow {
    const charges = {} as Record<SurchargeColumnId, ChargeLine[]>;
    const chargeSort = {} as Record<SurchargeColumnId, number>;

    for (const column of SURCHARGE_COLUMNS) {
      const items = result.surcharges.filter(s => (column.types as readonly string[]).includes(s.type));
      charges[column.id] = items.map(s => this.chargeLine(s, column.types.length > 1));
      chargeSort[column.id] = items.length ? items.reduce((sum, s) => sum + s.amount, 0) : Number.POSITIVE_INFINITY;
    }

    let breakdown = '';
    if (weight != null && result.applicableSlab && result.estimatedTotal != null) {
      const price = result.slabs[result.applicableSlab] ?? 0;
      breakdown =
        `Freight: ${result.applicableSlab} ${number.format(price)}/kg × ${weight} kg` +
        ` (min ${number.format(result.minimumCharge)}) = ${number.format(result.freightCharge ?? 0)}\n` +
        `Surcharges: ${number.format(result.surchargeTotal ?? 0)}\n` +
        `Total: ${result.currency} ${number.format(result.estimatedTotal)}`;
    }

    return { ...result, charges, chargeSort, breakdown };
  }

  private chargeLine(surcharge: Surcharge, showType: boolean): ChargeLine {
    const amount = number.format(surcharge.amount) + (surcharge.basis === 'KG' ? '/kg' : '');
    return {
      text: showType ? `${surcharge.type} ${amount}` : amount,
      min: surcharge.minAmount != null ? `min ${number.format(surcharge.minAmount)}` : null,
    };
  }

  private sortValue(row: RateRow, column: string): string | number {
    switch (column) {
      case 'airline': return row.airlineName.toLowerCase();
      case 'commodity': return row.commodity.toLowerCase();
      case 'origin': return row.originCode;
      case 'destination': return row.destinationCode;
      case 'min': return row.minimumCharge;
      case 'total': return row.estimatedTotal ?? Number.POSITIVE_INFINITY;
    }
    const slab = SLAB_CODES.find(code => this.slabColumn(code) === column);
    if (slab) {
      return row.slabs[slab] ?? Number.POSITIVE_INFINITY;
    }
    return row.chargeSort[column as SurchargeColumnId] ?? Number.POSITIVE_INFINITY;
  }
}
