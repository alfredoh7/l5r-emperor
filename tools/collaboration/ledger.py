"""Read-only C# contract inventory; candidates for review, never rules acceptance."""
from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass
import json
from pathlib import Path
import re
import subprocess


CORE = 'src/L5R.Core/'
LEXER = re.compile(r'//[^\n]*|/\*.*?\*/|\$?@"(?:""|[^"])*"|\$?""".*?"""|'
                   r'\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|'
                   r'[A-Za-z_][A-Za-z_0-9]*|==|!=|=>|\S', re.S)
IDENTIFIER = re.compile(r'^[A-Za-z_][A-Za-z_0-9]*$')
MODIFIERS = {'static', 'readonly', 'required', 'virtual', 'override', 'new', 'abstract', 'sealed'}


@dataclass(frozen=True)
class Token:
    text: str
    line: int


def tokens(source):
    result, line, previous = [], 1, 0
    for match in LEXER.finditer(source):
        line += source[previous:match.start()].count('\n')
        value = match.group()
        if not value.startswith(('//', '/*')):
            result.append(Token(value, line))
        line += value.count('\n')
        previous = match.end()
    return result


def pairs(ts):
    stack, result = [], {}
    for i, token in enumerate(ts):
        if token.text in ('(', '[', '{'):
            stack.append(i)
        elif token.text in (')', ']', '}'):
            if stack and ts[stack[-1]].text == {')': '(', ']': '[', '}': '{'}[token.text]:
                start = stack.pop()
                result[start] = i
    return result


def generic_open(ts, index):
    """Recognize type argument brackets, not comparison operators in payload expressions."""
    depth = 1
    for cursor in range(index + 1, len(ts)):
        value = ts[cursor].text
        if value == '<':
            depth += 1
        elif value == '>':
            depth -= 1
            if depth == 0:
                following = ts[cursor + 1].text if cursor + 1 < len(ts) else ''
                return following in ('(', '.', '[', '?', ',', ')') or IDENTIFIER.fullmatch(following)
        elif not IDENTIFIER.fullmatch(value) and value not in ('.', ',', '[', ']', '?'):
            return False
    return False


def split_args(ts, declaration=False):
    """Split declarations/calls without splitting generic types or nested defaults."""
    result, start, stack, default = [], 0, [], False
    for i, token in enumerate(ts):
        if token.text in ('=', ':') and not stack:
            default = True
        if token.text in ('(', '[', '{') or (token.text == '<' and ((declaration and not default) or generic_open(ts, i))):
            stack.append(token.text)
        elif stack and token.text == {'(': ')', '[': ']', '{': '}', '<': '>'}[stack[-1]]:
            stack.pop()
        elif token.text == ',' and not stack:
            result.append(ts[start:i])
            start, default = i + 1, False
    return result + [ts[start:]] if ts else []


def literal(ts):
    if len(ts) != 1:
        return None
    value = ts[0].text
    if value.startswith('@"'):
        return value[2:-1].replace('""', '"')
    if value.startswith('"""'):
        return value[3:-3]
    if value.startswith('"'):
        try:
            return json.loads(value)
        except ValueError:
            return None
    return None


