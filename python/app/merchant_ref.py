"""Generate fresh merchantReference values.

The gateway keeps a registered merchantReference used for good, so a new payment needs a
new one. This sample always generates a fresh value, which is also safe after a rejected
request.
"""

from uuid import uuid4


def new_merchant_ref(prefix: str) -> str:
    return f"{prefix}-{uuid4().hex}"
