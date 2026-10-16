export type SlabCode = 'M' | '+45' | '+100' | '+250' | '+300' | '+500' | '+1000';

/** Slab codes in ascending weight order (matches Models.Constants.SlabCodes). */
export const SLAB_CODES: readonly SlabCode[] = ['M', '+45', '+100', '+250', '+300', '+500', '+1000'];

/** KG = per chargeable kg, SHIPMENT = flat per AWB. */
export type SurchargeBasis = 'KG' | 'SHIPMENT';

/** Matches Models.Dtos.SurchargeDto. */
export interface Surcharge {
  type: string;
  amount: number;
  minAmount: number | null;
  basis: SurchargeBasis;
}

/** Matches Models.Dtos.RateSearchResultDto. */
export interface RateSearchResult {
  rateId: number;
  airlineCode: string;
  airlineName: string;
  originCode: string;
  originName: string;
  destinationCode: string;
  destinationName: string;
  /** Special commodity (Courier, DGR, ...). Empty = general cargo. */
  commodity: string;
  currency: string;
  minimumCharge: number;
  slabs: Partial<Record<SlabCode, number>>;
  surcharges: Surcharge[];
  /** Set only when a weight was searched. */
  applicableSlab: SlabCode | null;
  freightCharge: number | null;
  surchargeTotal: number | null;
  estimatedTotal: number | null;
  updatedAt: string;
}

/** Matches Models.Responses.RateSearchResponse. */
export interface RateSearchResponse {
  origin: string;
  destination: string;
  weight: number | null;
  count: number;
  results: RateSearchResult[];
}

/** Query for GET /api/rates/search (Models.Requests.RateSearchRequest). */
export interface RateSearchQuery {
  origin: string;
  destination: string;
  weight?: number | null;
}
