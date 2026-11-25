# Cómo colaborar en el repo

Reglas para trabajar varias personas en 7 DÍAS sin pisarse. Si alguna no está clara, pregunta antes de subir.

## Ramas

- `main` es la rama estable y la lleva el dueño del repo. **Nadie sube directo a `main`.**
- Cada colaborador trabaja en su propia rama (por ejemplo `feature/historieta-v1`) y la une a `main` con un
  **Pull Request**.
- Antes de empezar a trabajar, trae lo nuevo de `main` a tu rama:

  ```bash
  git switch feature/mi-rama
  git pull origin main
  ```

- Al unir un Pull Request, usa **«Create a merge commit»** (el botón normal). «Squash» o «Rebase» cambian los
  commits y tu rama queda desfasada.

## Commits

- Pequeños y con un solo tema. Mensaje en español: `tipo(área): qué cambia`, por ejemplo
  `docs(guía): tipos de zombi y cómo vencerlos` o `fix(ui): el ranking no se cortaba`.
- Tipos habituales: `feat` (algo nuevo), `fix` (arreglo), `docs` (documentación), `chore` (mantenimiento).
- Configura tu nombre y tu correo de GitHub para que los commits cuenten como tuyos:

  ```bash
  git config user.name "Tu nombre"
  git config user.email "ID+usuario@users.noreply.github.com"   # GitHub > Settings > Emails
  ```

## Qué toca cada uno

- **Colaboradores:** `game/Assets/_Project/Intro/`, `game/Assets/_Project/UI/`, `docs/` y el `README.md`.
- **Solo el dueño:** combate, zombis, jefes, rig y animación, mapas (`Resources/Maps`), backend y
  autenticación, `ProjectSettings`, `Packages/manifest.json` y la escena `Game`.
- Si necesitas un cambio fuera de tu área, pídelo en el Pull Request o en un issue en lugar de hacerlo tú.

## Archivos

- Los `.meta` **no se crean a mano**: los genera Unity al abrir el proyecto. Los que genere sí se suben, junto
  con su archivo.
- Imágenes de 1-2 MB como máximo. **No se suben videos.**
- Los secretos van solo en `backend/.env` (copia de `.env.example`), que git ignora.

## Antes de pedir que se una a `main`

1. Unity compila sin errores rojos en la Console.
2. Test Runner > EditMode (y PlayMode) > Run All: todo en verde.
3. Si tocaste el backend: `npm test` en verde.
4. Si cambiaste algo que se ve en el juego, repasa la parte que toca de `docs/PRUEBAS_MANUALES.md`.
5. En la descripción del Pull Request: qué cambia, por qué y cómo probarlo.
