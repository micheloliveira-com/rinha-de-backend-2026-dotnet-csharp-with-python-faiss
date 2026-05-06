#!/bin/bash
rm -rf .venv
python3 -m venv .venv
source .venv/bin/activate

# Determine which requirements file to use
if [ -f "requirements.freeze.txt" ]; then
    echo "[INFO] Installing from requirements.freeze.txt"
    pip install -r requirements.freeze.txt
else
    echo "[INFO] requirements.freeze.txt not found, using requirements.original.txt"
    if [ ! -f "requirements.original.txt" ]; then
        echo "[ERROR] requirements.original.txt not found!"
        exit 1
    fi

    pip install -r requirements.original.txt

    # Generate frozen requirements for reproducibility
    pip freeze > requirements.freeze.txt
    echo "[INFO] Generated requirements.freeze.txt"
fi

echo "[INFO] Virtual environment setup complete."