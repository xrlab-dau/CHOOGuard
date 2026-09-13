#!/usr/bin/env python3
"""Bounded loopback-only opaque UDP impairment for local protocol verification.

Counters describe UDP payloads handled by this proxy, not IP/link overhead or WAN acceptance.
"""
import argparse
import asyncio
import ipaddress
import json
import math
import os
from pathlib import Path
import random
import signal
import socket
import tempfile
import time


class DelayStats:
    _MAX_FINITE_BUCKET_MS = 10000

    def __init__(self):
        self.count = 0
        self.total = 0.0
        self.square_total = 0.0
        self.minimum = math.inf
        self.maximum = 0.0
        self.overflow_minimum = math.inf
        self.overflow_maximum = 0.0
        # Bins are [0, 1], (1, 2], ..., (9999, 10000], (10000, +inf).
        self.bins = [0] * (self._MAX_FINITE_BUCKET_MS + 2)

    def add(self, milliseconds):
        if not isinstance(milliseconds, (int, float)) or isinstance(milliseconds, bool) or not math.isfinite(milliseconds) or milliseconds < 0:
            raise ValueError("Delay sample must be finite and nonnegative")
        self.count += 1
        self.total += milliseconds
        self.square_total += milliseconds * milliseconds
        self.minimum = min(self.minimum, milliseconds)
        self.maximum = max(self.maximum, milliseconds)
        if milliseconds > self._MAX_FINITE_BUCKET_MS:
            self.overflow_minimum = min(self.overflow_minimum, milliseconds)
            self.overflow_maximum = max(self.overflow_maximum, milliseconds)
        self.bins[min(self._MAX_FINITE_BUCKET_MS + 1, math.ceil(milliseconds))] += 1

    def snapshot(self):
        def percentile(fraction):
            if not self.count:
                return None
            target = math.ceil(self.count * fraction)
            seen = 0
            for index, count in enumerate(self.bins):
                seen += count
                if seen >= target:
                    # The overflow bucket has no finite upper bound. Do not
                    # report its observed maximum as an exact percentile.
                    return index if index <= self._MAX_FINITE_BUCKET_MS else None
            raise RuntimeError("Delay histogram is inconsistent")

        overflow = self.bins[self._MAX_FINITE_BUCKET_MS + 1]
        mean = self.total / self.count if self.count else None
        return {"count": self.count, "min": self.minimum if self.count else None,
                "max": self.maximum if self.count else None, "mean": mean,
                "stddev": math.sqrt(max(0, self.square_total / self.count - mean * mean)) if self.count else None,
                "p50_upper_ms": percentile(.5), "p95_upper_ms": percentile(.95),
                "p99_upper_ms": percentile(.99), "histogram_resolution_ms": 1,
                "overflow_count": overflow,
                "overflow_min_ms": self.overflow_minimum if overflow else None,
                "overflow_max_ms": self.overflow_maximum if overflow else None}


class Direction:
    def __init__(self):
        self.values = dict(received=0, received_bytes=0, delivered=0, delivered_bytes=0,
                           random_dropped=0, random_dropped_bytes=0,
                           capacity_dropped=0, capacity_dropped_bytes=0,
                           shutdown_dropped=0, shutdown_dropped_bytes=0,
                           endpoint_dropped=0, endpoint_dropped_bytes=0,
                           send_pressure_dropped=0, send_pressure_dropped_bytes=0)
        self.delay = DelayStats()

    def snapshot(self):
        return dict(self.values, delay_ms=self.delay.snapshot())


class Flow:
    def __init__(self, address, now):
        self.address = address
        self.last_seen = now
        self.transport = None
        self.sender = None
        self.start_error = None
        self.ready = None
        self.pending = 0


class Listener(asyncio.DatagramProtocol):
    def __init__(self, relay):
        self.relay = relay

    def connection_made(self, transport):
        self.relay.transport = transport
        sender = None
        try:
            sender = transport.get_extra_info("socket").dup()
            sender.setblocking(False)
            self.relay.sender = sender
        except (AttributeError, OSError, RuntimeError) as error:
            if sender is not None:
                sender.close()
            self.relay.sender = None
            self.relay.start_error = error
            transport.abort()

    def datagram_received(self, data, address):
        self.relay.receive("client_to_server", data, address)

    def error_received(self, error):
        self.relay.socket_errors += 1


class _StartFailure(RuntimeError):
    """Internal marker used when startup is cancelled by stop()."""


