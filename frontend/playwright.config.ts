import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests against the real stack (docker-compose: SQL Server + API + nginx-served frontend).
 * Start it first with `docker compose up --build -d` from the repo root, then `npm run e2e`.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    viewport: { width: 1440, height: 900 },
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
  ],
});
