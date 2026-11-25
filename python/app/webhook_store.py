"""In-memory log of received gateway callbacks. DEMO ONLY — wipes on restart.

Production integrations write each entry to a database keyed on the gateway-assigned
``gatewayReference`` and ``status`` so callbacks are durable, dedupable across replicas,
and reconcilable via /records. A payment can send a second callback with a different
status (an operator can reverse a final status), so the status is part of the key.
"""

from __future__ import annotations

import threading
from collections import OrderedDict
from datetime import datetime
from typing import Any

_MAX_ENTRIES = 200


class WebhookEntry:
    __slots__ = ("received_at", "source_ip", "payload", "raw_body")

    def __init__(self, received_at: datetime, source_ip: str, payload: dict[str, Any], raw_body: str) -> None:
        self.received_at = received_at
        self.source_ip = source_ip
        self.payload = payload
        self.raw_body = raw_body


class WebhookStore:
    def __init__(self) -> None:
        # OrderedDict for newest-first iteration. Lock guards the whole structure.
        self._by_reference: OrderedDict[str, WebhookEntry] = OrderedDict()
        self._lock = threading.Lock()

    def try_record(self, payload: dict[str, Any], raw_body: str, source_ip: str) -> bool:
        """Insert if absent. Returns True when newly stored, False on duplicate.

        Keyed on the gateway-assigned ``gatewayReference`` plus the ``status``, as the v2
        callbacks docs say: a repeat of the same status is a duplicate, a new status is not.
        """
        gateway_reference = payload.get("gatewayReference")
        if not gateway_reference or not isinstance(gateway_reference, str):
            return False
        key = f"{gateway_reference}|{payload.get('status')}"
        with self._lock:
            if key in self._by_reference:
                return False
            self._by_reference[key] = WebhookEntry(datetime.utcnow(), source_ip, payload, raw_body)
            self._by_reference.move_to_end(key, last=False)  # newest-first
            while len(self._by_reference) > _MAX_ENTRIES:
                self._by_reference.popitem(last=True)
        return True

    def snapshot(self) -> list[WebhookEntry]:
        with self._lock:
            return list(self._by_reference.values())

    def clear(self) -> None:
        with self._lock:
            self._by_reference.clear()


store = WebhookStore()
