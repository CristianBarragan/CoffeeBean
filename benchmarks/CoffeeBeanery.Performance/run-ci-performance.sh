#!/usr/bin/env bash

set -Eeuo pipefail

# ============================================================
# CoffeeBeanery / Foundgine CI Performance Benchmark
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

COMPOSE_FILE="${SCRIPT_DIR}/docker-compose.benchmark.yml"
PROJECT_NAME="coffeebeaneryperformance"

DB_CONNECTION="Host=localhost;Port=55432;Database=foundgine_benchmark;Username=benchmark;Password=benchmark"

CUSTOMER_COUNT="${BENCHMARK_CUSTOMERS:-1}"
WARMUP_SECONDS="${BENCHMARK_WARMUP_SECONDS:-3}"
DURATION_SECONDS="${BENCHMARK_DURATION_SECONDS:-10}"
CONCURRENCY="${BENCHMARK_CONCURRENCY:-1,8,16,32,64}"

REPORT_ROOT="${SCRIPT_DIR}/reports"
REPORT_DIRECTORY="${REPORT_ROOT}/mutation"

# ============================================================
# Helpers
# ============================================================

cleanup() {
    echo "== Performance cleanup =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        ps || true

    echo "== Docker container status =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        ps || true

    echo "== Docker logs: PostgreSQL =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        logs postgres || true

    echo "== Docker logs: database (migration/seed) =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        logs database || true

    echo "== Docker logs: hotchocolate =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        logs hotchocolate || true

    echo "== Docker logs: foundgine-cold =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        logs foundgine-cold || true

    echo "== Docker logs: foundgine-warm =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        logs foundgine-warm || true

    echo "== Docker cleanup =="

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        down -v --remove-orphans || true

    echo "== Performance environment destroyed =="
}

trap cleanup EXIT

# ============================================================
# Header
# ============================================================

echo "=============================================="
echo " CoffeeBeanery / Foundgine CI Performance"
echo "=============================================="
echo "Repository:  ${REPO_ROOT}"
echo "Compose:     ${COMPOSE_FILE}"
echo "Database:    ${DB_CONNECTION/Password=benchmark/Password=***}"
echo "Customer:    ${CUSTOMER_COUNT}"
echo "Warm-up:     ${WARMUP_SECONDS}s"
echo "Measurement: ${DURATION_SECONDS}s"
echo "Concurrency: ${CONCURRENCY}"
echo "=============================================="

# ============================================================
# Validate benchmark files
# ============================================================

echo "== Validate benchmark files =="

test -f "${COMPOSE_FILE}"

test -f "${SCRIPT_DIR}/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj"
test -f "${SCRIPT_DIR}/CoffeeBeanery.LoadTest/CoffeeBeanery.LoadTest.csproj"
test -f "${SCRIPT_DIR}/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj"
test -f "${SCRIPT_DIR}/Foundgine.CoffeeBeanery.BenchmarkApi/Foundgine.CoffeeBeanery.BenchmarkApi.csproj"

echo "Benchmark files verified."

# ============================================================
# Prepare reports directory
#
# IMPORTANT:
# The load-test container writes to /reports.
# /reports is a bind mount of this host directory.
#
# GitHub Actions runs the container as a non-root user, so
# the host directory must already be writable.
# ============================================================

echo "== Prepare benchmark reports directory =="

mkdir -p "${REPORT_ROOT}"
mkdir -p "${REPORT_DIRECTORY}"

chmod -R 777 "${REPORT_ROOT}"

echo "Reports directory:"
ls -ld "${REPORT_ROOT}"
ls -ld "${REPORT_DIRECTORY}"

# ============================================================
# Restore performance projects
# ============================================================

echo "== Restore performance projects =="

dotnet restore \
    "${SCRIPT_DIR}/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj"

dotnet restore \
    "${SCRIPT_DIR}/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj"

