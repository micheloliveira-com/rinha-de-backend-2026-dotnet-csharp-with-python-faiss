# -----------------------------
# Builder stage: install deps and prebuild
# -----------------------------
FROM python:3.11.15-slim-trixie AS builder

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    libopenblas-dev \
    libomp-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install
COPY requirements.freeze.txt .
RUN pip install --no-cache-dir -U pip && \
    pip install --no-cache-dir -r requirements.freeze.txt

# Copy app and prebuild
COPY . .
ENV OMP_NUM_THREADS=1 \
    MKL_NUM_THREADS=1 \
    OPENBLAS_NUM_THREADS=1 \
    NUMEXPR_NUM_THREADS=1 \
    PYTHONUNBUFFERED=1

# Prebuild step
RUN ONLY_REBUILD=1 python app.py

# -----------------------------
# Final image: runtime only
# -----------------------------
FROM python:3.11.15-slim-trixie

WORKDIR /app

# Copy only runtime dependencies from builder
COPY --from=builder /usr/local /usr/local
COPY --from=builder /app /app

# Environment variables
ENV OMP_NUM_THREADS=1 \
    MKL_NUM_THREADS=1 \
    OPENBLAS_NUM_THREADS=1 \
    NUMEXPR_NUM_THREADS=1 \
    PYTHONUNBUFFERED=1

# Expose API port
EXPOSE 5000

# Run service
CMD ["python", "app.py"]