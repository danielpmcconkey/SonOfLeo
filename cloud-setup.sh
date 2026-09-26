#!/usr/bin/env bash
# Setup script for Claude Code cloud environments.
# Paste into the environment's "Setup script" setting (cloud environment menu -> Edit).
#
# Installs the .NET 10 SDK from Ubuntu's archive. Microsoft's download CDN
# (builds.dotnet.microsoft.com) is blocked by the environment's network policy,
# so dotnet-install.sh won't work here; Ubuntu 24.04 ships dotnet-sdk-10.0.
set -euo pipefail

if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

dotnet --version
