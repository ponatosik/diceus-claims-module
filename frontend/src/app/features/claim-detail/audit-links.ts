import { AuditLogEntry } from '../../api/models';
import { ClaimTab } from './claim-tabs';

/** Which Claim Detail tab shows each audit-referenced entity (spec 012 story 6). */
const TAB_FOR_ENTITY: Record<string, ClaimTab> = {
  ReserveHistory: 'reserves',
  ClaimParty: 'parties',
  ClaimDocument: 'documents',
};

export interface AuditEntityLink {
  tab: ClaimTab;
  highlight: string;
}

/** The in-claim link for an audit entry's related entity — `?tab=…&highlight=<id>` — or null for claim-level entries. */
export function auditEntityLink(entry: AuditLogEntry): AuditEntityLink | null {
  const tab =
    entry.relatedEntityType === null ? undefined : TAB_FOR_ENTITY[entry.relatedEntityType];
  return tab === undefined || entry.relatedEntityId === null
    ? null
    : { tab, highlight: entry.relatedEntityId };
}

export type AuditEventFamily =
  'claim' | 'status' | 'reserve' | 'gl' | 'party' | 'document' | 'other';

const FAMILY_BY_PREFIX: [string, AuditEventFamily][] = [
  ['CLAIM_', 'claim'],
  ['VALIDATION_', 'claim'],
  ['SLA_', 'claim'],
  ['STATUS_', 'status'],
  ['RESERVE_', 'reserve'],
  ['GL_', 'gl'],
  ['PARTY_', 'party'],
  ['DOCUMENT_', 'document'],
];

/** Event-type badges are coloured by family rather than one colour per event type (spec 012). */
export function auditEventFamily(eventType: string): AuditEventFamily {
  return FAMILY_BY_PREFIX.find(([prefix]) => eventType.startsWith(prefix))?.[1] ?? 'other';
}
