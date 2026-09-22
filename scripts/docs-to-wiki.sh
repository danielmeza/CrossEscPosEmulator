#!/usr/bin/env bash
# Mirrors docs/ into a checked-out GitHub wiki. docs/ is the source of truth, so wiki pages that aren't
# in docs/ are removed. Links are rewritten for the wiki: page links lose ".md", README.md becomes Home,
# repo-relative paths (../src/...) point at GitHub, and sibling images load from raw.githubusercontent.
#
#   scripts/docs-to-wiki.sh docs path/to/Repo.wiki owner/repo
set -euo pipefail

docs=$1
wiki=$2
repo=$3
blob="https://github.com/$repo/blob/main"
raw="https://raw.githubusercontent.com/$repo/main"

find "$wiki" -maxdepth 1 -name '*.md' -delete
cp "$docs"/*.md "$wiki"/
mv "$wiki/README.md" "$wiki/Home.md"

for page in "$wiki"/*.md; do
  sed -i -E \
    -e 's#\]\(README\.md(\#[^)]*)?\)#](Home\1)#g' \
    -e 's#\]\(([A-Za-z0-9_-]+)\.md(\#[^)]*)?\)#](\1\2)#g' \
    -e "s#\]\(\.\./([^)]*)\)#]($blob/\1)#g" \
    -e "s#\]\(([^):/]+\.(png|jpg|gif|svg))\)#]($raw/docs/\1)#g" \
    "$page"
done
