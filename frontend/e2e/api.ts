import { APIRequestContext } from '@playwright/test';

import { DEMO_USERS, Role } from '../src/app/core/auth/demo-users';

/** Fixed Demo User ids (ADR-0010), from the app's single source of truth. */
export const DEMO_USER_IDS = Object.fromEntries(
  DEMO_USERS.map((u) => [u.role, u.userId]),
) as Record<Role, string>;

export interface SeededClaim {
  claimId: string;
  claimNumber: string;
}

/** Seeds test data straight through the real API, as a given Demo User. */
export class ApiSeeder {
  private constructor(
    private readonly request: APIRequestContext,
    private readonly token: string,
  ) {}

  static async as(
    request: APIRequestContext,
    role: keyof typeof DEMO_USER_IDS,
  ): Promise<ApiSeeder> {
    const response = await request.post('/api/dev/auth/token', {
      data: { userId: DEMO_USER_IDS[role] },
    });
    const { accessToken } = (await response.json()) as { accessToken: string };
    return new ApiSeeder(request, accessToken);
  }

  async createClaim(): Promise<SeededClaim> {
    const response = await this.request.post('/api/claims', {
      headers: this.headers(),
      data: {
        lossDate: new Date(Date.now() - 86_400_000).toISOString(),
        lossDescription: 'A pipe burst and flooded the basement overnight.',
        causeOfLossCode: 'COL-FIRE',
        parties: [
          { partyRole: 'Claimant', partyType: 'Person', firstName: 'Jane', lastName: 'Doe' },
        ],
        riskObjects: [],
      },
    });
    if (!response.ok())
      throw new Error(`createClaim failed: ${response.status()} ${await response.text()}`);
    return (await response.json()) as SeededClaim;
  }

  async transition(claimId: string, targetStatus: string, reason?: string): Promise<void> {
    const response = await this.request.put(`/api/claims/${claimId}/status`, {
      headers: this.headers(),
      data: { targetStatus, reason },
    });
    if (!response.ok())
      throw new Error(`transition failed: ${response.status()} ${await response.text()}`);
  }

  private headers() {
    return { Authorization: `Bearer ${this.token}` };
  }
}
