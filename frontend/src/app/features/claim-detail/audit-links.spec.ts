import { AuditLogEntry } from '../../api/models';
import { auditEventFamily, auditEntityLink } from './audit-links';

const entry = (overrides: Partial<AuditLogEntry>): AuditLogEntry => ({
  auditLogId: 'a',
  createdAt: '2026-11-01T10:00:00Z',
  eventType: 'RESERVE_CREATED',
  description: 'x',
  userId: null,
  userDisplayName: null,
  relatedEntityType: null,
  relatedEntityId: null,
  ...overrides,
});

describe('auditEntityLink (spec 012 story 6)', () => {
  it.each([
    ['ReserveHistory', 'reserves', 'View reserve'],
    ['ClaimParty', 'parties', 'View party'],
    ['ClaimDocument', 'documents', 'View document'],
  ] as const)(
    'links a %s entry to the %s tab, highlighting that row',
    (relatedEntityType, tab, text) => {
      expect(auditEntityLink(entry({ relatedEntityType, relatedEntityId: 'e-1' }))).toEqual({
        tab,
        highlight: 'e-1',
        text,
      });
    },
  );

  it('has no link for claim-level entries or unknown entity types', () => {
    expect(auditEntityLink(entry({}))).toBeNull();
    expect(
      auditEntityLink(entry({ relatedEntityType: 'Mystery', relatedEntityId: 'e-1' })),
    ).toBeNull();
    expect(
      auditEntityLink(entry({ relatedEntityType: 'ClaimParty', relatedEntityId: null })),
    ).toBeNull();
  });
});

describe('auditEventFamily (badge colour by family: claim / status / reserve / document / party / system)', () => {
  it.each([
    ['CLAIM_CREATED', 'claim'],
    ['CLAIM_NOTES_UPDATED', 'claim'],
    ['VALIDATION_ISSUE_ADDED', 'claim'],
    ['STATUS_CHANGED', 'status'],
    ['RESERVE_APPROVED', 'reserve'],
    ['DOCUMENT_UPLOADED', 'document'],
    ['PARTY_REMOVED', 'party'],
    ['GL_POSTING_SIMULATED', 'system'],
    ['SLA_BREACH_DETECTED', 'system'],
    ['SOMETHING_NEW', 'system'],
  ])('%s → %s', (eventType, family) => {
    expect(auditEventFamily(eventType)).toBe(family);
  });
});
