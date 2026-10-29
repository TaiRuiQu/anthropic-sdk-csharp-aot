"""Update AOT metadata from generated C# source without building the SDK.

Keeps assembly-generator registrations (including diagnostic-only shapes), and adds
model/enum and closed collection shapes present in the generated model sources.
Run from any directory: python scripts/update-aot-json-context.py [--check]
"""
from pathlib import Path
import hashlib
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
CONTEXT = ROOT / 'src/Anthropic/Core/AnthropicJsonSerializerContext.g.cs'
SOURCES = sorted((ROOT / 'src/Anthropic/Models').rglob('*.cs'))
BCL = dict(zip(
    'string bool byte int long float double decimal object'.split(),
    'String Boolean Byte Int32 Int64 Single Double Decimal Object'.split()))
BCL.update({x: x for x in ['String', 'Boolean', 'Byte', 'Int32', 'Int64', 'Single', 'Double', 'Decimal', 'Object', 'DateTime', 'DateTimeOffset', 'TimeSpan', 'Guid']})
CONTAINERS = {
    'ApiEnum': 'Anthropic.Core.ApiEnum',
    'ImmutableArray': 'System.Collections.Immutable.ImmutableArray',
    'FrozenDictionary': 'System.Collections.Frozen.FrozenDictionary',
    **{x: 'System.Collections.Generic.' + x for x in ['List', 'Dictionary', 'IReadOnlyList', 'IReadOnlyDictionary', 'IEnumerable']},
}
SYMBOLS = {}
FILES = []
for path in sorted((ROOT / 'src/Anthropic').rglob('*.cs')):
    if path.name == 'AnthropicJsonSerializerContext.g.cs':
        continue
    source = path.read_text(encoding='utf-8-sig')
    ns_match = re.search(r'^namespace ([\w.]+);', source, re.M)
    if not ns_match:
        continue
    ns = ns_match.group(1)
    for match in re.finditer(r'^public (?:sealed |abstract |readonly |partial |record )*(class|struct|enum) (\w+)([^\n]*)', source, re.M):
        kind, name, tail = match.groups()
        SYMBOLS[ns + '.' + name] = (kind, bool(re.search(r': (?:JsonModel|ModelBase)\b', tail)))
    aliases = dict(re.findall(r'^using (\w+) = ([\w.]+);', source, re.M))
    usings = re.findall(r'^using ([\w.]+);', source, re.M)
    if path in SOURCES:
        FILES.append((path, source, ns, aliases, usings))


def resolve(name, ns, aliases, usings):
    name = name.replace('global::', '')
    if '::' in name:
        alias, rest = name.split('::', 1)
        name = aliases.get(alias, alias) + '.' + rest
    if name in BCL:
        return 'System.' + BCL[name]
    if name in CONTAINERS:
        return CONTAINERS[name]
    if name in ('JsonElement', 'JsonDocument'):
        return 'System.Text.Json.' + name
    if name in ('MultipartJsonElement',):
        return 'Anthropic.Core.' + name
    if name in SYMBOLS or name.startswith('System.'):
        return name
    parts = name.split('.')
    if parts[0] in aliases:
        name = aliases[parts[0]] + '.' + '.'.join(parts[1:])
        if name in SYMBOLS:
            return name
    candidates = ['.'.join(ns.split('.')[:i]) + '.' + name for i in range(len(ns.split('.')), 0, -1)] + [u + '.' + name for u in usings]
    for candidate in candidates:
        if candidate in SYMBOLS:
            return candidate
    raise ValueError(name)