def declarations(path, ts):
    """Inventory public properties/fields and positional record properties, including partials."""
    links = pairs(ts)
    types, fields, constants = set(), {}, {}
    for i, token in enumerate(ts):
        if token.text not in ('class', 'record', 'struct'):
            continue
        n = i + 1
        if n < len(ts) and ts[n].text in ('class', 'struct'):
            n += 1
        if n >= len(ts) or not IDENTIFIER.fullmatch(ts[n].text):
            continue
        owner = ts[n].text
        types.add(owner)
        cursor = n + 1

        def add(name, declaration, line):
            fields[owner + '.' + name] = {'path': path, 'line': line,
                'declaration': ' '.join(t.text for t in declaration), 'owner': owner}

        if token.text == 'record' and cursor < len(ts) and ts[cursor].text == '(':
            end = links.get(cursor)
            if end is None:
                continue
            for arg in split_args(ts[cursor + 1:end], declaration=True):
                arg = arg[:next((k for k, t in enumerate(arg) if t.text == '='), len(arg))]
                if arg and IDENTIFIER.fullmatch(arg[-1].text):
                    add(arg[-1].text, arg[:-1], arg[-1].line)
            cursor = end + 1
        while cursor < len(ts) and ts[cursor].text not in ('{', ';'):
            cursor += 1
        if cursor >= len(ts) or ts[cursor].text != '{' or cursor not in links:
            continue
        end, cursor = links[cursor], cursor + 1
        while cursor < end:
            if ts[cursor].text != 'public':
                if ts[cursor].text == '{' and cursor in links:
                    cursor = links[cursor] + 1
                else:
                    cursor += 1
                continue
            start, cursor = cursor + 1, cursor + 1
            while cursor < end and ts[cursor].text not in ('{', ';', '=', '=>', '('):
                cursor += 1
            header = ts[start:cursor]
            if cursor >= end or not header:
                continue
            if len(header) >= 3 and [t.text for t in header[:2]] == ['const', 'string']:
                stop = cursor + 1
                while stop < end and ts[stop].text != ';':
                    stop += 1
                for part in split_args(ts[start:stop], declaration=True):
                    equal = next((k for k, t in enumerate(part) if t.text == '='), None)
                    if equal is not None and equal > 0:
                        value = literal(part[equal + 1:])
                        if value is not None:
                            constants[owner + '.' + part[equal - 1].text] = value
                cursor = stop
            elif ts[cursor].text == '{' and cursor in links and 'static' not in [t.text for t in header]:
                accessors = ts[cursor + 1:links[cursor]]
                if any(t.text == 'get' for t in accessors) and IDENTIFIER.fullmatch(header[-1].text):
                    decl = [t for t in header[:-1] if t.text not in MODIFIERS]
                    add(header[-1].text, decl, header[-1].line)
            elif ts[cursor].text == '=>' and len(header) >= 2 and IDENTIFIER.fullmatch(header[-1].text) and 'static' not in [t.text for t in header]:
                add(header[-1].text, [t for t in header[:-1] if t.text not in MODIFIERS], header[-1].line)
            elif ts[cursor].text in (';', '=') and len(header) >= 2 and not any(t.text in ('static', 'const') for t in header):
                stop = cursor
                while stop < end and ts[stop].text != ';':
                    if ts[stop].text in ('(', '[', '{') and stop in links:
                        stop = links[stop]
                    stop += 1
                parts = split_args(ts[start:stop], declaration=True)
                parts = [part[:next((k for k, t in enumerate(part) if t.text == '='), len(part))] for part in parts]
                declaration = [t for t in parts[0][:-1] if t.text not in MODIFIERS]
                for part in parts:
                    if part and IDENTIFIER.fullmatch(part[-1].text):
                        add(part[-1].text, declaration, part[-1].line)
                cursor = stop
            if cursor >= end:
                break
            # Skip the accessor/method/initializer body, never inventory locals as fields.
            if ts[cursor].text in ('{', '(') and cursor in links:
                cursor = links[cursor] + 1
            while cursor < end and ts[cursor].text not in (';', 'public'):
                if ts[cursor].text == '{' and cursor in links:
                    cursor = links[cursor] + 1
                    break
                cursor += 1
            if cursor < end and ts[cursor].text == ';':
                cursor += 1
    return types, fields, constants


