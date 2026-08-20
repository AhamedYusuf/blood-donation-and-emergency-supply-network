from fastapi import FastAPI

app = FastAPI(title="Blood Donation Network Agent Service")


@app.get("/health")
def health():
    return {"status": "ok"}
