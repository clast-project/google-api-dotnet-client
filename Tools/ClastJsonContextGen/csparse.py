"""Minimal C# scanner for the *generated* Google API client files.

These files are machine-generated and extremely regular, so a brace-tracking
line scanner is sufficient (and exact) here.
"""
import re, io

TYPE_DECL = re.compile(
    r'^\s*(?:public|internal|protected|private)\s+(?:static\s+|abstract\s+|sealed\s+|partial\s+|new\s+|virtual\s+|readonly\s+)*'
    r'(class|enum|struct|interface)\s+([A-Za-z_@][A-Za-z0-9_]*)\s*(?:<[^{]*?>)?\s*(?::\s*(.*?))?\s*$')
NAMESPACE = re.compile(r'^\s*namespace\s+([A-Za-z0-9_.]+)\s*$')
REQ_PARAM_ATTR = re.compile(
    r'^\s*\[Google\.Apis\.Util\.RequestParameterAttribute\("([^"]+)"(?:,\s*Google\.Apis\.Util\.RequestParameterType\.(\w+))?\)\]\s*$')
PROP_DECL = re.compile(
    r'^\s*public\s+(?:virtual\s+|override\s+|new\s+)*(.+?)\s+([A-Za-z_@][A-Za-z0-9_]*)\s*\{\s*get;\s*(?:private\s+|protected\s+|internal\s+)?set;\s*\}\s*$')
STRING_VALUE_ATTR = re.compile(r'^\s*\[Google\.Apis\.Util\.StringValueAttribute\("([^"]*)"\)\]\s*$')
ENUM_MEMBER = re.compile(r'^\s*([A-Za-z_@][A-Za-z0-9_]*)\s*=\s*-?\d+\s*,?\s*$')

VALUE_TYPES = {'bool', 'int', 'long', 'float', 'double', 'decimal', 'char', 'byte', 'sbyte',
               'short', 'ushort', 'uint', 'ulong'}


def strip_code(line):
    out, i, n = [], 0, len(line)
    while i < n:
        c = line[i]
        if c == '/' and i + 1 < n and line[i + 1] == '/':
            break
        if c == '@' and i + 1 < n and line[i + 1] == '"':
            i += 2
            while i < n:
                if line[i] == chr(34):
                    if i + 1 < n and line[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            out.append(' ')
            continue
        if c == '"':
            i += 1
            while i < n:
                if line[i] == chr(92):
                    i += 2
                    continue
                if line[i] == chr(34):
                    i += 1
                    break
                i += 1
            out.append(' ')
            continue
        if c == "'":
            i += 1
            while i < n:
                if line[i] == chr(92):
                    i += 2
                    continue
                if line[i] == "'":
                    i += 1
                    break
                i += 1
            out.append(' ')
            continue
        out.append(c)
        i += 1
    return ''.join(out)


class TypeInfo:
    def __init__(self, kind, name, path, ns, base):
        self.kind, self.name, self.path, self.ns, self.base = kind, name, path, ns, base or ''
        self.params = []        # (wire, kind, is_value_type, prop)
        self.enum_members = []  # (member, wire)

    @property
    def full(self):
        return self.ns + '.' + '.'.join(self.path)

    @property
    def nested(self):
        return '.'.join(self.path)


def parse(path):
    with io.open(path, encoding='utf-8-sig') as fh:
        src = fh.read()
    types = []
    stack = []          # frames: ('ns', name) | ('type', TypeInfo) | ('blk', None)
    pending = None      # ('ns', name) | ('type', kind, name, base)
    pending_param = None
    pending_sv = None

    for raw in src.split('\n'):
        line = raw.rstrip('\r')
        stripped = line.strip()

        m = REQ_PARAM_ATTR.match(line)
        if m:
            pending_param = (m.group(1), m.group(2) or 'Query')
            continue
        m = STRING_VALUE_ATTR.match(line)
        if m:
            pending_sv = m.group(1)
            continue

        cur = None
        for kind, obj in reversed(stack):
            if kind == 'type':
                cur = obj
                break

        code = strip_code(line)

        if cur is not None and cur.kind == 'enum' and pending_sv is not None:
            m = ENUM_MEMBER.match(code)
            if m:
                cur.enum_members.append((m.group(1), pending_sv))
                pending_sv = None

        if pending_param is not None:
            m = PROP_DECL.match(code)
            if m and cur is not None:
                ctype = m.group(1).strip()
                is_vt = ctype.startswith('System.Nullable<')
                cur.params.append((pending_param[0], pending_param[1], is_vt, m.group(2)))
                pending_param = None

        if stripped.startswith('['):
            continue

        if '{' not in code and '}' not in code:
            m = NAMESPACE.match(code)
            if m:
                pending = ('ns', m.group(1))
                continue
            m = TYPE_DECL.match(code)
            if m and not code.rstrip().endswith((';', ',', '(', ')', '=>')):
                pending = ('type', m.group(1), m.group(2), m.group(3) or '')
                continue
            if code.strip():
                # continuation of a base list (rare) — keep pending
                if pending is not None and pending[0] == 'type' and code.strip().startswith(':'):
                    pending = ('type', pending[1], pending[2], code.strip()[1:].strip())
                    continue
            continue

        for ch in code:
            if ch == '{':
                if pending is not None and pending[0] == 'ns':
                    stack.append(('ns', pending[1]))
                elif pending is not None and pending[0] == 'type':
                    _, kind, name, base = pending
                    parent = None
                    for k, o in reversed(stack):
                        if k == 'type':
                            parent = o
                            break
                    ns = '.'.join(n for k, n in stack if k == 'ns')
                    ti = TypeInfo(kind, name, (parent.path if parent else []) + [name], ns, base)
                    types.append(ti)
                    stack.append(('type', ti))
                else:
                    stack.append(('blk', None))
                pending = None
            elif ch == '}':
                if stack:
                    stack.pop()
        # a decl line that also opened its brace consumed `pending`; anything else clears it
        if '{' in code:
            pending = None
        else:
            # line had only '}' — check for a decl on the same line (doesn't happen here)
            pass
    return types
