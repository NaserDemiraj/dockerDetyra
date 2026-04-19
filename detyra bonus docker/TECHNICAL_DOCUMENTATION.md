# Technical Documentation

## 1) Docker Isolation Guarantees

This platform executes user code in a **fresh Docker container per request**.  
Execution flow is: **Create container → Copy code → Execute → Destroy container**.

### Process and user isolation
- Each execution gets its own Linux process namespace and PID tree.
- A timeout/infinite loop affects only that execution container.
- Another user request starts in a different container and is not blocked by a stuck process in another container.

### Resource limits
- Containers are started with:
  - `-m 512m` (memory limit)
  - `--cpus 0.5` (CPU limit)
- If memory is exceeded, Docker can mark container state as `OOMKilled`, which is surfaced to the API as a memory-limit error.

### Network isolation
- Execution containers are run with `--network none`.
- User code cannot reach external/internal network services from the worker container.

### Filesystem isolation
- User code is copied into `/tmp` inside the execution container.
- No host filesystem bind mount is provided to the execution container.
- Container is deleted after execution, so temporary files do not persist between runs.

---

## 2) Single Point of Failure (SPOF) Analysis

### Docker daemon SPOF
**Yes, Docker daemon is a SPOF** in the current single-host design.  
If Docker service stops, code execution fails for all users.

Mitigations:
- Health checks + automatic Docker service restart
- Multi-node deployment (Kubernetes / Docker Swarm) with scheduler failover
- Queue-based execution workers distributed across multiple hosts

### Database SPOF
Current DB instance is also a SPOF.  
If DB is unavailable, login/register and execution history fail.

Mitigations:
- Automated backups and restore drills
- Primary/replica setup
- Managed DB with HA and failover

### Backend API SPOF
A single backend instance is a SPOF.

Mitigations:
- Run multiple API replicas behind a load balancer
- Use readiness/liveness probes and rolling deployments
- Externalize session/auth state so replicas are stateless

---

## 3) Scalability to 100 Users

### Current capacity (single host baseline)
Capacity depends on host RAM/CPU and Docker scheduling overhead.

### Primary bottlenecks
- Docker socket/daemon contention
- Host memory (512MB max per active execution)
- CPU throttling (`0.5` CPU per execution container)
- Database connection pool limits

### Memory calculation
At strict peak isolation:  
`100 users × 512MB = 51,200MB (~51GB RAM)` minimum, excluding OS and backend/frontend overhead.

### Scaling strategies
- Horizontal API scaling behind load balancer
- Dedicated execution worker nodes
- Queue-based execution with backpressure
- Smarter scheduling (short-job prioritization)
- Optional warm-container pools (trade isolation strictness vs startup latency)

---

## 4) Platform Comparison

### Docker vs Virtual Machines
Docker advantages:
- Faster startup
- Higher density (lower overhead per execution)
- Simpler image-based deployment pipeline

VM advantages:
- Stronger isolation boundary (full kernel isolation)

Trade-off:
- Docker is preferred here for speed/cost and acceptable isolation with strict limits + ephemeral lifecycle.

### Docker vs Serverless (AWS Lambda / Azure Functions)
Docker advantages for this use case:
- Full control over runtime/toolchain for multi-language code execution
- Easier support for custom compilers/interpreters
- Predictable execution environment

Serverless advantages:
- Built-in autoscaling and managed operations

Trade-off:
- Serverless can reduce ops burden but may be harder for interactive, compiler-heavy, custom sandbox workloads.
- Docker provides greater flexibility for educational code-runner scenarios.
