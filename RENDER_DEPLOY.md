# Deploy VailAC to Render

## Files included
- `render.yaml` at the repository root: Render Blueprint definition.
- `VailACServer/Dockerfile`: builds and runs the ASP.NET Core backend.
- `VailACServer/render.py`: container entrypoint that starts the published .NET app.
- `VailACServer/.dockerignore`: excludes local build/editor files from Docker context.

## Deploy with a Blueprint
1. Extract this ZIP.
2. Push the **contents** of the extracted folder to the root of a GitHub repository. The root must contain both `render.yaml` and the `VailACServer/` folder.
3. In Render, choose **New + → Blueprint** and connect that repository.
4. Render reads the root `render.yaml`. It builds the Dockerfile in `VailACServer/`.
5. Set the two secret values when prompted:
   - `AntiCheat__AdminPassword`: the password used to sign in to the VailAC admin dashboard.
   - `AntiCheat__GameServerKey`: a different, long random secret used only by your trusted game-server backend.
6. Apply the Blueprint and wait for the deploy to finish.
7. Open the service's `https://…onrender.com` URL. The dashboard is served at `/`; health check is `/health`.

## Important before first deploy
The server intentionally refuses to start while `VailACServer/known-hashes.json` contains placeholder hashes. Replace them with real SHA-256 values from the clean, trusted game release build before deploying. Do not invent hashes or copy them from an untrusted player installation.

Do not commit either secret to GitHub. Keep `AntiCheat__GameServerKey` out of the Unity client and browser code. The dashboard's shared-password login is a starter implementation; harden it with individual accounts, MFA, CSRF protection, rate limiting and audit logging before public production use.

The Blueprint requests a persistent disk for SQLite storage and uses the `starter` service plan. Review Render's current pricing and plan limits before applying it.
