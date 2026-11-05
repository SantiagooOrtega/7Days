import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globalSetup: ['./src/tests/globalSetup.ts'],
    fileParallelism: false,
    env: {
      NODE_ENV: 'test',
      DATABASE_URL: 'file:./test.db',
      JWT_ACCESS_SECRET: 'test-secret-test-secret-test-secret-123',
      JWT_ACCESS_TTL: '15m',
      REFRESH_TOKEN_TTL_DAYS: '7',
      CORS_ORIGINS: 'http://localhost:3000',
    },
  },
});
