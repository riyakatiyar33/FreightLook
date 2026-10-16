import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { UploadRateSheet } from './upload-rate-sheet';

@NgModule({
  declarations: [UploadRateSheet],
  imports: [SharedModule, RouterModule.forChild([{ path: '', component: UploadRateSheet }])],
})
export class UploadRateSheetModule {}
