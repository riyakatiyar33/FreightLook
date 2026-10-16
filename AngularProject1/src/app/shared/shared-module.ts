import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';
import { AirlineChip } from './airline-chip/airline-chip';

const MATERIAL = [
  MatAutocompleteModule, MatButtonModule, MatCardModule, MatChipsModule, MatDividerModule,
  MatExpansionModule, MatFormFieldModule, MatIconModule, MatInputModule, MatPaginatorModule,
  MatProgressBarModule, MatSortModule, MatTableModule, MatTooltipModule,
];

/** Common Angular + Material modules and shared components for the lazy-loaded feature modules. */
@NgModule({
  declarations: [AirlineChip],
  imports: [CommonModule, ReactiveFormsModule, RouterModule, ...MATERIAL],
  exports: [CommonModule, ReactiveFormsModule, RouterModule, ...MATERIAL, AirlineChip],
})
export class SharedModule {}
