import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { RateSearch } from './rate-search';

@NgModule({
  declarations: [RateSearch],
  imports: [SharedModule, RouterModule.forChild([{ path: '', component: RateSearch }])],
})
export class RateSearchModule {}
