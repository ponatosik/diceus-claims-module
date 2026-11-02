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
    ['ReserveHistory', 'reserves'],
    ['ClaimParty', 'parties'],
    ['ClaimDocument', 'documents'],
  ] as const)('links a %s entry to the %s tab, highlighting that row', (relatedEntityType, tab) => {
    expect(auditEntityLink(entry({ relatedEntityType, relatedEntityId: 'e-1' }))).toEqual({
      tab,
      highlight: 'e-1',
    });
  });

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

describe('auditEventFamily (badge colour by family, spec 012)', () => {
  it.each([
    ['CLAIM_CREATED', 'claim'],
    ['CLAIM_NOTES_UPDATED', 'claim'],
    ['VALIDATION_ISSUE_ADDED', 'claim'],
    ['SLA_BREACH_DETECTED', 'claim'],
    ['STATUS_CHANGED', 'status'],
    ['RESERVE_APPROVED', 'reserve'],
    ['GL_POSTING_SIMULATED', 'gl'],
    ['DOCUMENT_UPLOADED', 'document'],
    ['PARTY_REMOVED', 'party'],
    ['SOMETHING_NEW', 'other'],
  ])('%s → %s', (eventType, family) => {
    expect(auditEventFamily(eventType)).toBe(family);
  });
});
