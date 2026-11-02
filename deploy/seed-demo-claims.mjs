// Seeds a handful of realistic claims into a deployed environment — through the public API, as the
// ADR-0010 demo users — so the live demo isn't an empty dashboard and every seeded claim carries a
// genuine audit trail (spec 013 story 10). Skips itself if the environment already has claims.
// Usage: node deploy/seed-demo-claims.mjs <api-url>

const apiUrl = (process.argv[2] ?? '').replace(/\/+$/, '');
if (!apiUrl) throw new Error('usage: node deploy/seed-demo-claims.mjs <api-url>');

const users = {
  handler: '00000000-0000-0000-0003-000000000001',
  supervisor: '00000000-0000-0000-0003-000000000002',
};

async function tokenFor(userId) {
  const response = await fetch(`${apiUrl}/api/dev/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId }),
  });
  if (!response.ok) throw new Error(`token for ${userId}: ${response.status}`);
  return (await response.json()).accessToken;
}

function client(token) {
  return async function call(method, path, body) {
    const isForm = body instanceof FormData;
    const response = await fetch(`${apiUrl}${path}`, {
      method,
      headers: { Authorization: `Bearer ${token}`, ...(isForm || body === undefined ? {} : { 'Content-Type': 'application/json' }) },
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    });
    if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${await response.text()}`);
    const text = await response.text();
    return text === '' ? undefined : JSON.parse(text);
  };
}

const daysAgo = (days) => new Date(Date.now() - days * 86_400_000).toISOString();

const handler = client(await tokenFor(users.handler));
const supervisor = client(await tokenFor(users.supervisor));

const existing = await handler('GET', '/api/claims?pageSize=1');
if (existing.totalCount > 0) {
  console.log(`Environment already has ${existing.totalCount} claims — skipping the demo seed.`);
  process.exit(0);
}

async function policyId(query) {
  const [policy] = await handler('GET', `/api/policies/search?q=${encodeURIComponent(query)}`);
  if (policy === undefined) throw new Error(`No seeded policy matches "${query}" — has the database been migrated?`);
  return policy.policyId;
}
const person = (partyRole, firstName, lastName, extra = {}) => ({ partyRole, partyType: 'Person', firstName, lastName, ...extra });

async function createClaim({ policy, lossDaysAgo, cause, description, location, estimate, parties, riskObjects = [], initialReserve = null }) {
  return handler('POST', '/api/claims', {
    policyId: policy === null ? null : await policyId(policy),
    lossDate: daysAgo(lossDaysAgo),
    lossDescription: description,
    lossLocation: location,
    causeOfLossCode: cause,
    estimatedLossAmount: estimate,
    parties,
    riskObjects,
    initialReserve,
  });
}

const transition = (claimId, targetStatus, extra = {}) => handler('PUT', `/api/claims/${claimId}/status`, { targetStatus, ...extra });
const reserve = (claimId, component, amount, changeReason, transactionType = 'Add') =>
  handler('POST', `/api/claims/${claimId}/reserves`, { component, amount, changeReason, transactionType });

// 1. Fresh FNOL left in Draft.
const draft = await createClaim({
  policy: 'POL-2024-001001',
  lossDaysAgo: 1,
  cause: 'COL-VEH-COL',
  description: 'Trailer jack-knifed on the I-84 on-ramp in heavy rain; cargo shifted and the trailer side wall split.',
  location: 'I-84 eastbound, mile 12',
  estimate: 18500,
  parties: [person('Claimant', 'Dana', 'Whitfield', { email: 'dana.whitfield@example.com' })],
  riskObjects: [{ assetType: 'Vehicle', assetDescription: '2021 Utility 3000R reefer trailer', damageDescription: 'Side wall split, rear axle bent', isPrimary: true, assetReference: 'TRL-4471' }],
});

