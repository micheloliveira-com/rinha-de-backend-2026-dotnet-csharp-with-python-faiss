import os
import numpy as np
from aiohttp import web
import gzip
import ijson
import faiss

faiss.omp_set_num_threads(2)

VECTOR_DIM = 14
TOP_K = 5
NLIST = 4096
NPROBE = 16

DATA_FILE = "resources/references.json.gz"
INDEX_FILE = "resources/train/references.faiss"
LABELS_FILE = "resources/train/labels.npy"

os.makedirs(os.path.dirname(INDEX_FILE), exist_ok=True)

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
# BUILD + SAVE
# -----------------------------
def train_and_save():
    global index, labels

    print("[FAISS] loading raw data...")
    X, y = load_data(DATA_FILE)

    print("[FAISS] creating index...")
    quantizer = faiss.IndexFlatL2(VECTOR_DIM)

    idx = faiss.IndexIVFScalarQuantizer(
        quantizer,
        VECTOR_DIM,
        NLIST,
        faiss.ScalarQuantizer.QT_fp16,
        faiss.METRIC_L2
    )

    idx.nprobe = NPROBE

    print("[FAISS] training...")
    idx.train(X)

    print("[FAISS] adding...")
    idx.add(X)

    print("[FAISS] saving index...")
    faiss.write_index(idx, INDEX_FILE)

    print("[FAISS] saving labels...")
    np.save(LABELS_FILE, y)

    index = idx
    labels = y

    del X

    print("[FAISS] ready:", index.ntotal)


# -----------------------------
# LOAD SAVED
# -----------------------------
def load_saved():
    global index, labels

    print("[FAISS] loading saved index...")
    index = faiss.read_index(INDEX_FILE)
    index.nprobe = NPROBE

    print("[FAISS] loading labels...")
    labels = np.load(LABELS_FILE)

    print("[FAISS] ready:", index.ntotal)


# -----------------------------
# BOOTSTRAP
# -----------------------------
def bootstrap():
    if ONLY_REBUILD:
        print("[FAISS] ONLY_REBUILD enabled -> rebuilding index and exiting")
        train_and_save()
        return False

    if os.path.exists(INDEX_FILE) and os.path.exists(LABELS_FILE):
        load_saved()
    else:
        train_and_save()

    return True


# -----------------------------
# SEARCH
# -----------------------------
def search(vec):
    _, I = index.search(vec, TOP_K)
    return int(labels[I[0]].sum())


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