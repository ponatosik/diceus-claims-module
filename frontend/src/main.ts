import { bootstrapApplication } from '@angular/platform-browser';
import { mergeApplicationConfig } from '@angular/core';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { RUNTIME_CONFIG, loadRuntimeConfig } from './app/core/config/runtime-config';

// Runtime config is fetched before bootstrap so it is available synchronously through DI everywhere
// (spec 007 story 3): one build artifact, configured per environment by /assets/config.json.
loadRuntimeConfig()
  .then((runtimeConfig) =>
    bootstrapApplication(
      App,
      mergeApplicationConfig(appConfig, {
        providers: [{ provide: RUNTIME_CONFIG, useValue: runtimeConfig }],
      }),
    ),
  )
  .catch((error: unknown) => console.error(error));
