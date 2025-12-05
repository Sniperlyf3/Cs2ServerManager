#!/bin/bash
set -e

# Variables
NEWUSER="cs2user"
DOTNET_VERSION="8.0"   # adjust to the version you need

apt install -y sudo

echo "[INFO] Updating package lists..."
sudo apt-get update

echo "[INFO] Installing prerequisites (wget, apt-transport-https)..."
sudo apt-get install -y wget apt-transport-https

echo "[INFO] Installing Git ..."
sudo apt-get install -y git

echo "[INFO] Installing lsb-release..."
sudo apt-get install -y lsb-release


echo "[INFO] Adding Microsoft package feed for Debian..."
wget https://packages.microsoft.com/config/debian/$(. /etc/os-release; echo $VERSION_ID)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

echo "[INFO] Updating package lists again..."
apt-get update

echo "[INFO] Installing .NET SDK $DOTNET_VERSION..."
apt-get install -y dotnet-sdk-$DOTNET_VERSION

echo "[INFO] Creating user $NEWUSER..."
if ! id "$NEWUSER" &>/dev/null; then
    adduser --disabled-password --gecos "" "$NEWUSER"
    usermod -aG sudo "$NEWUSER"
else
    echo "[INFO] User $NEWUSER already exists."
fi

echo "[INFO] Switching to $NEWUSER for final steps..."
sudo -u "$NEWUSER" bash <<'EOF'
echo "[INFO] Running as $(whoami)..."
cd ~
mkdir -p cs2-setup
cd cs2-setup
echo "[INFO] Environment ready for CS2 setup."
EOF