dotnet restore \
    "${SCRIPT_DIR}/Foundgine.CoffeeBeanery.BenchmarkApi/Foundgine.CoffeeBeanery.BenchmarkApi.csproj"

dotnet restore \
    "${SCRIPT_DIR}/CoffeeBeanery.LoadTest/CoffeeBeanery.LoadTest.csproj"

# ============================================================
# Start fresh PostgreSQL fixture
# ============================================================

echo "== Start fresh PostgreSQL fixture =="

docker compose \
    -p "${PROJECT_NAME}" \
    -f "${COMPOSE_FILE}" \
    down -v --remove-orphans || true

docker compose \
    -p "${PROJECT_NAME}" \
    -f "${COMPOSE_FILE}" \
    up -d postgres

# ============================================================
# Wait for PostgreSQL
# ============================================================

echo "== Verify PostgreSQL =="

POSTGRES_READY=false

for i in $(seq 1 60); do

    if docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        exec -T postgres \
        pg_isready \
        -U benchmark \
        -d foundgine_benchmark \
        >/dev/null 2>&1
    then
        POSTGRES_READY=true
        break
    fi

    sleep 1
done

if [[ "${POSTGRES_READY}" != "true" ]]; then
    echo "PostgreSQL did not become ready."
    exit 1
fi

echo "PostgreSQL is ready."

# ============================================================
# Install dotnet-ef
# ============================================================

echo "== Install dotnet-ef =="

if ! command -v dotnet-ef >/dev/null 2>&1; then
    dotnet tool install \
        --global dotnet-ef \
        --version 9.0.7
else
    echo "dotnet-ef already installed."
fi

export PATH="${PATH}:${HOME}/.dotnet/tools"

dotnet ef --version

# ============================================================
# Apply CoffeeBeanery EF Core schema
# ============================================================

echo "== Apply CoffeeBeanery EF Core schema =="

dotnet ef database update \
    --project "${SCRIPT_DIR}/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj" \
    --startup-project "${SCRIPT_DIR}/CoffeeBeanery.Database/CoffeeBeanery.Database.csproj" \
    --context BankingEntityContext \
    --connection "${DB_CONNECTION}"

echo "EF Core schema applied."

# ============================================================
# Seed CoffeeBeanery fixture
#
# The database project is responsible for its normal fixture
# initialization when invoked by the benchmark environment.
# ============================================================

echo "== Verify database fixture =="

PG_TABLE_COUNT="$(
    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        exec -T postgres \
        psql \
        -U benchmark \
        -d foundgine_benchmark \
        -tAc \
        "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';"
)"

echo "Public table count: ${PG_TABLE_COUNT}"

if [[ "${PG_TABLE_COUNT}" -eq 0 ]]; then
    echo "ERROR: EF Core did not create any public tables."
    exit 1
fi

# ============================================================
# Start benchmark services
# ============================================================

echo "== Start benchmark services =="

# Do NOT let this command's exit code decide readiness. Docker Compose can
# abort `up -d` early ("dependency failed to start: ... is unhealthy") the
# moment a container's healthcheck retry budget is exhausted, even though the
# app itself is still starting - loading Foundgine's metadata/provider-plan
# cache can legitimately take longer than that. The real readiness gate is
# the curl polling loop below, which has its own generous timeout per
# target; a container that's merely slow, not actually broken, must not
# fail the whole run here.
docker compose \
    -p "${PROJECT_NAME}" \
    -f "${COMPOSE_FILE}" \
    up -d \
    hotchocolate \
    foundgine-cold \
    foundgine-warm \
    || echo "up -d reported a non-zero exit (likely a slow-starting healthcheck); continuing to the readiness poll below instead of failing here."

# ============================================================
# Wait for benchmark services
# ============================================================

echo "== Wait for benchmark services =="

docker compose \
    -p "${PROJECT_NAME}" \
    -f "${COMPOSE_FILE}" \
    ps

echo "Waiting for Hot Chocolate..."

