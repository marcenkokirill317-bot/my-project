import argparse, socket, time
ap=argparse.ArgumentParser(); ap.add_argument("--host",default="127.0.0.1"); ap.add_argument("--port",type=int,default=27020)
a=ap.parse_args(); ADDR=(a.host,a.port)
def cl():
    s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.settimeout(2.0); return s
def snd(s,m): s.sendto(m.encode("utf-8"),ADDR)
def until(s,k,t=3.0):
    e=time.time()+t
    while time.time()<e:
        try: d,_=s.recvfrom(4096)
        except socket.timeout: continue
        f=d.decode("utf-8","replace").split("|")
        if len(f)>1 and f[1]==k: return f
    raise AssertionError("timeout waiting "+k)
def drain(s,sec=0.4):
    out=[]; s.settimeout(0.1); e=time.time()+sec
    while time.time()<e:
        try: d,_=s.recvfrom(4096); out.append(d.decode("utf-8","replace").split("|"))
        except socket.timeout: pass
    s.settimeout(2.0); return out
def ok(m): print("[PASS]",m)
A,B,C=cl(),cl(),cl()
snd(A,"MSC1|HELLO|V2_A"); wa=until(A,"WELCOME")
snd(B,"MSC1|HELLO|V2_B"); wb=until(B,"WELCOME"); until(A,"PLAYER_JOIN"); ok("two clients joined")
snd(A,"MSC1|POS2|1|10|1|20|90|5|1|1.5|0|0|1000")
st=until(B,"ST2")
assert len(st)==14, "ST2 must have 14 fields, got %d"%len(st)
assert st[2]==wa[2] and st[3]=="1" and st[9]=="1", st
ok("ST2 format / id / seq / move state")
time.sleep(0.05)                      # rate limit 50 packets/s
snd(A,"MSC1|POS2|5|11|1|20|90|0|1|1.5|0|0|1100"); time.sleep(0.05)
snd(A,"MSC1|POS2|4|12|1|20|90|0|1|1.5|0|0|1050")
seqs=[p[3] for p in drain(B,0.5) if p[1]=="ST2"]
assert "5" in seqs and "4" not in seqs, seqs
ok("stale / out-of-order packets dropped")
time.sleep(0.05)
snd(A,"MSC1|POS2|6|99999|1|20|0|0|0|0|0|0|1200")
assert not [p for p in drain(B,0.4) if p[1]=="ST2"]
ok("out-of-range coordinates rejected")
time.sleep(0.05)
for i in range(7,107): snd(A,"MSC1|POS2|%d|10|1|20|0|0|1|1|0|0|%d"%(i,1200+i))
n=len([p for p in drain(B,0.6) if p[1]=="ST2"]); assert n<60, n
ok("rate limit: %d of 100 passed"%n)
snd(C,"MSC1|HELLO|V1_C"); until(C,"WELCOME"); snd(C,"MSC1|POS|5|1|5|180")
lg=until(B,"STATE"); assert lg[3]=="V1_C", lg
ok("backward compatibility: v1 client visible to v2")
snd(B,"MSC1|BYE"); lv=until(A,"PLAYER_LEAVE"); assert lv[2]==wb[2]
ok("disconnect notification")
for s in (A,B,C):
    try: snd(s,"MSC1|BYE")
    except Exception: pass
    s.close()
print("ALL PROTOCOL V2 TESTS PASSED")