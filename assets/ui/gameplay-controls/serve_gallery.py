"""Open this static concept gallery using a loopback-only server."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading
import urllib.request
import webbrowser

ROOT = Path(__file__).resolve().parent
if __name__ == '__main__':
    # Reuse only this gallery, never an arbitrary service that already occupies a port.
    for port in range(8815, 8840):
        url = f'http://127.0.0.1:{port}/'
        try:
            with urllib.request.urlopen(url, timeout=.3) as response:
                existing = response.read(16000).decode('utf-8', errors='replace')
            if 'Gameplay choices &amp; payment controls' in existing:
                webbrowser.open(url)
                print(url, flush=True)
                raise SystemExit(0)
        except (OSError, ValueError):
            pass
        try:
            server = ThreadingHTTPServer(('127.0.0.1', port), partial(SimpleHTTPRequestHandler, directory=str(ROOT)))
        except OSError:
            continue
        print(url, flush=True)
        threading.Timer(.3, lambda: webbrowser.open(url)).start()
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass
        finally:
            server.server_close()
        break
    else:
        raise SystemExit('No local review port is available.')
