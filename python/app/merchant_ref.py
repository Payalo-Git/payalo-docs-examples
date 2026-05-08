"""Generate fresh merchantReference values.

The gateway's deduplication store retains used IDs for ~15 minutes; never reuse one,
even after a failure.
"""

from uuid import uuid4


def new_merchant_ref(prefix: str) -> str:
    return f"{prefix}-{uuid4().hex}"
