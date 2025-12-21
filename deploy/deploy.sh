#!/usr/bin/env bash
set -e

# SpeechToText.Service deployment script
# Usage: ./deploy/deploy.sh [target-path]
# Default target: /opt/olbrasoft/speech-to-text

TARGET_PATH="${1:-/opt/olbrasoft/speech-to-text}"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVICE_PROJECT="$PROJECT_ROOT/src/SpeechToText.Service"

echo "=== SpeechToText.Service Deployment ==="
echo "Target: $TARGET_PATH"
echo "Project: $SERVICE_PROJECT"
echo

# Build and publish
echo "[1/4] Building release..."
dotnet publish "$SERVICE_PROJECT/SpeechToText.Service.csproj" \
    -c Release \
    -o "$TARGET_PATH/app" \
    --no-self-contained

# Create directory structure
echo "[2/4] Creating directory structure..."
mkdir -p "$TARGET_PATH"/{config,logs}

# Copy configuration (if doesn't exist)
if [ ! -f "$TARGET_PATH/config/appsettings.json" ]; then
    echo "[3/4] Creating default config..."
    cp "$SERVICE_PROJECT/appsettings.json" "$TARGET_PATH/config/appsettings.json"
    echo "  → Config created at $TARGET_PATH/config/appsettings.json"
    echo "  → Edit this file to configure model path and options"
else
    echo "[3/4] Config already exists, skipping..."
fi

# Set permissions
echo "[4/4] Setting permissions..."
chmod +x "$TARGET_PATH/app/SpeechToText.Service"

echo
echo "=== Deployment Complete ==="
echo "  Binaries:   $TARGET_PATH/app/"
echo "  Config:     $TARGET_PATH/config/appsettings.json"
echo "  Logs:       $TARGET_PATH/logs/"
echo
echo "Next steps:"
echo "  1. Configure: $TARGET_PATH/config/appsettings.json"
echo "  2. Install systemd service: sudo cp deploy/speech-to-text.service /etc/systemd/system/"
echo "  3. Enable: sudo systemctl enable speech-to-text.service"
echo "  4. Start: sudo systemctl start speech-to-text.service"
echo "  5. Logs: journalctl -u speech-to-text.service -f"