def inventory(sources):
    types, fields, constants, parsed = set(), {}, {}, {}
    for path, source in sorted(sources.items()):
        ts = tokens(source)
        parsed[path] = ts
        names, members, values = declarations(path, ts)
        types.update(names)
        fields.update(members)
        constants.update(values)
    def resolve(expr):
        value = literal(expr)
        if value is not None:
            return value
        key = ''.join(t.text for t in expr)
        if key in constants:
            return constants[key]
        return None

    graph = defaultdict(set)
    for field in fields.values():
        graph[field['owner']].update(set(re.findall(r'\w+', field['declaration'])) & types)

    def reachable(roots):
        found, pending = set(), list(roots)
        while pending:
            name = pending.pop()
            if name not in found:
                found.add(name)
                pending.extend(graph[name] - found)
        return {name: value for name, value in fields.items() if value['owner'] in found}

    view_roots = {'MatchView', 'CommandResult'} | {name for name in types if name.endswith(('View', 'Quote', 'Option'))}
    result = {'pending_choices': {}, 'commands': {}, 'decider_specs': {},
              'view_fields': reachable(view_roots),
              'state_fields': reachable({'MatchState'}),
              'command_fields': reachable({'GameCommand'}), 'payload_sites': {}}
    for path, ts in parsed.items():
        links = pairs(ts)
        for i, token in enumerate(ts):
            category, opening = None, None
            if token.text == 'Command' and i + 1 < len(ts) and ts[i + 1].text == '(':
                category, opening = 'commands', i + 1  # MatchQueries' GameCommand factory.
            elif token.text in ('GameCommand', 'PendingChoice') and i > 0 and ts[i - 1].text == 'new' and i + 1 < len(ts) and ts[i + 1].text == '(':
                category = 'commands' if token.text == 'GameCommand' else 'pending_choices'
                opening = i + 1
            elif token.text == 'new' and i + 1 < len(ts) and ts[i + 1].text == '(':
                context = [t.text for t in ts[max(0, i - 6):i]]
                if len(context) >= 2 and context[-2:] == ['PendingChoice', '=']:
                    category, opening = 'pending_choices', i + 1
                elif 'GameCommand' in context and context[-1:] == ['=']:
                    category, opening = 'commands', i + 1
            if opening in links and category:
                args = split_args(ts[opening + 1:links[opening]])
                first = next((a[2:] for a in args if len(a) > 2 and a[0].text == 'Type' and a[1].text == ':'), args[0] if args else [])
                value = resolve(first)
                if value is not None:
                    result[category].setdefault(value, {'path': path, 'line': token.line})
                if category == 'commands':
                    for arg in args:
                        if len(arg) > 2 and arg[0].text in ('Choice', 'TargetId', 'CardIds', 'SourceIds') and arg[1].text == ':':
                            expression = ' '.join(t.text for t in arg)
                            result['payload_sites'].setdefault(path + ' :: ' + expression, {'path': path, 'line': arg[0].line})
            # Existing commands can gain new choices without a new constructor shape.
            if token.text == 'command' and [t.text for t in ts[i + 1:i + 4]] == ['.', 'Choice', '==']:
                stop = i + 4
                while stop < len(ts) and (IDENTIFIER.fullmatch(ts[stop].text) or ts[stop].text == '.' or literal(ts[stop:stop + 1]) is not None):
                    stop += 1
                expression = 'Choice == ' + ' '.join(t.text for t in ts[i + 4:stop])
                result['payload_sites'].setdefault(path + ' :: ' + expression, {'path': path, 'line': token.line})
            # The authoritative dispatch switch may be the only declaration of a command.
            if token.text == 'switch' and i + 1 in links:
                end = links[i + 1]
                expression = ''.join(t.text for t in ts[i + 2:end])
                if expression.endswith('command.Type') and end + 1 in links:
                    cursor, stop = end + 2, links[end + 1]
                    while cursor < stop:
                        if ts[cursor].text == 'case':
                            tail = cursor + 1
                            while tail < stop and ts[tail].text != ':':
                                tail += 1
                            value = resolve(ts[cursor + 1:tail])
                            if value is not None:
                                result['commands'].setdefault(value, {'path': path, 'line': ts[cursor].line})
                        if ts[cursor].text == '{' and cursor in links:
                            cursor = links[cursor]
                        cursor += 1
        # Decider spec registries are constants, often passed through existing generic commands.
        for name, value in constants.items():
            owner = name.rsplit('.', 1)[0]
            if owner.endswith('Specs') and any(t.text == 'IsDeciderSpec' for t in ts):
                for i, t in enumerate(ts):
                    if t.text == owner:
                        result['decider_specs'].setdefault(value, {'path': path, 'line': t.line})
                        break
    return result


def git(root, *args, binary=False):
    process = subprocess.run(['git', *args], cwd=root, capture_output=True, timeout=60)
    if process.returncode:
        raise RuntimeError('git failed while reading ledger snapshots; verify local refs and repository')
    return process.stdout if binary else process.stdout.decode('utf-8').rstrip('\r\n')


