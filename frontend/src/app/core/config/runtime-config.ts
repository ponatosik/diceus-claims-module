import { InjectionToken } from '@angular/core';

/**
 * Deployment-specific settings read at runtime from `/assets/config.json` (fetched in main.ts before
 * bootstrap), so one build artifact is promoted unchanged between local, docker and Azure (spec 007 story 3).
 */
export interface RuntimeConfig {
  /** Origin of the API, e.g. `https://claims-api.azurewebsites.net`; empty string = same origin (dev proxy / nginx). */
  apiBaseUrl: string;
}

export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>('RUNTIME_CONFIG');

export async function loadRuntimeConfig(fetchFn: typeof fetch = fetch): Promise<RuntimeConfig> {
  const response = await fetchFn('/assets/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Could not load /assets/config.json (HTTP ${response.status}).`);
  }
  const config = (await response.json()) as Partial<RuntimeConfig>;
  return { apiBaseUrl: (config.apiBaseUrl ?? '').replace(/\/+$/, '') };
}
