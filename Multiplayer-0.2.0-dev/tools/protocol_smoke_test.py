#!/usr/bin/env python3
"""UDP smoke test for a running MSC Multiplayer server. Python 3, stdlib only."""
import argparse
import socket
import sys
import time

PREFIX = "MSC1|"

def receive_until(sock, wanted, timeout=4.0):
    deadline = time.time() + timeout
    received = []
    while time.time() < deadline:
        sock.settimeout(max(0.05, min(0.25, deadline - time.time())))
        try:
            data, _ = sock.recvfrom(2048)
        except socket.timeout:
            continue
        packet = data.decode("utf-8", errors="replace")
        received.append(packet)
        if packet.startswith(PREFIX + wanted + "|") or packet == PREFIX + wanted:
            return packet, received
    raise AssertionError("Timed out waiting for %s; received: %r" % (wanted, received))

def make_client(host, port, nickname):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("", 0))
    sock.sendto((PREFIX + "HELLO|" + nickname).encode("utf-8"), (host, port))
    return sock

def main():
    parser = argparse.ArgumentParser(description="Smoke-test a running MSC Multiplayer UDP server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=27020)
    args = parser.parse_args()
    sockets = []
    try:
        a = make_client(args.host, args.port, "Smoke_A"); sockets.append(a)
        wa, _ = receive_until(a, "WELCOME")
        id_a = wa.split("|")[2]
        print("[PASS] first client joins:", id_a)

        b = make_client(args.host, args.port, "Smoke_B"); sockets.append(b)
        wb, packets_b = receive_until(b, "WELCOME")
        id_b = wb.split("|")[2]
        if not any(x.startswith(PREFIX + "PLAYER_JOIN|" + id_a + "|") for x in packets_b):
            raise AssertionError("new client did not receive existing player's lobby entry")
        jb, _ = receive_until(a, "PLAYER_JOIN")
        if not jb.startswith(PREFIX + "PLAYER_JOIN|" + id_b + "|"):
            raise AssertionError("existing client did not receive second player's join")
        print("[PASS] lobby roster and PLAYER_JOIN")

        c = make_client(args.host, args.port, "Smoke_C"); sockets.append(c)
        receive_until(c, "WELCOME")
        d = make_client(args.host, args.port, "Smoke_D"); sockets.append(d)
        wd, _ = receive_until(d, "WELCOME")
        id_d = wd.split("|")[2]
        print("[PASS] four-player capacity")

        e = make_client(args.host, args.port, "Smoke_E"); sockets.append(e)
        error, _ = receive_until(e, "ERROR")
        if "server_full" not in error:
            raise AssertionError("fifth client was not rejected: " + error)
        print("[PASS] fifth client rejected")

        a.sendto((PREFIX + "POS|12.5|3|8.25|90").encode("utf-8"), (args.host, args.port))
        state, _ = receive_until(b, "STATE")
        fields = state.split("|")
        if len(fields) != 8 or fields[2] != id_a or fields[4:7] != ["12.5", "3", "8.25"]:
            raise AssertionError("invalid STATE packet: " + state)
        print("[PASS] position state broadcast")

        d.sendto((PREFIX + "BYE").encode("utf-8"), (args.host, args.port))
        leave, _ = receive_until(a, "PLAYER_LEAVE")
        if leave != PREFIX + "PLAYER_LEAVE|" + id_d:
            raise AssertionError("unexpected PLAYER_LEAVE packet: " + leave)
        print("[PASS] disconnect notification")

        e.sendto((PREFIX + "HELLO|Smoke_E_Retry").encode("utf-8"), (args.host, args.port))
        receive_until(e, "WELCOME")
        print("[PASS] freed slot can be reused")
        print("ALL SERVER SMOKE TESTS PASSED")
        return 0
    except Exception as exc:
        print("SMOKE TEST FAILED:", exc, file=sys.stderr)
        return 1
    finally:
        for sock in sockets:
            try: sock.close()
            except Exception: pass

if __name__ == "__main__":
    sys.exit(main())
