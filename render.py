#!/usr/bin/env python3
"""Render container entrypoint for the VailAC ASP.NET Core service."""
import os
import shutil
import sys

def main() -> None:
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("dotnet runtime was not found in the Render container.")

    port = os.environ.get("PORT", "10000")
    os.environ.setdefault("ASPNETCORE_URLS", f"http://0.0.0.0:{port}")
    os.environ.setdefault("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")

    app = "/app/VailACServer.dll"
    if not os.path.isfile(app):
        raise SystemExit(f"Published application not found: {app}")

    os.execv(dotnet, [dotnet, app])

if __name__ == "__main__":
    main()