// 2. Open, with an auto-approved initial reserve, a witness and a police report.
const open = await createClaim({
  policy: 'POL-2024-001002',
  lossDaysAgo: 6,
  cause: 'COL-FLOOD',
  description: 'A burst supply line flooded the ground-floor retail unit overnight; flooring and stock damaged.',
  location: '14 Harbor Way, Unit 2',
  estimate: 9200,
  parties: [
    { partyRole: 'Claimant', partyType: 'Company', companyName: 'Harborview Properties Inc', email: 'claims@harborview.example.com' },
    person('Witness', 'Luis', 'Ortega', { phone: '555-0142' }),
  ],
  riskObjects: [{ assetType: 'Property', assetDescription: 'Ground-floor retail unit', damageDescription: 'Water damage to flooring and stock', isPrimary: true, assetReference: null }],
  initialReserve: { component: 'Indemnity', amount: 8000, changeReason: 'Initial estimate from site photos' },
});
await transition(open.claimId, 'Open');
const report = new FormData();
report.append('File', new Blob(['Incident report #HV-2291 — building manager attended 06:40, isolated the supply line.'], { type: 'text/plain' }), 'incident-report.txt');
report.append('DocumentType', 'PoliceReport');
await handler('POST', `/api/claims/${open.claimId}/documents`, report);

// 3. Under investigation, with a supervisor-tier reserve awaiting approval.
const investigating = await createClaim({
  policy: 'POL-2025-002001',
  lossDaysAgo: 12,
  cause: 'COL-EQUIP',
  description: 'Tower crane slewing motor failed mid-lift; load lowered safely but the motor and gearbox are destroyed.',
  location: 'Coastal Builders site, Pier 9 redevelopment',
  estimate: 62000,
  parties: [person('Claimant', 'Grace', 'Okoye', { email: 'g.okoye@coastalbuilders.example.com' })],
  riskObjects: [{ assetType: 'Equipment', assetDescription: 'Liebherr 172 EC-B tower crane', damageDescription: 'Slewing motor and gearbox failure', isPrimary: true, assetReference: 'CR-0172' }],
});
await transition(investigating.claimId, 'Open');
await transition(investigating.claimId, 'UnderInvestigation');
await reserve(investigating.claimId, 'Expense', 4500, 'Independent engineer inspection');
await reserve(investigating.claimId, 'Indemnity', 45000, 'Motor and gearbox replacement quote');

// 4. Liability accepted: a supervisor-approved reserve, moved to Pending Payment.
const payable = await createClaim({
  policy: 'POL-2025-002002',
  lossDaysAgo: 21,
  cause: 'COL-LIAB',
  description: 'Visitor slipped on a freshly mopped corridor without signage and fractured a wrist.',
  location: 'Stanton Medical Group, Westside clinic',
  estimate: 30000,
  parties: [
    person('Claimant', 'Peter', 'Novak', { email: 'p.novak@example.com' }),
    { partyRole: 'Insured', partyType: 'Company', companyName: 'Stanton Medical Group', email: 'risk@stanton.example.com' },
  ],
});
await transition(payable.claimId, 'Open');
const liability = await reserve(payable.claimId, 'Indemnity', 28000, 'Settlement range agreed with counsel');
await supervisor('POST', `/api/claims/${payable.claimId}/reserves/${liability.reserveHistoryId}/approve`);
await transition(payable.claimId, 'PendingPayment');

// 5. A small claim opened and closed.
const closed = await createClaim({
  policy: 'POL-2024-001001',
  lossDaysAgo: 40,
  cause: 'COL-THEFT',
  description: 'Fuel card and handheld scanner stolen from an unlocked cab at a truck stop.',
  location: 'Pilot Travel Center, Exit 33',
  estimate: 650,
  parties: [person('Claimant', 'Marcus', 'Bell')],
});
await transition(closed.claimId, 'Open');
await transition(closed.claimId, 'Closed', { reason: 'Below deductible; no payment due.' });

// 6. No policy found at FNOL — a Draft carrying the "no policy linked" warning.
await createClaim({
  policy: null,
  lossDaysAgo: 2,
  cause: 'COL-WIND',
  description: 'Storm tore part of a warehouse roof membrane; caller could not locate their policy number.',
  location: 'Unit 7, Riverside Industrial Estate',
  estimate: 12000,
  parties: [person('Claimant', 'Aisha', 'Rahman', { phone: '555-0199' })],
});

console.log(`Seeded 6 demo claims (first: ${draft.claimNumber}).`);
