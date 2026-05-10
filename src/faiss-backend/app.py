import os
import numpy as np
from aiohttp import web
import gzip
import ijson
import faiss

VECTOR_DIM = 14
TOP_K = 5
NLIST = 4096
NPROBE = 8
NUM_SHARDS = 2

DATA_FILE = "resources/references.json.gz"
INDEX_DIR = "resources/train"
LABELS_FILE = os.path.join(INDEX_DIR, "labels.npy")

SHARD_FILES = [
    os.path.join(INDEX_DIR, f"references_shard_{i}.faiss")
    for i in range(NUM_SHARDS)
]

os.makedirs(INDEX_DIR, exist_ok=True)

ONLY_REBUILD = os.getenv("ONLY_REBUILD", "0") == "1"

index = None
labels = None


# -----------------------------
# LOAD RAW DATA
# -----------------------------
def load_data(path):
    vectors = []
    label_list = []

    with gzip.open(path, "rb") as f:
        for item in ijson.items(f, "item"):
            vectors.append(item["vector"])

            lbl = item["label"]
            if isinstance(lbl, str):
                label_list.append(1 if lbl.lower() == "fraud" else 0)
            else:
                label_list.append(1 if int(lbl) == 1 else 0)

    X = np.ascontiguousarray(vectors, dtype=np.float32)
    y = np.asarray(label_list, dtype=np.int8)

    return X, y


# -----------------------------
# TRAIN GLOBAL TEMPLATE
# -----------------------------
def train_template(X):
    quantizer = faiss.IndexFlatL2(VECTOR_DIM)

    idx = faiss.IndexIVFScalarQuantizer(
        quantizer,
        VECTOR_DIM,
        NLIST,
        faiss.ScalarQuantizer.QT_fp16,
        faiss.METRIC_L2
    )

    idx.train(X)
    return idx


# -----------------------------
# BUILD SHARD FROM TEMPLATE
# -----------------------------
def build_shard_from_template(template, X):
    quantizer = faiss.clone_index(template.quantizer)

    idx = faiss.IndexIVFScalarQuantizer(
        quantizer,
        VECTOR_DIM,
        NLIST,
        faiss.ScalarQuantizer.QT_fp16,
        faiss.METRIC_L2
    )

    idx.is_trained = True
    idx.add(X)
    idx.nprobe = NPROBE

    return idx


# -----------------------------
# BUILD + SAVE
# -----------------------------
def train_and_save():
    global index, labels

    print("[FAISS] loading raw data...")
    X, y = load_data(DATA_FILE)

    print("[FAISS] training global quantizer on full dataset...")
    template = train_template(X)

    print(f"[FAISS] building {NUM_SHARDS} shards...")
    parts_X = np.array_split(X, NUM_SHARDS)

    shard_indexes = []

    for i in range(NUM_SHARDS):
        print(f"[FAISS] building shard {i}...")
        shard_idx = build_shard_from_template(template, parts_X[i])

        print(f"[FAISS] saving shard {i}...")
        faiss.write_index(shard_idx, SHARD_FILES[i])

        shard_indexes.append(shard_idx)

    print("[FAISS] saving labels...")
    np.save(LABELS_FILE, y)

    merged = faiss.IndexShards(VECTOR_DIM, False, True)

    for shard_idx in shard_indexes:
        shard_idx.nprobe = NPROBE
        merged.add_shard(shard_idx)

    index = merged
    labels = y

    del X

    print("[FAISS] ready:", labels.shape[0])


# -----------------------------
# LOAD SAVED
# -----------------------------
def load_saved():
    global index, labels

    print("[FAISS] loading saved shards...")

    merged = faiss.IndexShards(VECTOR_DIM, False, True)

    for i in range(NUM_SHARDS):
        shard_idx = faiss.read_index(SHARD_FILES[i])
        shard_idx.nprobe = NPROBE
        merged.add_shard(shard_idx)

    index = merged

    print("[FAISS] loading labels...")
    labels = np.load(LABELS_FILE)

    print("[FAISS] ready:", labels.shape[0])


# -----------------------------
# BOOTSTRAP
# -----------------------------
def bootstrap():
    if ONLY_REBUILD:
        print("[FAISS] ONLY_REBUILD enabled -> rebuilding index and exiting")
        train_and_save()
        return False

    if all(os.path.exists(f) for f in SHARD_FILES) and os.path.exists(LABELS_FILE):
        load_saved()
    else:
        train_and_save()

    return True


# -----------------------------
# SEARCH
# -----------------------------
def search(vec):
    _, I = index.search(vec, TOP_K)
    valid = I[0][I[0] >= 0]
    return int(labels.take(valid).sum())


# -----------------------------
# API
# -----------------------------
async def search_endpoint(request):
    msg = await request.json()

    vec = np.ascontiguousarray(msg["vector"], dtype=np.float32)[None, :]

    fraud_count = search(vec)

    return web.json_response({
        "fraud_count": fraud_count
    })


async def init_app():
    should_run = bootstrap()

    if not should_run:
        print("[FAISS] build complete -> exiting")
        os._exit(0)

    app = web.Application()
    app.router.add_post("/search", search_endpoint)

    return app


web.run_app(init_app(), host="0.0.0.0", port=5000)