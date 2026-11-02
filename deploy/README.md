# Deployment Baseline

This folder contains the production deployment baseline for the API, Redis, and future Unity dedicated game nodes.

## Before deployment

1. Rotate all database, Redis, and JWT secrets that were previously committed in application settings.
2. Create `deploy/.env` from `.env.example` on the target host.
3. Replace all database and Redis endpoints with private-network addresses.
4. Confirm Redis AOF is enabled with `appendfsync everysec`.
5. Build and publish a Linux Unity dedicated-server artifact before enabling the `game` Compose profile.

## API and Redis

Run from the repository root on the host:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d --build redis api
```

## Game node image contract

The game node must not start accepting players until it has:

1. Loaded and validated versioned static JSON configuration.
2. Connected to Redis and MySQL using private-network endpoints.
3. Registered `ServerNodeId`, `BuildVersion`, `LineId`, capacity, and a TTL-backed health record.
4. Acquired a unique player-session lease with a monotonic fencing token.

New lines accept only newly assigned players first. Player or dungeon migration is a later feature and must use a snapshot handoff protocol.

## Production host

The target host is `39.107.141.107`. Actual remote deployment requires an SSH account or an existing CI/CD runner with deployment permission. Do not expose Redis or MySQL ports publicly.

## Node plane (Phase 11 - dynamic line opening)

The control plane is the API process itself (NodeRegistryService): game nodes self-register, heartbeat, and are health-probed there. Lines (LineData) are created per node registration and scheduled least-connections-first by ShardManager.

### Node lifecycle contract

- Startup: POST /api/node/register {nodeId, host, mirrorPort, capacity, buildVersion} + X-Node-Key header. Allocates a dedicated line (ids start at 100); re-registration reuses the same line.
- Running: POST /api/node/heartbeat {nodeId, load, accepting} every 10s; load = Mirror connection count.
- Graceful stop: POST /api/node/deregister {nodeId}. Line taken down immediately.
- Crash (no heartbeat): the API probe loop removes the node after Arch__NodeHeartbeatTimeoutSeconds (default 45s); the corpse stays in /api/node/list for ops.

Failure handling on removal: the node's line stops accepting players, and every Active session owned by that node transitions to Detached (3-minute reconnect lease). On reconnect (POST /api/session/reconnect) the session is reassigned to a healthy node's line (fencing token incremented) and the response carries the new nodeHost/nodePort - the client connects to the new Mirror node and restores dungeon progress via POST /api/dungeon/load (phase 06 snapshots).

### Unity dedicated-server launch flags

ArpgGameServer.x86_64 -batchmode -nographics -port 7777 -webApiUrl http://api:5132 -nodeId cn-bj-01-node-01 -publicHost <client-reachable-ip> -nodeKey <NODE_KEY from deploy/.env>

### Ops / verification endpoints

- GET /api/node/list - full registry incl. removed corpses (health, load, line binding)
- POST /api/node/probe (X-GM-Key) - trigger the timeout check on demand (used by the phase 11 acceptance harness)
- GET /api/session/lines - line occupancy and health
- GET /healthz - API liveness

Phase 11 acceptance: dotnet run --project Server/ArchTest -c Release -- phase11 (18 checks: registration, capacity scheduling, failure removal, reconnect reassignment with fencing increment, heartbeat recovery, graceful deregister, auth rejection).
