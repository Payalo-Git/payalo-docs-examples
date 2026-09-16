"""Strongly-typed configuration loaded from PAYALO_* environment variables.

Mirrors the .NET sample's PayaloOptions and the Node sample's `config.ts`.
"""

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_prefix="PAYALO_",
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    base_url: str = "https://api.payalo.com/gateway/mmo/v2/"
    api_key: str = ""
    webhook_path: str = "/webhooks/payalo"
    public_base_url: str = ""
    port: int = 8000

    @property
    def result_url(self) -> str:
        """Absolute resultUrl pre-fill for forms — public base + webhook path."""
        base = self.public_base_url.rstrip("/") if self.public_base_url else "https://example.com"
        return f"{base}{self.webhook_path}"


settings = Settings()
