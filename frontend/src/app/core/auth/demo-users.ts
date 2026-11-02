/** §3 role codes, as carried in the JWT role claim. */
export type Role = 'handler' | 'supervisor' | 'manager';

/** GLOSSARY.md "Demo User" — one of three fixed simulated identities (ADR-0010). */
export interface DemoUser {
  userId: string;
  displayName: string;
  role: Role;
}

/** Mirrors the backend's seeded Users table (Persistence SeedData / UserConfiguration) exactly. */
export const DEMO_USERS: readonly DemoUser[] = [
  { userId: '00000000-0000-0000-0003-000000000001', displayName: 'Alex Morgan', role: 'handler' },
  { userId: '00000000-0000-0000-0003-000000000002', displayName: 'Priya Shah', role: 'supervisor' },
  { userId: '00000000-0000-0000-0003-000000000003', displayName: 'Daniel Okafor', role: 'manager' },
];
