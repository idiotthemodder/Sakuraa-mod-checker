#!/bin/bash
# builds the mod, deploys it into Gorilla Tag, and pushes the source to github
set -e
cd "$(dirname "$0")"

echo "== building =="
dotnet build

echo "== deploying =="
cp "bin/Debug/netstandard2.1/Sakuraa Client.dll" ~/Videos/plugins/QOL/
echo "copied to ~/Videos/plugins/QOL/"

echo "== syncing to github =="
git add -A
if git diff --cached --quiet; then
  echo "no source changes, nothing to push"
else
  git commit -m "auto sync $(date +'%Y-%m-%d %H:%M:%S')" -q
  git push -q
  echo "pushed to github"
fi