def parse_type(tokens, index, env):
    name = resolve(tokens[index], *env)
    index += 1
    args = []
    if index < len(tokens) and tokens[index] == '<':
        index += 1
        while True:
            arg, index = parse_type(tokens, index, env)
            args.append(arg)
            if tokens[index] == '>':
                index += 1
                break
            if tokens[index] != ',':
                raise ValueError('Invalid generic expression')
            index += 1
    formatted = 'global::' + name
    if args:
        formatted += '<' + ', '.join(args) + '>'
    while index < len(tokens) and tokens[index] in ('?', '['):
        if tokens[index] == '?':
            # Nullable reference annotations do not change the runtime type.
            if (
                name in SYMBOLS and SYMBOLS[name][0] in ('enum', 'struct')
                or name == 'System.Collections.Immutable.ImmutableArray'
                or name in ['System.' + v for v in BCL.values() if v not in ('String', 'Object')]
            ):
                formatted += '?'
            index += 1
        else:
            if tokens[index + 1] != ']':
                raise ValueError('Invalid array')
            formatted += '[]'
            index += 2
    return formatted, index

existing = CONTEXT.read_text(encoding='utf-8-sig')
entries = {line: re.sub(r'\s+', '', shape).replace(',', ', ') for line, shape in re.findall(r'(\[JsonSerializable\(\s*typeof\((.*?)\)\s*(?:,.*?)*\)\])', existing, re.S)}
registrations = {value: key for key, value in entries.items()}
required = set()


def add(shape):
    required.add(shape)
    if shape.startswith('global::System.Collections.Frozen.FrozenDictionary<'):
        for container in ('Dictionary', 'IReadOnlyDictionary'):
            required.add(shape.replace('System.Collections.Frozen.FrozenDictionary', 'System.Collections.Generic.' + container, 1))

for name, (kind, model) in SYMBOLS.items():
    if name.startswith('Anthropic.Models.') and (model or kind == 'enum'):
        add('global::' + name)
    if name.startswith('Anthropic.Models.') and kind in ('enum', 'struct') and (model or kind == 'enum'):
        add('global::' + name + '?')

for path, source, ns, aliases, usings in FILES:
    # Exclude comments and string literals before scanning generic type expressions.
    source = re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', ' ', source, flags=re.S)
    tokens = re.findall(r'(?:global::)?[A-Za-z_]\w*(?:(?:::|\.)[A-Za-z_]\w*)*|[<>,?\[\]]', source)
    for i, token in enumerate(tokens[:-1]):
        if token not in CONTAINERS or tokens[i + 1] != '<':
            continue
        try:
            shape, _ = parse_type(tokens, i, (ns, aliases, usings))
        except (ValueError, IndexError) as error:
            # Generic converter definitions contain open T, TModel, etc.
            if isinstance(error, ValueError) and re.fullmatch(r'T\w*', str(error)):
                continue
            raise ValueError(f'{path.relative_to(ROOT)}: {error}') from error
        add(shape)
        if token == 'IReadOnlyList':
            add(shape.replace('System.Collections.Generic.IReadOnlyList', 'System.Collections.Immutable.ImmutableArray', 1))
        elif token == 'IReadOnlyDictionary':
            add(shape.replace('System.Collections.Generic.IReadOnlyDictionary', 'System.Collections.Frozen.FrozenDictionary', 1))

# Thread helpers serialize materialized JSON block arrays outside the model sources.
add('global::System.Text.Json.JsonElement[]')
for shape in sorted(required):
    if shape not in registrations:
        suffix = hashlib.sha256(shape.encode()).hexdigest()[:16]
        registrations[shape] = f'[JsonSerializable(typeof({shape}), TypeInfoPropertyName = "Aot_{suffix}")]'

start = existing.index('[JsonSerializable(')
end = existing.index('internal partial class AnthropicJsonSerializerContext')
updated = existing[:start] + '\n'.join(registrations[key] for key in sorted(registrations)) + '\n' + existing[end:]
missing = required - set(entries.values())
if '--check' in sys.argv:
    if missing:
        sys.exit(f'Missing {len(missing)} AOT metadata registrations')
    print(f'AOT source coverage verified: {len(required)} required shapes')
else:
    CONTEXT.write_text(updated, encoding='utf-8', newline='\n')
    print(f'Added {len(missing)} registrations; {len(registrations)} total')