for i in $(seq 1 150); do

    if curl \
        --fail \
        --silent \
        --show-error \
        "http://localhost:4500/health" \
        >/dev/null 2>&1
    then
        break
    fi

    if [[ "${i}" -eq 150 ]]; then
        echo "Hot Chocolate did not become ready."
        exit 1
    fi

    sleep 2
done

echo "Hot Chocolate is ready."

echo "Waiting for Foundgine cold..."

for i in $(seq 1 150); do

    if curl \
        --fail \
        --silent \
        --show-error \
        "http://localhost:4501/health" \
        >/dev/null 2>&1
    then
        break
    fi

    if [[ "${i}" -eq 150 ]]; then
        echo "Foundgine cold did not become ready."
        exit 1
    fi

    sleep 2
done

echo "Foundgine cold is ready."

echo "Waiting for Foundgine warm..."

for i in $(seq 1 150); do

    if curl \
        --fail \
        --silent \
        --show-error \
        "http://localhost:4502/health" \
        >/dev/null 2>&1
    then
        break
    fi

    if [[ "${i}" -eq 150 ]]; then
        echo "Foundgine warm did not become ready."
        exit 1
    fi

    sleep 2
done

echo "Foundgine warm is ready."

# ============================================================
# Verify reports mount
#
# This catches the permissions problem before the actual
# benchmark starts.
# ============================================================

echo "== Verify benchmark reports mount =="

docker compose \
    -p "${PROJECT_NAME}" \
    -f "${COMPOSE_FILE}" \
    run \
    --rm \
    --no-deps \
    --entrypoint sh \
    loader \
    -c 'touch /reports/.write-test && rm /reports/.write-test && echo "Reports mount is writable."'

# ============================================================
# Run CoffeeBeanery performance load test
#
# CoffeeBeanery.LoadTest is a single-target runner: it requires
# BENCHMARK_TARGET_NAME and BENCHMARK_TARGET_URL and benchmarks
# exactly one API per process (see Program.cs). Run it once per
# target, in its own network namespace ("run"), reusing the
# already-running hotchocolate / foundgine-cold / foundgine-warm
# containers as dependencies.
# ============================================================

echo "== Run CoffeeBeanery performance load test =="

declare -a TARGET_NAMES=(
    "Hot Chocolate + EF Core"
    "Foundgine - no cache"
    "Foundgine - provider-plan cache"
)

declare -a TARGET_URLS=(
    "http://hotchocolate:4300/graphql"
    "http://foundgine-cold:4301/graphql/cold"
    "http://foundgine-warm:4302/graphql/warm"
)

for i in "${!TARGET_NAMES[@]}"; do

    echo "-- Benchmarking: ${TARGET_NAMES[$i]} --"

    docker compose \
        -p "${PROJECT_NAME}" \
        -f "${COMPOSE_FILE}" \
        run \
        --rm \
        -e "BENCHMARK_TARGET_NAME=${TARGET_NAMES[$i]}" \
        -e "BENCHMARK_TARGET_URL=${TARGET_URLS[$i]}" \
        loader

done

# ============================================================
# Verify reports
# ============================================================

echo "== Verify benchmark reports =="

if [[ ! -d "${REPORT_DIRECTORY}" ]]; then
    echo "ERROR: Benchmark report directory does not exist:"
    echo "       ${REPORT_DIRECTORY}"
    exit 1
fi

echo "Generated reports:"

find "${REPORT_DIRECTORY}" \
    -maxdepth 2 \
    -type f \
    -print \
    | sort || true

REPORT_COUNT="$(
    find "${REPORT_DIRECTORY}" \
        -type f \
        | wc -l
)"

echo "Report file count: ${REPORT_COUNT}"

if [[ "${REPORT_COUNT}" -eq 0 ]]; then
    echo "ERROR: Benchmark completed but generated no report files."
    exit 1
fi

echo "=============================================="
echo " CoffeeBeanery / Foundgine CI Performance"
echo " SUCCESS"
echo "=============================================="