import { HttpEventType } from '@angular/common/http';
import { Component, computed, DestroyRef, ElementRef, inject, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiError } from '../../models/api-error';
import { UploadResult } from '../../models/upload-result';
import { RateSheetService } from '../../services/rate-sheet.service';

type Phase = 'idle' | 'uploading' | 'processing' | 'done' | 'failed';

@Component({
  selector: 'app-upload-rate-sheet',
  standalone: false,
  templateUrl: './upload-rate-sheet.html',
  styleUrl: './upload-rate-sheet.css',
})
export class UploadRateSheet {
  private readonly rateSheetService = inject(RateSheetService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private upload$?: Subscription;

  @ViewChild('fileInput') private fileInput?: ElementRef<HTMLInputElement>;

  protected readonly accept = this.rateSheetService.allowedExtensions.join(',');
  protected readonly maxMb = this.rateSheetService.maxFileBytes / (1024 * 1024);

  protected readonly file = signal<File | null>(null);
  protected readonly fileError = signal<string | null>(null);
  protected readonly dragOver = signal(false);
  protected readonly phase = signal<Phase>('idle');
  protected readonly progress = signal(0);
  protected readonly result = signal<UploadResult | null>(null);
  protected readonly serverError = signal<string | null>(null);

  protected readonly busy = computed(() => this.phase() === 'uploading' || this.phase() === 'processing');

  /** Unique routes from the saved rates ("QR DEL-DOH DGRCAO" -> "DEL-DOH"). */
  protected readonly savedRoutes = computed(() => {
    const routes = (this.result()?.savedRoutes ?? [])
      .map(text => /\b([A-Z]{3})-([A-Z]{3})\b/.exec(text))
      .filter((match): match is RegExpExecArray => match !== null)
      .map(match => `${match[1]}-${match[2]}`);
    return [...new Set(routes)];
  });

  protected browse(): void {
    if (!this.busy()) {
      this.fileInput?.nativeElement.click();
    }
  }

  protected onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.select(input.files?.[0] ?? null);
    input.value = ''; // allow re-selecting the same file
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    if (!this.busy()) {
      this.dragOver.set(true);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    if (!this.busy()) {
      this.select(event.dataTransfer?.files?.[0] ?? null);
    }
  }

  protected clearFile(): void {
    this.file.set(null);
    this.fileError.set(null);
  }

  protected upload(): void {
    const file = this.file();
    if (!file || this.busy()) {
      return;
    }

    this.phase.set('uploading');
    this.progress.set(0);
    this.result.set(null);
    this.serverError.set(null);

    this.upload$ = this.rateSheetService
      .upload(file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress) {
            const percent = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
            this.progress.set(percent);
            if (percent >= 100) {
              this.phase.set('processing'); // file sent; server is extracting and saving
            }
          } else if (event.type === HttpEventType.Response && event.body) {
            this.result.set(event.body);
            this.phase.set('done');
            this.file.set(null);
            const { saved, rejected } = event.body;
            this.snackBar.open(
              `${saved} rate${saved === 1 ? '' : 's'} saved${rejected ? `, ${rejected} rejected` : ''}.`,
              'OK',
              { duration: 4000, panelClass: saved ? 'fl-snack-success' : 'fl-snack-error' },
            );
          }
        },
        error: (error: ApiError) => {
          this.serverError.set(error.message);
          this.phase.set('failed');
        },
      });
  }

  protected cancel(): void {
    this.upload$?.unsubscribe();
    this.phase.set('idle');
    this.progress.set(0);
  }

  protected startOver(): void {
    this.phase.set('idle');
    this.result.set(null);
    this.serverError.set(null);
    this.clearFile();
  }

  /** Opens Rate Search for a saved route (or the first one) so the new rates are visible immediately. */
  protected goToSearch(route?: string): void {
    const [origin, destination] = (route ?? this.savedRoutes()[0] ?? '').split('-');
    this.router.navigate(['/rates'], { queryParams: origin && destination ? { origin, destination } : {} });
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private select(file: File | null): void {
    this.fileError.set(null);
    this.serverError.set(null);
    if (!file) {
      return;
    }

    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!(this.rateSheetService.allowedExtensions as readonly string[]).includes(extension)) {
      this.fileError.set(`"${file.name}" is not supported. Use a text PDF, XLSX, XLS or CSV file.`);
      return;
    }
    if (file.size === 0) {
      this.fileError.set(`"${file.name}" is empty.`);
      return;
    }
    if (file.size > this.rateSheetService.maxFileBytes) {
      this.fileError.set(`"${file.name}" is ${this.formatSize(file.size)}; the limit is ${this.maxMb} MB.`);
      return;
    }

    this.file.set(file);
    if (this.phase() !== 'idle') {
      this.phase.set('idle');
      this.result.set(null);
    }
  }
}
