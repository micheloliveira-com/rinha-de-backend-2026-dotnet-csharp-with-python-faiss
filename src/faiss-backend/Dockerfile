FROM python:3.11.15-slim-trixie

# -----------------------------
# System dependencies
# -----------------------------
RUN apt-get update && apt-get install -y \
    build-essential \
    libopenblas-dev \
    libomp-dev \
    && rm -rf /var/lib/apt/lists/*

# -----------------------------
# Environment tuning
# -----------------------------
ENV OMP_NUM_THREADS=1
ENV MKL_NUM_THREADS=1
ENV OPENBLAS_NUM_THREADS=1
ENV NUMEXPR_NUM_THREADS=1
ENV PYTHONUNBUFFERED=1

# -----------------------------
# Workdir
# -----------------------------
WORKDIR /app

# -----------------------------
# Install Python deps
# -----------------------------
COPY requirements.txt .

RUN pip install --no-cache-dir -U pip && \
    pip install --no-cache-dir -r requirements.txt

# -----------------------------
# Copy app
# -----------------------------
COPY . .

# -----------------------------
# FORCE REBUILD AT BUILD TIME
# -----------------------------
RUN ONLY_REBUILD=1 python app.py

# -----------------------------
# Expose API port
# -----------------------------
EXPOSE 5000

# -----------------------------
# Run service (no rebuild)
# -----------------------------
CMD ["python", "app.py"]