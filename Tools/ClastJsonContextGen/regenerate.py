"""Regenerate every committed Clast *.JsonContext.cs from its generated client.

Usage (from the repo root):

    python Tools/ClastJsonContextGen/regenerate.py           # rewrite in place
    python Tools/ClastJsonContextGen/regenerate.py --check    # report drift, exit 1

Run this after merging upstream into a sync branch, before building. See
CLAST_SYNC.md for where it fits in the sync process.
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from gen_jsoncontext import generate  # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

# Every generated client that carries a committed JsonContext. The namespace is the
# directory name in all four cases, but keep it explicit rather than inferred.
CLIENTS = [
    ('Google.Apis.Storage.v1', 'Google.Apis.Storage.v1'),
    ('Google.Apis.Bigquery.v2', 'Google.Apis.Bigquery.v2'),
    ('Google.Apis.ManufacturerCenter.v1', 'Google.Apis.ManufacturerCenter.v1'),
    ('Google.Apis.Translate.v2', 'Google.Apis.Translate.v2'),
]


def paths(directory):
    root = os.path.join(REPO_ROOT, 'Src', 'Generated', directory)
    return (os.path.join(root, directory + '.cs'),
            os.path.join(root, directory + '.JsonContext.cs'))


def main(argv):
    check = '--check' in argv
    drifted = []
    for directory, namespace in CLIENTS:
        client_cs, context_cs = paths(directory)
        text = generate(client_cs, namespace, existing=context_cs)
        current = io.open(context_cs, encoding='utf-8-sig', newline='').read()
        if current.replace('\r\n', '\n') == text.replace('\r\n', '\n'):
            print('%-40s up to date' % directory)
            continue
        drifted.append(directory)
        if check:
            print('%-40s DRIFTED' % directory)
        else:
            io.open(context_cs, 'w', encoding='utf-8-sig', newline='\r\n').write(text)
            print('%-40s regenerated' % directory)

    if check and drifted:
        print('\n%d file(s) out of date; run without --check to regenerate.' % len(drifted))
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
