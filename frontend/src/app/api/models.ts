// TypeScript mirrors of the backend DTOs (ClaimsModule.Application/**/Dtos and query/command records).
// Enums are string-literal unions matching JsonStringEnumConverter output. Dates: DateTimeOffset →
// ISO-8601 string with offset; DateOnly → 'YYYY-MM-DD'. Money: number (backend DECIMAL(19,4)).

import { BlockingCondition } from '../core/errors/api-error';
import { ClaimStatus } from '../shared/domain/claim-status';

export type { ClaimStatus } from '../shared/domain/claim-status';
export type { BlockingCondition } from '../core/errors/api-error';

export type PartyRole = 'Claimant' | 'Insured' | 'ThirdParty' | 'Witness' | 'Attorney';
export type PartyType = 'Person' | 'Company';
export type AssetType = 'Vehicle' | 'Property' | 'Person' | 'Equipment' | 'Other';
export type ClaimSeverity = 'Minor' | 'Standard' | 'Critical' | 'Catastrophic';
export type ReserveComponentType = 'Indemnity' | 'Expense' | 'ALAE' | 'SubrogationRecoverable';
export type ReserveTransactionType = 'Add' | 'Adjust' | 'Reverse';
export type ReserveApprovalStatus =
  'AutoApproved' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type ReservePostingStatus = 'Pending' | 'Posted' | 'Failed' | 'Cancelled';
export type ReserveComponentStatus = 'Active' | 'Closed';
export type PolicyStatus = 'Active' | 'Expired' | 'Cancelled';
export type PerilCategory =
  'Property' | 'Auto' | 'Liability' | 'Weather' | 'Equipment' | 'Crime' | 'General';
export type ValidationSeverity = 'Error' | 'Warning';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ---- Claims ----------------------------------------------------------------------------------

export interface ClaimSummary {
  claimId: string;
  claimNumber: string;
  clientName: string | null;
  policyNumber: string | null;
  lossDate: string;
  status: ClaimStatus;
  totalReserves: number;
  causeOfLossCode: string | null;
  causeOfLossName: string | null;
}

export interface ClaimListQuery {
  statuses?: ClaimStatus[];
  /** Loss date range, `YYYY-MM-DD`. */
  dateFrom?: string;
  dateTo?: string;
  /** Partial, case-insensitive match on the handler's display name. */
  assignedHandler?: string;
  assignedHandlerId?: string;
  causeOfLossCode?: string;
  policyId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface LossEvent {
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  estimatedLossAmount: number | null;
}

export interface ClaimParty {
  claimPartyId: string;
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  isActive: boolean;
}

export interface ClaimRiskObject {
  claimRiskObjectId: string;
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

export interface UserSummary {
  userId: string;
  displayName: string | null;
}

export interface ReserveSummary {
  reserveComponentId: string;
  component: ReserveComponentType;
  currentAmount: number;
  status: ReserveComponentStatus;
  pendingAmount: number;
}

export interface ClaimDocumentSummary {
  claimDocumentId: string;
  documentName: string;
  documentType: string;
  uploadedAt: string;
}

export interface AuditLogEntry {
  auditLogId: string;
  createdAt: string;
  eventType: string;
  description: string;
  userId: string | null;
  userDisplayName: string | null;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
}

/** Returned by create and status-transition commands. */
export interface Claim {
  claimId: string;
  claimNumber: string;
  status: ClaimStatus;
  policyId: string | null;
  policyNumber: string | null;
  clientName: string | null;
  reportedDate: string;
  lossEvent: LossEvent;
  parties: ClaimParty[];
  riskObjects: ClaimRiskObject[];
}

export interface ClaimDetail extends Claim {
  reserves: ReserveSummary[];
  documents: ClaimDocumentSummary[];
  recentAuditEntries: AuditLogEntry[];
  reserveOverrideApproved: boolean;
  severity: ClaimSeverity | null;
  notes: string | null;
  assignedHandler: UserSummary | null;
  causeOfLossName: string | null;
}

export interface CreatePartyRequest {
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
}

export interface CreateRiskObjectRequest {
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

export interface InitialReserveRequest {
  component: ReserveComponentType;
  amount: number;
  changeReason: string;
}

/** Body of POST /api/claims and POST /api/claims/validate. */
export interface CreateClaimRequest {
  policyId: string | null;
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  estimatedLossAmount: number | null;
  parties: CreatePartyRequest[];
  riskObjects: CreateRiskObjectRequest[];
  initialReserve: InitialReserveRequest | null;
}

export interface ValidationIssue {
  propertyName: string;
  message: string;
  severity: ValidationSeverity;
}

export interface TransitionRequest {
  targetStatus: ClaimStatus;
  reason?: string | null;
  justificationNote?: string | null;
}

export interface TransitionPreflight {
  isTransitionValid: boolean;
  blockingConditions: BlockingCondition[];
  warnings: BlockingCondition[];
  requiresReason: boolean;
  requiresJustificationNote: boolean;
}

// ---- Reserves --------------------------------------------------------------------------------

export interface ReserveTransaction {
  reserveHistoryId: string;
  reserveComponentId: string;
  component: ReserveComponentType;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  approvedByUserId: string | null;
  approvedAt: string | null;
  rejectedByUserId: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  changeReason: string;
  postingStatus: ReservePostingStatus;
  submittedByUserId: string | null;
  createdAt: string;
  submittedByDisplayName: string | null;
  approvedByDisplayName: string | null;
  rejectedByDisplayName: string | null;
}

export interface ReserveList {
  components: ReserveSummary[];
  history: ReserveTransaction[];
}

export interface OpenReserveRequest {
  component: ReserveComponentType;
  amount: number;
  changeReason: string;
  transactionType: ReserveTransactionType;
}

// ---- Documents -------------------------------------------------------------------------------

export interface ClaimDocument {
  claimDocumentId: string;
  claimId: string;
  documentType: string;
  documentName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string | null;
  notes: string | null;
  downloadUrl: string;
  uploadedByDisplayName: string | null;
}

// ---- Policies & reference data ---------------------------------------------------------------

export interface PolicySearchResult {
  policyId: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  status: PolicyStatus;
}

export interface PolicyCoverage {
  policyId: string;
  policyNumber: string;
  coverageTypes: string[];
  effectiveDate: string;
  expirationDate: string;
  status: PolicyStatus;
}

export interface CauseOfLossCode {
  code: string;
  name: string;
  perilCategory: PerilCategory;
  sortOrder: number;
}

export interface ClaimStatusReference {
  status: ClaimStatus;
  validNextStatuses: ClaimStatus[];
}