def revision(root, ref):
    return git(root, 'rev-parse', '--verify', '--end-of-options', ref + '^{commit}')


def snapshot(root, sha):
    # Raw blobs ignore export-ignore/export-subst attributes; no checkout or code execution.
    listing = git(root, 'ls-tree', '-r', '-z', sha, '--', CORE, binary=True)
    objects = []
    for entry in listing.split(b'\0'):
        if not entry:
            continue
        metadata, path = entry.split(b'\t', 1)
        mode, kind, oid = metadata.split()
        if kind == b'blob' and mode in (b'100644', b'100755') and path.endswith(b'.cs'):
            objects.append((oid, path.decode('utf-8')))
    if not objects:
        return {}
    process = subprocess.run(['git', 'cat-file', '--batch'], cwd=root,
        input=b''.join(oid + b'\n' for oid, _ in objects), capture_output=True, timeout=60)
    if process.returncode:
        raise RuntimeError('git failed reading ledger source blobs')
    result, cursor = {}, 0
    for oid, path in objects:
        end = process.stdout.index(b'\n', cursor)
        returned, kind, size = process.stdout[cursor:end].split()
        if returned != oid or kind != b'blob':
            raise RuntimeError('Incomplete ledger source blob read')
        cursor = end + 1
        result[path] = process.stdout[cursor:cursor + int(size)].decode('utf-8')
        cursor += int(size) + 1
    return result


def ledger(root: Path, base, head='HEAD', working=False):
    base_sha, head_sha = revision(root, base), revision(root, head)
    ancestor = git(root, 'merge-base', base_sha, head_sha)
    before, after = snapshot(root, ancestor), snapshot(root, head_sha)
    if working:
        if head_sha != revision(root, 'HEAD'):
            raise ValueError('--working requires --head to resolve to HEAD')
        paths = git(root, 'ls-files', '--cached', '--others', '--exclude-standard', '-z', '--', CORE).split('\0')
        after = {}
        for path in paths:
            if path.endswith('.cs'):
                target = root / path
                if not target.resolve().is_relative_to(root.resolve()):
                    raise ValueError('Ledger source path leaves repository')
                if target.is_file():
                    after[path] = target.read_text(encoding='utf-8')
    old, new = inventory(before), inventory(after)
    categories = {}
    for category in old:
        changes = []
        for name in sorted(old[category].keys() | new[category].keys()):
            a, b = old[category].get(name), new[category].get(name)
            kind = 'added' if a is None else 'removed' if b is None else 'changed'
            if a is not None and b is not None and a.get('declaration') == b.get('declaration'):
                continue  # Moving declarations across partial files is not a new contract.
            entry = {'change': kind, 'name': name, **(b or a)}
            entry.pop('owner', None)
            if a and b:
                entry['before'] = a['declaration']
            changes.append(entry)
        categories[category] = changes
    return {'base': base_sha, 'merge_base': ancestor, 'head': head_sha, 'working': working,
            'categories': categories,
            'limitations': ['Heuristic C# inventory, not a compiler or rules/visibility verdict.',
                'Literal/constant constructors and named payload sites only; dynamic dispatch, value semantics, events and UI behavior require the four reviewers.',
                'Fields are public properties/fields reachable from MatchState, view/quote/option types and GameCommand; inheritance, custom serializers and conditional compilation require review.']}


def markdown(report):
    lines = ['# Client-contract ledger', '',
             f"Compared `{report['merge_base']}` to `{report['head']}`" + (' plus working files.' if report['working'] else '.'),
             '', 'Candidates for review; an empty ledger does not prove no client-lane impact.']
    for category, entries in report['categories'].items():
        lines += ['', '## ' + category.replace('_', ' ').capitalize(), '']
        if not entries:
            lines.append('None detected.')
        for entry in entries:
            name = entry['name'].replace('`', '\\`').replace('\n', ' ')
            lines.append(f"- {entry['change']}: `{name}` — `{entry['path']}:{entry['line']}`" +
                         (f" ({entry['declaration']})" if 'declaration' in entry else ''))
    lines += ['', '## Limits', '', *['- ' + text for text in report['limitations']], '']
    return '\n'.join(lines)
