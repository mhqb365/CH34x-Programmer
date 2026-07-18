#!/usr/bin/env python3
import glob
import hashlib
import importlib.util
import argparse
from collections import Counter
from pathlib import Path

parser_path = Path(__file__).with_name("parse-usbpcap.py")
spec = importlib.util.spec_from_file_location("parse_usbpcap", parser_path)
parse_usbpcap = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(parse_usbpcap)
read_packets = parse_usbpcap.read_packets


def find_t48(packets):
    for packet in packets:
        if bytes.fromhex("66A4530A") in packet["payload"]:
            return packet["bus"], packet["device"]
    return None, None


def main():
    parser = argparse.ArgumentParser(description="Summarize T48 USBPcap captures.")
    parser.add_argument("path", nargs="?", default=r"C:\Users\Windows\Desktop")
    args = parser.parse_args()

    root = Path(args.path)
    pattern = str(root / "*.pcap*") if root.is_dir() else args.path

    for file_name in sorted(glob.glob(pattern)):
        packets = list(read_packets(file_name))
        bus, device = find_t48(packets)
        frames = [
            packet
            for packet in packets
            if packet["bus"] == bus
            and packet["device"] == device
            and packet["endpoint"] in (0x01, 0x81, 0x02, 0x82)
        ]

        print(f"\n==== {Path(file_name).name} frames={len(frames)} target={bus}/{device} ====")
        counts = Counter((packet["endpoint"], len(packet["payload"])) for packet in frames)
        print("counts:", " ".join(f"ep=0x{ep:02X}/len={length}:{count}" for (ep, length), count in sorted(counts.items())))
        out8 = Counter(
            packet["payload"].hex().upper()
            for packet in frames
            if packet["endpoint"] == 0x01 and len(packet["payload"]) == 8
        )
        print("top-out8:", " ".join(f"{value}:{count}" for value, count in out8.most_common(12)))

        if len(frames) > 60:
            visible_frames = frames[:30] + [None] + frames[-30:]
        else:
            visible_frames = frames

        for packet in visible_frames:
            if packet is None:
                print("...")
                continue
            payload = packet["payload"]
            if len(payload) <= 64:
                body = payload.hex().upper()
            else:
                digest = hashlib.sha256(payload).hexdigest()[:12].upper()
                body = f"head={payload[:16].hex().upper()} tail={payload[-8:].hex().upper()} sha={digest}"
            print(f"{packet['index']:05d} ep=0x{packet['endpoint']:02X} len={len(payload):5d} {body}")


if __name__ == "__main__":
    main()
