# API del backend (Fase 1)

Base: `http://localhost:3001`. Errores: `{ "error": { "code", "message", "details?" } }`.
Rutas protegidas: cabecera `Authorization: Bearer <accessToken>`.

| Método | Ruta | Auth | Cuerpo | Respuesta |
|--------|------|------|--------|-----------|
| GET | `/health` | no | - | `{status:"ok"}` |
| POST | `/api/auth/register` | no | `email, username (3-20, a-zA-Z0-9_), password (8-128)` | 201 `{user, accessToken, refreshToken}`; 409 si ya existe |
| POST | `/api/auth/login` | no | `email, password` | 200 igual que register; 401; 429 tras 10 intentos / 15 min |
| POST | `/api/auth/refresh` | no | `refreshToken` | 200 `{accessToken, refreshToken}` (el anterior queda invalidado) |
| POST | `/api/auth/logout` | no | `refreshToken` | 204 (revoca la sesión) |
| GET | `/api/me` | sí | - | `{user}` |
| GET | `/api/progress` | sí | - | `{progress:{currentDay, inventory, stats, updatedAt}}` (día 1 si no hay guardado) |
| PUT | `/api/progress` | sí | `currentDay (1-7), inventory (objeto), stats {kills, shotsFired, shotsHit, deaths}` | `{progress}` |
| POST | `/api/rankings` | sí | `score, daysSurvived (0-7), kills, victory` | 201 `{ranking}` |
| GET | `/api/rankings?limit=20` | sí | - | `{rankings:[{position, username, score, ...}]}` |

## Planeado (aún no implementado)
| Método | Ruta | Auth | Fase | Respuesta |
|--------|------|------|------|-----------|
| GET | `/api/maps` | sí | 3 | Lista de mapas `{id, name, baseDifficulty, origin}` |
| GET | `/api/maps/:id` | sí | 3 | Mapa completo (perímetro, spawns, casa central, loot) validado con zod |
| GET | `/api/content/:type` | sí | 5 | Contenido guardado (`zombie`, `weapon`, `character`, `boss`); solo lectura |

Reglas de diseño:
- Los mapas viven en la tabla `maps` (nombre, JSON, dificultad base, origen `seed` | `gemini` | `procedural`). Un seed inicial trae 3-5 mapas completos.
- **No existe ningún endpoint que genere contenido con Gemini en caliente**, y el backend nunca llama a Gemini mientras se juega.
- Gemini se usa solo con el script manual `npm run content:generate` (lotes pequeños, reintentos con backoff, límite de peticiones por ejecución, key en `backend/.env`). Lo generado se valida con zod y se guarda con origen `gemini`. Si no se ejecuta, el juego usa el contenido del seed.
- `generated_content.type` queda para `zombie | weapon | character | boss`; los mapas pasan a su propia tabla.

## Seguridad implementada
- Contraseñas con argon2; el hash nunca sale en las respuestas.
- Access token JWT (HS256, 15 min). Refresh token aleatorio, guardado como hash SHA-256, rotativo: reutilizar uno viejo revoca toda la sesión.
- Login con tiempo constante frente a emails inexistentes y rate limiting.
- zod en todas las entradas, helmet, CORS por lista de orígenes, cuerpo máximo 100 KB.
- Nota: el ranking confía en el puntaje que envía el cliente (suficiente para la demo; un servidor autoritativo queda fuera de alcance).

## Levantar el backend
```bash
cd backend
npm install
cp ../.env.example .env        # y pon en JWT_ACCESS_SECRET el resultado de: openssl rand -hex 32
npx prisma generate
npm run db:push                # crea prisma/dev.db
npm run dev                    # http://localhost:3001
npm test                       # 17 pruebas
```