class Upstream(asyncio.DatagramProtocol):
    def __init__(self, relay, flow):
        self.relay, self.flow = relay, flow
    def connection_made(self, transport):
        self.flow.transport = transport
        sender = None
        try:
            sender = transport.get_extra_info("socket").dup()
            sender.setblocking(False)
            self.flow.sender = sender
        except (AttributeError, OSError, RuntimeError) as error:
            if sender is not None:
                sender.close()
            self.flow.sender = None
            self.flow.start_error = error
            transport.abort()

    def datagram_received(self, data, address):
        self.relay.receive("server_to_client", data, self.flow.address)
    def error_received(self, error):
        self.relay.socket_errors += 1


class UdpImpairment:
    def __init__(self, upstream, *, delay_ms=0, loss=0, seed=1, max_flows=128,
                 max_queued_bytes=8 * 1024 * 1024, max_queued_packets=8192, idle_seconds=300):
        if not ipaddress.ip_address(upstream[0]).is_loopback or not 1 <= upstream[1] <= 65535:
            raise ValueError("Upstream must be a numeric loopback UDP endpoint")
        if isinstance(delay_ms, bool) or not isinstance(delay_ms, (int, float)) or not math.isfinite(delay_ms) or not 0 <= delay_ms <= 5000:
            raise ValueError("Delay must be in [0,5000] milliseconds")
        if isinstance(loss, bool) or not isinstance(loss, (int, float)) or not math.isfinite(loss) or not 0 <= loss <= 1:
            raise ValueError("Loss must be a probability")
        budgets = (max_flows, max_queued_bytes, max_queued_packets)
        if any(isinstance(value, bool) or not isinstance(value, int) for value in budgets):
            raise ValueError("Invalid flow/memory budget")
        if not 1 <= max_flows <= 1024 or not 1 <= max_queued_bytes <= 64 * 1024 * 1024 or not 1 <= max_queued_packets <= 65536:
            raise ValueError("Invalid flow/memory budget")
        if isinstance(idle_seconds, bool) or not isinstance(idle_seconds, (int, float)) or not math.isfinite(idle_seconds) or idle_seconds <= 0:
            raise ValueError("Invalid idle timeout")
        if isinstance(seed, bool) or not isinstance(seed, int):
            raise ValueError("Seed must be an integer")
        if isinstance(upstream[1], bool) or not isinstance(upstream[1], int):
            raise ValueError("Upstream must use an integer UDP port")
        self.upstream = upstream
        self.delay_seconds = delay_ms / 1000
        self.loss = loss
        self.seed = seed
        self.random = random.Random(seed)
        self.max_flows = max_flows
        self.max_queued_bytes = max_queued_bytes
        self.max_queued_packets = max_queued_packets
        self.idle_seconds = idle_seconds
        self.flows = {}
        self.pending = {}
        self.tasks = set()
        self.next_token = 0
        self.queued_bytes = 0
        self.peak_queued_bytes = 0
        self.socket_errors = 0
        self.transport = None
        self.sender = None
        self.running = False
        self.closed = False
        self._starting = False
        self.start_error = None
        self.directions = {name: Direction() for name in ("client_to_server", "server_to_client")}

    def _close_listener_resources(self):
        if self.sender is not None:
            self.sender.close()
        if self.transport is not None:
            self.transport.abort()

    async def start(self, address):
        if self.running or self.closed or self._starting or not ipaddress.ip_address(address[0]).is_loopback:
            raise ValueError("Start a fresh proxy on a numeric loopback endpoint")
        self._starting = True
        self.start_error = None
        self.loop = asyncio.get_running_loop()
        self.started_at = self.loop.time()
        family = socket.AF_INET6 if ":" in address[0] else socket.AF_INET
        try:
            await self.loop.create_datagram_endpoint(lambda: Listener(self), local_addr=address, family=family)
            if self.closed:
                raise _StartFailure("Proxy stopped during startup")
            if self.start_error is not None or self.sender is None or self.transport is None:
                raise self.start_error or OSError("Listener sender was not created")
            self.address = self.transport.get_extra_info("sockname")
            self.running = True
            self.expirer = asyncio.create_task(self.expire())
            return self.address
        except BaseException:
            if not self.running:
                self._close_listener_resources()
            raise
        finally:
            self._starting = False

    def receive(self, direction, data, address):
        if not self.running:
            return
        stats = self.directions[direction].values
        stats["received"] += 1
        stats["received_bytes"] += len(data)
        if direction == "server_to_client" and address not in self.flows:
            stats["endpoint_dropped"] += 1
            stats["endpoint_dropped_bytes"] += len(data)
            return
        if self.random.random() < self.loss:
            stats["random_dropped"] += 1
            stats["random_dropped_bytes"] += len(data)
            return
        now = self.loop.time()
        flow = self.flows.get(address)
        if self.queued_bytes + len(data) > self.max_queued_bytes or len(self.pending) >= self.max_queued_packets or (flow is None and len(self.flows) >= self.max_flows):
            stats["capacity_dropped"] += 1
            stats["capacity_dropped_bytes"] += len(data)
            return
        if flow is None:
            if direction != "client_to_server":
                stats["endpoint_dropped"] += 1
                stats["endpoint_dropped_bytes"] += len(data)
                return
            flow = Flow(address, now)
            self.flows[address] = flow
            flow.ready = asyncio.create_task(self.connect(flow))
            flow.ready.add_done_callback(
                lambda ready, owner=flow: self._finish_ready(owner, ready)
            )
        flow.last_seen = now
        self.next_token += 1
        token = self.next_token
        self.pending[token] = {"data": data, "flow": flow, "direction": direction,
                               "arrived": now, "due": now + self.delay_seconds, "handle": None}
        self.queued_bytes += len(data)
        self.peak_queued_bytes = max(self.peak_queued_bytes, self.queued_bytes)
        flow.pending += 1
        task = asyncio.create_task(self.schedule(token))
        self.tasks.add(task)
        task.add_done_callback(self.tasks.discard)

    def _finish_ready(self, flow, ready):
        if ready.cancelled():
            self.drop_flow(
                flow,
                outcome="shutdown_dropped" if self.closed else "endpoint_dropped",
            )

    async def connect(self, flow):
        family = socket.AF_INET6 if ":" in self.upstream[0] else socket.AF_INET
        transport = None
        try:
            transport, _ = await self.loop.create_datagram_endpoint(
                lambda: Upstream(self, flow), remote_addr=self.upstream, family=family)
            if flow.start_error is not None or flow.sender is None:
                raise flow.start_error or OSError("Upstream sender was not created")
            if not self.running:
                raise _StartFailure("Proxy stopped during endpoint startup")
        except asyncio.CancelledError:
            if transport is not None:
                transport.abort()
            self.drop_flow(flow, outcome="shutdown_dropped" if self.closed else "endpoint_dropped")
            raise
        except Exception:
            if transport is not None:
                transport.abort()
            self.drop_flow(
                flow,
                outcome="shutdown_dropped" if self.closed else "endpoint_dropped",
            )
            return

    async def schedule(self, token):
        try:
            packet = self.pending.get(token)
            if packet is None:
                return
            await packet["flow"].ready
            packet = self.pending.get(token)
            if packet is not None and self.running:
                packet["handle"] = self.loop.call_at(packet["due"], self.deliver, token)
        except asyncio.CancelledError:
            raise
        except Exception:
            packet = self.release(token, "endpoint_dropped")
            if packet is not None:
                self.drop_flow(packet["flow"])

    def release(self, token, outcome):
        packet = self.pending.pop(token, None)
        if packet is None:
            return None
        self.queued_bytes -= len(packet["data"])
        packet["flow"].pending -= 1
        values = self.directions[packet["direction"]].values
        values[outcome] += 1
        if outcome.endswith("_dropped"):
            values[outcome + "_bytes"] += len(packet["data"])
        return packet

    def _close_flow_resources(self, flow):
        if flow.sender is not None:
            flow.sender.close()
        if flow.transport is not None:
            flow.transport.abort()

    def drop_flow(self, flow, *, outcome="endpoint_dropped"):
        if self.flows.get(flow.address) is flow:
            del self.flows[flow.address]
        if flow.ready is not None and not flow.ready.done() and flow.ready is not asyncio.current_task():
            flow.ready.cancel()
        self._close_flow_resources(flow)
        for token, packet in list(self.pending.items()):
            if packet["flow"] is flow:
                if packet["handle"] is not None:
                    packet["handle"].cancel()
                self.release(token, outcome)

    def deliver(self, token):
        packet = self.pending.get(token)
        if packet is None:
            return
        if not self.running:
            self.release(token, "shutdown_dropped")
            return
        flow = packet["flow"]
        transport = flow.transport if packet["direction"] == "client_to_server" else self.transport
        if transport is None or transport.is_closing():
            packet = self.release(token, "endpoint_dropped")
            if packet is not None:
                self.drop_flow(packet["flow"])
            return
        try:
            # Use a duplicated nonblocking socket for writes. DatagramTransport.sendto can
            # retain an unbounded Python queue under EWOULDBLOCK, outside our packet budget.
            sender = flow.sender if packet["direction"] == "client_to_server" else self.sender
            if packet["direction"] == "client_to_server":
                sender.send(packet["data"])
            else:
                sender.sendto(packet["data"], flow.address)
        except (BlockingIOError, InterruptedError):
            self.release(token, "send_pressure_dropped")
            return
        except (OSError, RuntimeError):
            packet = self.release(token, "endpoint_dropped")
            if packet is not None:
                self.drop_flow(packet["flow"])
            return
        packet = self.release(token, "delivered")
        direction = self.directions[packet["direction"]]
        direction.values["delivered_bytes"] += len(packet["data"])
        direction.delay.add((self.loop.time() - packet["arrived"]) * 1000)

    async def expire(self):
        while self.running:
            await asyncio.sleep(min(1, self.idle_seconds))
            now = self.loop.time()
            for address, flow in list(self.flows.items()):
                if flow.pending == 0 and now - flow.last_seen >= self.idle_seconds:
                    self.drop_flow(flow, outcome="endpoint_dropped")

    async def stop(self):
        if self.closed:
            return
        self.closed = True
        self.running = False
        if hasattr(self, "expirer"):
            self.expirer.cancel()
        for token, packet in list(self.pending.items()):
            if packet["handle"] is not None:
                packet["handle"].cancel()
            self.release(token, "shutdown_dropped")
        for task in list(self.tasks):
            task.cancel()
        for flow in list(self.flows.values()):
            if flow.ready is not None and not flow.ready.done():
                flow.ready.cancel()
            self._close_flow_resources(flow)
        self._close_listener_resources()
        waiting = list(self.tasks) + [f.ready for f in self.flows.values() if f.ready is not None]
        if hasattr(self, "expirer"):
            waiting.append(self.expirer)
        if waiting:
            await asyncio.gather(*waiting, return_exceptions=True)
        self.flows.clear()

    def snapshot(self):
        return {"classification": "local_opaque_udp_impairment_counters", "seed": self.seed,
                "delivery_meaning": "accepted_by_nonblocking_kernel_send_remote_receipt_not_assumed",
                "configured_one_way_delay_ms": self.delay_seconds * 1000, "configured_loss": self.loss,
                "flows": len(self.flows), "queued_bytes": self.queued_bytes, "queued_packets": len(self.pending), "peak_queued_bytes": self.peak_queued_bytes,
                "socket_errors": self.socket_errors, **{name: stats.snapshot() for name, stats in self.directions.items()}}


