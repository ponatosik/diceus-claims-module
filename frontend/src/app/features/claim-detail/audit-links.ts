import { AuditLogEntry } from '../../api/models';

/** The tabs an audit entry can link into, and what the link says (spec 012 story 6). */
type LinkableTab = 'reserves' | 'parties' | 'documents';

const LINK_FOR_ENTITY: Record<string, { tab: LinkableTab; text: string }> = {
  ReserveHistory: { tab: 'reserves', text: 'View reserve' },
  ClaimParty: { tab: 'parties', text: 'View party' },
  ClaimDocument: { tab: 'documents', text: 'View document' },
};

export interface AuditEntityLink {
  tab: LinkableTab;
  highlight: string;
  text: string;
}

/** The in-claim link for an audit entry's related entity — `?tab=…&highlight=<id>` — or null for claim-level entries. */
export function auditEntityLink(entry: AuditLogEntry): AuditEntityLink | null {
  const link =
    entry.relatedEntityType === null ? undefined : LINK_FOR_ENTITY[entry.relatedEntityType];
  return link === undefined || entry.relatedEntityId === null
    ? null
    : { ...link, highlight: entry.relatedEntityId };
}

/** §11.3 / spec 012: event-type badges are coloured by family, not one colour per event type. */
export type AuditEventFamily = 'claim' | 'status' | 'reserve' | 'document' | 'party' | 'system';

const FAMILY_BY_PREFIX: [string, AuditEventFamily][] = [
  ['CLAIM_', 'claim'],
  ['VALIDATION_', 'claim'],
  ['STATUS_', 'status'],
  ['RESERVE_', 'reserve'],
  ['DOCUMENT_', 'document'],
  ['PARTY_', 'party'],
];

/** Events no user action produced directly — GL postings, SLA breaches, anything new — are "system". */
export function auditEventFamily(eventType: string): AuditEventFamily {
  return FAMILY_BY_PREFIX.find(([prefix]) => eventType.startsWith(prefix))?.[1] ?? 'system';
}
