import { execSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import path from 'node:path';

// Recrea la BD de pruebas (prisma/test.db, archivo desechable) antes de correr los tests.
export default function setup() {
  const dbFile = path.join(__dirname, '..', '..', 'prisma', 'test.db');
  rmSync(dbFile, { force: true });
  rmSync(`${dbFile}-journal`, { force: true });
  execSync('npx prisma db push --skip-generate', {
    env: { ...process.env, DATABASE_URL: 'file:./test.db' },
    stdio: 'pipe',
  });
}
