#!/usr/bin/env python3
import argparse
import hashlib
import struct
from pathlib import Path


def read_packets(path):
    data = Path(path).read_bytes()
    if data[:4] != bytes.fromhex("d4c3b2a1"):
        raise SystemExit("Only little-endian classic PCAP files are supported.")

    offset = 24
    index = 0
    while offset < len(data):
        if offset + 16 > len(data):
            break
        ts_sec, ts_usec, captured_len, original_len = struct.unpack_from("<IIII", data, offset)
        offset += 16
        packet = data[offset : offset + captured_len]
        offset += captured_len

        if len(packet) < 28:
            index += 1
            continue

        header_len = struct.unpack_from("<H", packet, 0)[0]
        if header_len > len(packet):
            index += 1
            continue

        # USBPcap header layout as observed in captures from this environment.
        info = packet[16]
        bus = packet[17]
        device = packet[19]
        endpoint = packet[21]
        transfer = packet[22]
        declared_len = struct.unpack_from("<I", packet, 23)[0]
        payload = packet[header_len:]

        yield {
            "index": index,
            "time": ts_sec + ts_usec / 1_000_000,
            "info": info,
            "bus": bus,
            "device": device,
            "endpoint": endpoint,
            "transfer": transfer,
            "declared_len": declared_len,
            "payload": payload,
        }
        index += 1


def main():
    parser = argparse.ArgumentParser(description="Extract T48 USB bulk frames from a USBPcap PCAP.")
    parser.add_argument("pcap")
    parser.add_argument("--bus", type=int)
    parser.add_argument("--device", type=int)
    parser.add_argument("--endpoints", default="01,81,02,82")
    parser.add_argument("--full", action="store_true", help="Print full payloads instead of prefix/suffix summaries.")
    args = parser.parse_args()

    endpoints = {int(value, 16) for value in args.endpoints.split(",")}
    packets = list(read_packets(args.pcap))
    bus = args.bus
    device = args.device

    if bus is None or device is None:
        for packet in packets:
            payload = packet["payload"]
            if bytes.fromhex("66A4530A") in payload:
                bus = packet["bus"]
                device = packet["device"]
                break

    if bus is None or device is None:
        raise SystemExit("Unable to auto-detect T48. Pass --bus and --device.")

    count = 0
    print(f"target bus={bus} device={device}")
    for packet in packets:
        if packet["bus"] != bus or packet["device"] != device:
            continue
        if packet["endpoint"] not in endpoints:
            continue

        direction = "IN " if packet["endpoint"] & 0x80 else "OUT"
        payload_bytes = packet["payload"]
        payload = payload_bytes.hex().upper()
        if not args.full and len(payload_bytes) > 80:
            digest = hashlib.sha256(payload_bytes).hexdigest()[:16].upper()
            payload = f"{payload[:96]}...{payload[-32:]} sha256={digest}"
        print(
            f"{packet['index']:05d} {packet['time']:.6f} "
            f"{direction} ep=0x{packet['endpoint']:02X} "
            f"info={packet['info']} xfer={packet['transfer']} "
            f"len={len(packet['payload'])}/{packet['declared_len']} {payload}"
        )
        count += 1

    print(f"frames={count}")


if __name__ == "__main__":
    main()
