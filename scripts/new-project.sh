#!/usr/bin/env bash
# Renames this template to a new company and project name.
#
#   bash scripts/new-project.sh <NewCompany> <NewProject>
#   e.g. bash scripts/new-project.sh YourCompany YourProject
#
# Reads the CURRENT names (COMPANY_NAME, PROJECT_NAME) from Directory.Build.props
# and replaces their PascalCase and lowercase forms, whole tokens only, in file
# contents, file names and folder names, in a single pass (a new name that
# contains an old name is never replaced twice). Skips .git, bin, obj,
# TestResults and binary files; keeps line endings and BOMs byte for byte.
# Then generates a new UserSecretsId and regenerates docs/openapi.json.
#
# Refuses to run: with uncommitted git changes, with invalid names, or when a new
# name already occurs in the files. Does nothing when the names are unchanged.
# Requires: Python 3, .NET SDK (for the OpenAPI regeneration).
set -euo pipefail

cd "$(dirname "$0")/.."

if [ "$#" -ne 2 ]; then
  echo "usage: bash scripts/new-project.sh <NewCompany> <NewProject>" >&2
  exit 2
fi

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  if [ -n "$(git status --porcelain)" ]; then
    echo "new-project: the git working tree has uncommitted changes; commit or stash them first." >&2
    git status --short >&2
    exit 1
  fi
else
  echo "new-project: WARNING: not a git repository; changes cannot be reviewed or undone with git." >&2
fi

rc=0
python3 - "$1" "$2" <<'PY' || rc=$?
import os, re, sys, uuid

NEW_COMPANY, NEW_PROJECT = sys.argv[1], sys.argv[2]
SKIP_DIRS = {".git", "bin", "obj", "TestResults", ".vs"}
NAME = re.compile(r"[A-Z][A-Za-z0-9]*")

def fail(message):
    print("new-project: " + message, file=sys.stderr)
    sys.exit(1)

props = open("Directory.Build.props", encoding="utf-8").read()
def prop(name):
    match = re.search(r"<%s>([^<]+)</%s>" % (name, name), props)
    if not match:
        fail(name + " not found in Directory.Build.props")
    return match.group(1).strip()

old_company, old_project = prop("COMPANY_NAME"), prop("PROJECT_NAME")
for label, value in (("company", NEW_COMPANY), ("project", NEW_PROJECT)):
    if not NAME.fullmatch(value):
        fail(f"invalid {label} name '{value}': use PascalCase, letters and digits only (e.g. YourCompany, YourProject)")

pairs = [(o, n) for o, n in ((old_company, NEW_COMPANY), (old_project, NEW_PROJECT)) if o != n]
if not pairs:
    print(f"new-project: names are already {old_company}/{old_project}; nothing to do.")
    sys.exit(3)

mapping = {}
for old, new in pairs:
    mapping[old] = new
    mapping[old.lower()] = new.lower()

def token_pattern(words):
    alternation = "|".join(re.escape(w) for w in sorted(words, key=len, reverse=True))
    return re.compile(r"(?<![A-Za-z0-9])(%s)(?![A-Za-z0-9])" % alternation)

replace_pattern = token_pattern(mapping.keys())
new_names = [n for _, n in pairs]
new_pattern = re.compile(token_pattern(new_names).pattern, re.IGNORECASE)

def walk():
    for root, dirs, files in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            yield os.path.join(root, name)

def read_text(path):
    data = open(path, "rb").read()
    if b"\0" in data[:8000]:
        return None
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        return None

texts = {}
for path in walk():
    text = read_text(path)
    if text is not None:
        texts[path] = text

# Refuse when a new name already occurs (contents or paths), case-insensitively.
clashes = [p for p, t in texts.items() if new_pattern.search(t)]
clashes += [p for p in texts if new_pattern.search(p)]
if clashes:
    fail("the new name(s) " + "/".join(new_names) + " already occur in:\n  " + "\n  ".join(sorted(set(clashes))))

# 1. File contents (single pass).
changed = 0
for path, text in sorted(texts.items()):
    count = len(replace_pattern.findall(text))
    if count:
        new_text = replace_pattern.sub(lambda m: mapping[m.group(1)], text)
        with open(path, "wb") as f:
            f.write(new_text.encode("utf-8"))
        changed += 1
        print(f"  edited  {path} ({count})")

# 2. File and folder names, deepest first.
renamed = 0
for root, dirs, files in os.walk(".", topdown=False):
    if any(part in SKIP_DIRS for part in root.split(os.sep)):
        continue
    for name in files + [d for d in dirs if d not in SKIP_DIRS]:
        new_name = replace_pattern.sub(lambda m: mapping[m.group(1)], name)
        if new_name != name:
            os.rename(os.path.join(root, name), os.path.join(root, new_name))
            renamed += 1
            print(f"  renamed {os.path.join(root, name)} -> {new_name}")

# 3. New UserSecretsId.
api = os.path.join("src", "Api", "Api.csproj")
csproj = open(api, "rb").read().decode("utf-8")
secrets_id = str(uuid.uuid4())
csproj, n = re.subn(r"<UserSecretsId>[^<]*</UserSecretsId>", f"<UserSecretsId>{secrets_id}</UserSecretsId>", csproj)
if n != 1:
    fail("UserSecretsId not found in " + api)
open(api, "wb").write(csproj.encode("utf-8"))
print(f"  new UserSecretsId {secrets_id} in {api}")

old_names = "/".join(o for o, _ in pairs)
print(f"new-project: {old_names} -> {'/'.join(new_names)}: {changed} files edited, {renamed} paths renamed.")
PY
# 3 = names unchanged: nothing was modified, so nothing to regenerate.
[ "$rc" -eq 3 ] && exit 0
[ "$rc" -eq 0 ] || exit "$rc"

echo "new-project: regenerating docs/openapi.json"
bash scripts/generate-openapi.sh
echo "new-project: done. Review the changes, then run: bash scripts/verify.sh"