def endpoint(text):
    host, port = text.rsplit(":", 1)
    return host.strip("[]"), int(port)


def write_metrics(path, stats):
    path = Path(path)
    if path.is_symlink():
        raise ValueError("Metrics path must not be a symlink")
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    temporary = Path(temporary_name)
    owns_fd = True
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "w") as stream:
            owns_fd = False
            json.dump(stats, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except Exception:
        if owns_fd:
            try:
                os.close(fd)
            except OSError:
                pass
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass
        raise


async def main_async(args):
    relay = UdpImpairment(endpoint(args.upstream), delay_ms=args.delay_ms, loss=args.loss, seed=args.seed)
    stop = asyncio.Event()
    loop = asyncio.get_running_loop()
    for sig in (signal.SIGTERM, signal.SIGINT):
        try:
            loop.add_signal_handler(sig, stop.set)
        except NotImplementedError:
            pass
    if args.metrics.is_symlink():
        raise ValueError("Metrics path must not be a symlink")
    args.metrics.parent.mkdir(parents=True, exist_ok=True)
    await relay.start(endpoint(args.listen))
    print(json.dumps({"status": "listening", "address": relay.address, "scope": "loopback impairment, not WAN"}), flush=True)
    started = time.monotonic()
    try:
        while not stop.is_set() and (args.duration <= 0 or time.monotonic() - started < args.duration):
            write_metrics(args.metrics, relay.snapshot())
            try:
                remaining = args.duration - (time.monotonic() - started) if args.duration > 0 else 1
                await asyncio.wait_for(stop.wait(), max(0, min(1, remaining)))
            except TimeoutError:
                pass
    finally:
        await relay.stop()
        write_metrics(args.metrics, relay.snapshot())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--listen", default="127.0.0.1:17970")
    parser.add_argument("--upstream", required=True)
    parser.add_argument("--delay-ms", type=float, default=50)
    parser.add_argument("--loss", type=float, default=.01)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--duration", type=float, default=0)
    parser.add_argument("--metrics", type=Path, required=True)
    args = parser.parse_args()
    if not math.isfinite(args.duration) or args.duration < 0:
        parser.error("duration must be nonnegative and finite")
    asyncio.run(main_async(args))


if __name__ == "__main__":
    main()
