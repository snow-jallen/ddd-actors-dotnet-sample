#!/usr/bin/env bash
# Build and test every chNN-sMM tag in book order, each from a clean checkout in a
# temporary git worktree. Your own working tree is never touched.
#
#   tools/verify-tags.sh             # every section tag
#   tools/verify-tags.sh 'ch07-*'    # only the tags that match a git tag pattern
#
# At each tag it runs, from the root of the checkout:
#   dotnet build Ohmline.slnx -warnaserror
#   dotnet test --solution Ohmline.slnx
# and then fails the tag if the build or the tests left untracked or modified files.
# Tags whose Ohmline.slnx is missing or lists no projects yet are reported as skipped: these are
# the Chapter 1 tags (ch01-s02 to ch01-s07), which hold documents and setup only and are exempt
# from the build-and-test check. Every tag from ch02-s01 on has projects and must pass.
#
# Tests that start containers need Podman or Docker. With rootless Podman, first run:
#   export DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock
set -euo pipefail

pattern="${1:-ch[0-9][0-9]-s[0-9][0-9]}"
repo="$(git rev-parse --show-toplevel)"
work="$(mktemp -d "${TMPDIR:-/tmp}/ohmline-verify.XXXXXX")"
tree="$work/tree"
logs="$work/logs"
keep_logs=false

cleanup() {
  git -C "$repo" worktree remove --force "$tree" >/dev/null 2>&1 || true
  if [[ "$keep_logs" == true ]]; then
    rm -rf "$tree"
  else
    rm -rf "$work"
  fi
}
trap cleanup EXIT

mapfile -t tags < <(git -C "$repo" tag --list "$pattern" | LC_ALL=C sort)
if (( ${#tags[@]} == 0 )); then
  echo "No tags match '$pattern'." >&2
  exit 1
fi

mkdir -p "$logs"
git -C "$repo" worktree add --quiet --detach "$tree" "${tags[0]}"

has_projects() {
  [[ -f "$tree/Ohmline.slnx" ]] && grep -q '<Project ' "$tree/Ohmline.slnx"
}

verify() {
  (
    cd "$tree"
    dotnet build Ohmline.slnx -warnaserror
    dotnet test --solution Ohmline.slnx
  ) || return 1

  local leftovers
  leftovers="$(git -C "$tree" status --porcelain)"
  if [[ -n "$leftovers" ]]; then
    printf 'The build or the tests left files behind:\n%s\n' "$leftovers"
    return 1
  fi
}

passed=0
skipped=0
failed=()

for tag in "${tags[@]}"; do
  git -C "$tree" checkout --quiet --detach "$tag"
  git -C "$tree" clean -ffdxq

  if ! has_projects; then
    printf '%-10s skipped (Ohmline.slnx has no projects yet)\n' "$tag"
    skipped=$((skipped + 1))
    continue
  fi

  started=$SECONDS
  if verify >"$logs/$tag.log" 2>&1; then
    printf '%-10s passed  (%ds)\n' "$tag" $((SECONDS - started))
    passed=$((passed + 1))
  else
    printf '%-10s FAILED  (log: %s)\n' "$tag" "$logs/$tag.log"
    failed+=("$tag")
    keep_logs=true
  fi
done

printf '\n%d passed, %d skipped, %d failed\n' "$passed" "$skipped" "${#failed[@]}"
if (( ${#failed[@]} > 0 )); then
  printf 'Failed: %s\n' "${failed[*]}"
  exit 1
fi
