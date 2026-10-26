"""Fetch pinned official PGDG Debian 12 client files; never installs system packages."""
import hashlib, io, json, pathlib, tarfile, urllib.request, sys

base = pathlib.Path(__file__).resolve().parent
target = pathlib.Path(sys.argv[1]).resolve()
target.mkdir(parents=True, exist_ok=True)
for package in json.loads((base / 'pg-tools.lock.json').read_text()).values():
    data = urllib.request.urlopen('https://apt.postgresql.org/pub/repos/apt/' + package['Filename'], timeout=60).read()
    if hashlib.sha256(data).hexdigest() != package['SHA256']:
        raise SystemExit('PostgreSQL client integrity check failed')
    if data[:8] != b'!<arch>\n':
        raise SystemExit('Invalid Debian archive')
    offset = 8
    while offset + 60 <= len(data):
        header = data[offset:offset + 60]
        name, size = header[:16].decode().strip().rstrip('/'), int(header[48:58])
        content = data[offset + 60:offset + 60 + size]
        offset += 60 + size + size % 2
        if not name.startswith('data.tar'):
            continue
        with tarfile.open(fileobj=io.BytesIO(content), mode='r:*') as archive:
            for member in archive:
                path = member.name.removeprefix('./')
                destination = None
                if member.isfile() and path == 'usr/lib/postgresql/17/bin/pg_dump':
                    destination = target / 'pg_dump'
                elif member.isfile() and path.startswith('usr/lib/x86_64-linux-gnu/libpq.so.5.'):
                    destination = target / 'libpq.so.5'
                elif member.isfile() and path == 'usr/share/doc/' + package['Package'] + '/copyright':
                    destination = target / (package['Package'] + '-copyright.txt')
                if destination:
                    destination.write_bytes(archive.extractfile(member).read())
for name in ('pg_dump', 'libpq.so.5'):
    if not (target / name).is_file():
        raise SystemExit('Missing PostgreSQL tool: ' + name)
print('Pinned PostgreSQL client prepared with SHA-256 verification.')
