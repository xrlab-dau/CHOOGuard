import asyncio
import json
import os
import socket
import stat
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

from udp_impairment import DelayStats, UdpImpairment, write_metrics

class Echo(asyncio.DatagramProtocol):
    def connection_made(self, transport):
        self.transport = transport
        self.received = []
    def datagram_received(self, data, address):
        self.received.append((data, address))
        self.transport.sendto(data, address)

class Client(asyncio.DatagramProtocol):
    def connection_made(self, transport):
        self.transport = transport
        self.queue = asyncio.Queue()
    def datagram_received(self, data, address):
        self.queue.put_nowait(data)

class MetricsFileTests(unittest.TestCase):
    def test_write_metrics_rejects_existing_symlink_without_touching_target(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sentinel = root / "sentinel.json"
            sentinel.write_text("sentinel\\n")
            metrics = root / "metrics.json"
            metrics.symlink_to(sentinel)

            with self.assertRaises(ValueError):
                write_metrics(metrics, {"status": "test"})

            self.assertEqual(sentinel.read_text(), "sentinel\\n")
            self.assertTrue(metrics.is_symlink())

    def test_write_metrics_uses_private_unique_temp_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sentinel = root / "sentinel.json"
            sentinel.write_text("sentinel\\n")
            stale_temp = root / "metrics.json.tmp"
            stale_temp.symlink_to(sentinel)
            metrics = root / "metrics.json"

            write_metrics(metrics, {"status": "test"})

            self.assertEqual(sentinel.read_text(), "sentinel\\n")
            self.assertTrue(metrics.is_file() and not metrics.is_symlink())
            self.assertEqual(stat.S_IMODE(metrics.stat().st_mode), 0o600)
            self.assertEqual(json.loads(metrics.read_text()), {"status": "test"})
            self.assertTrue(stale_temp.is_symlink())

    def test_write_metrics_fchmod_failure_closes_raw_fd_and_preserves_output(self):
        import udp_impairment
        with tempfile.TemporaryDirectory() as directory:
            metrics = Path(directory) / "metrics.json"
            metrics.write_text("old-output")
            descriptors = []
            real_mkstemp = udp_impairment.tempfile.mkstemp

            def track_mkstemp(*args, **kwargs):
                fd, name = real_mkstemp(*args, **kwargs)
                descriptors.append(fd)
                return fd, name

            with patch.object(udp_impairment.tempfile, "mkstemp", side_effect=track_mkstemp), \
                 patch.object(udp_impairment.os, "fchmod", side_effect=OSError("fixture fchmod failure")):
                with self.assertRaises(OSError):
                    write_metrics(metrics, {"new": True})

            for fd in descriptors:
                with self.assertRaises(OSError):
                    os.fstat(fd)
            self.assertEqual(metrics.read_text(), "old-output")
            self.assertEqual(list(Path(directory).iterdir()), [metrics])

    def test_write_metrics_fdopen_failure_closes_raw_fd_and_preserves_output(self):
        import udp_impairment
        with tempfile.TemporaryDirectory() as directory:
            metrics = Path(directory) / "metrics.json"
            metrics.write_text("old-output")
            descriptors = []
            real_mkstemp = udp_impairment.tempfile.mkstemp

            def track_mkstemp(*args, **kwargs):
                fd, name = real_mkstemp(*args, **kwargs)
                descriptors.append(fd)
                return fd, name

            with patch.object(udp_impairment.tempfile, "mkstemp", side_effect=track_mkstemp), \
                 patch.object(udp_impairment.os, "fdopen", side_effect=OSError("fixture fdopen failure")):
                with self.assertRaises(OSError):
                    write_metrics(metrics, {"new": True})

            for fd in descriptors:
                with self.assertRaises(OSError):
                    os.fstat(fd)
            self.assertEqual(metrics.read_text(), "old-output")
            self.assertEqual(list(Path(directory).iterdir()), [metrics])


class DelayStatsTests(unittest.TestCase):
    def test_percentiles_are_upper_bounds_and_include_overflow(self):
        stats = DelayStats()
        for value in (0.1, 1.0, 1.1, 10000.1, 10001.0):
            stats.add(value)
        snapshot = stats.snapshot()
        self.assertEqual(snapshot["p50_upper_ms"], 2)
        self.assertIsNone(snapshot["p95_upper_ms"])
        self.assertIsNone(snapshot["p99_upper_ms"])
        self.assertEqual(snapshot["overflow_count"], 2)
        self.assertEqual(snapshot["overflow_min_ms"], 10000.1)
        self.assertEqual(snapshot["overflow_max_ms"], 10001.0)

    def test_empty_stats_have_no_percentile_or_overflow_claim(self):
        snapshot = DelayStats().snapshot()
        self.assertIsNone(snapshot["p95_upper_ms"])
        self.assertEqual(snapshot["overflow_count"], 0)
        self.assertIsNone(snapshot["overflow_min_ms"])
        self.assertIsNone(snapshot["overflow_max_ms"])


class ImpairmentTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.loop = asyncio.get_running_loop()
        self.echo_transport, self.echo = await self.loop.create_datagram_endpoint(Echo, local_addr=("127.0.0.1", 0))
        self.upstream = self.echo_transport.get_extra_info("sockname")
        self.clients = []
        self.relays = []

    async def asyncTearDown(self):
        for relay in self.relays:
            await relay.stop()
        for transport in self.clients:
            transport.close()
        self.echo_transport.close()
        await asyncio.sleep(0)

    async def relay(self, **kwargs):
        relay = UdpImpairment(self.upstream, **kwargs)
        await relay.start(("127.0.0.1", 0))
        self.relays.append(relay)
        return relay

    async def client(self, address):
        transport, protocol = await self.loop.create_datagram_endpoint(Client, remote_addr=address)
        self.clients.append(transport)
        return protocol

    async def test_binary_datagrams_and_distinct_client_flows(self):
        relay = await self.relay()
        a = await self.client(relay.address)
        b = await self.client(relay.address)
        first, second = bytes(range(256)), b"\x00\xffsecond\x00"
        a.transport.sendto(first)
        b.transport.sendto(second)
        self.assertEqual(await asyncio.wait_for(a.queue.get(), 1), first)
        self.assertEqual(await asyncio.wait_for(b.queue.get(), 1), second)
        self.assertEqual(len({address for _, address in self.echo.received}), 2)
        stats = relay.snapshot()
        self.assertEqual(stats["client_to_server"]["delivered"], 2)
        self.assertEqual(stats["server_to_client"]["delivered"], 2)
        self.assertEqual(stats["queued_bytes"], 0)

    async def test_fixed_delay_is_applied_in_both_directions(self):
        relay = await self.relay(delay_ms=20)
        client = await self.client(relay.address)
        started = time.monotonic()
        client.transport.sendto(b"latency")
        self.assertEqual(await asyncio.wait_for(client.queue.get(), 1), b"latency")
        self.assertGreaterEqual(time.monotonic() - started, .038)
        for direction in ("client_to_server", "server_to_client"):
            self.assertGreaterEqual(relay.snapshot()[direction]["delay_ms"]["min"], 19)

    async def test_total_loss_is_counted_without_logging_or_forwarding_payloads(self):
        relay = await self.relay(loss=1)
        client = await self.client(relay.address)
        for i in range(4):
            client.transport.sendto(bytes([i]) * 7)
        await asyncio.sleep(.03)
        stats = relay.snapshot()["client_to_server"]
        self.assertEqual(stats["received"], 4)
        self.assertEqual(stats["received_bytes"], 28)
        self.assertEqual(stats["random_dropped"], 4)
        self.assertEqual(stats["random_dropped_bytes"], 28)
        self.assertEqual(stats["delivered"], 0)
        self.assertEqual(self.echo.received, [])

    async def test_pending_memory_is_bounded_and_shutdown_discards_pending_packets(self):
        relay = await self.relay(delay_ms=200, max_queued_bytes=10)
        client = await self.client(relay.address)
        client.transport.sendto(b"12345678")
        client.transport.sendto(b"abcdefgh")
        await asyncio.sleep(.02)
        self.assertEqual(relay.snapshot()["queued_bytes"], 8)
        self.assertEqual(relay.snapshot()["client_to_server"]["capacity_dropped"], 1)
        self.assertEqual(relay.snapshot()["client_to_server"]["capacity_dropped_bytes"], 8)
        await relay.stop()
        stats = relay.snapshot()
        self.assertEqual(stats["queued_bytes"], 0)
        self.assertEqual(stats["client_to_server"]["shutdown_dropped"], 1)
        self.assertEqual(stats["client_to_server"]["shutdown_dropped_bytes"], 8)
        await asyncio.sleep(.22)
        self.assertEqual(self.echo.received, [])

    async def test_invalid_or_nonloopback_configuration_is_rejected(self):
        invalid_options = (
            {"loss": -1}, {"loss": 1.1}, {"loss": float("nan")},
            {"delay_ms": -1}, {"delay_ms": float("inf")},
            {"max_flows": 0}, {"max_queued_bytes": 0},
            {"max_queued_packets": 0}, {"idle_seconds": 0},
            {"delay_ms": True}, {"loss": False}, {"idle_seconds": True},
            {"max_flows": 1.5}, {"max_queued_bytes": 1.5},
            {"max_queued_packets": 1.5}, {"seed": None},
            {"seed": True}, {"seed": 1.5},
        )
        for options in invalid_options:
            with self.assertRaises(ValueError):
                UdpImpairment(self.upstream, **options)
        with self.assertRaises(ValueError):
            UdpImpairment(("192.0.2.1", 7777))
        with self.assertRaises(ValueError):
            UdpImpairment(("127.0.0.1", True))

    async def test_failed_send_releases_the_flow_for_new_clients(self):
        relay = await self.relay()
        client = await self.client(relay.address)
        client.transport.sendto(b"prime")
        self.assertEqual(await asyncio.wait_for(client.queue.get(), 1), b"prime")
        flow = next(iter(relay.flows.values()))
        original = flow.sender

        class Closed:
            closed = False
            def send(self, data):
                raise OSError("fixture closed socket")
            def close(self):
                self.closed = True

        broken = Closed()
        flow.sender = broken
        original.close()
        client.transport.sendto(b"fail")
        await asyncio.sleep(.02)
        stats = relay.snapshot()["client_to_server"]
        self.assertEqual(stats["endpoint_dropped"], 1)
        self.assertEqual(stats["endpoint_dropped_bytes"], 4)
        self.assertEqual(relay.flows, {})
        self.assertTrue(broken.closed)

    async def test_unknown_server_source_is_endpoint_drop_even_when_queue_is_full(self):
        relay = await self.relay(delay_ms=200, max_queued_bytes=1)
        client = await self.client(relay.address)
        client.transport.sendto(b"x")
        await asyncio.sleep(.02)
        relay.receive("server_to_client", b"orphan", ("127.0.0.1", 42424))
        stats = relay.snapshot()["server_to_client"]
        self.assertEqual(stats["endpoint_dropped"], 1)
        self.assertEqual(stats["endpoint_dropped_bytes"], 6)
        self.assertEqual(stats["capacity_dropped"], 0)

    async def test_closing_flow_transport_is_removed_after_endpoint_drop(self):
        relay = await self.relay()
        client = await self.client(relay.address)
        client.transport.sendto(b"prime")
        self.assertEqual(await asyncio.wait_for(client.queue.get(), 1), b"prime")
        flow = next(iter(relay.flows.values()))
        flow.transport.abort()
        flow.sender.close()
        client.transport.sendto(b"closed")
        await asyncio.sleep(.02)
        stats = relay.snapshot()["client_to_server"]
        self.assertEqual(stats["endpoint_dropped"], 1)
        self.assertEqual(stats["endpoint_dropped_bytes"], 6)
        self.assertEqual(relay.flows, {})

    async def test_same_seed_reproduces_finite_loopback_drop_counts(self):
        first = await self.relay(loss=.5, seed=73)
        second = await self.relay(loss=.5, seed=73)
        payloads = [bytes((index,)) * 9 for index in range(40)]
        for payload in payloads:
            first.receive("client_to_server", payload, ("127.0.0.1", 40001))
            second.receive("client_to_server", payload, ("127.0.0.1", 40001))
        await asyncio.sleep(.02)
        first_stats = first.snapshot()["client_to_server"]
        second_stats = second.snapshot()["client_to_server"]
        self.assertEqual(first_stats["received"], second_stats["received"])
        self.assertEqual(first_stats["random_dropped"], second_stats["random_dropped"])
        self.assertEqual(first_stats["random_dropped_bytes"], second_stats["random_dropped_bytes"])
        self.assertEqual(first_stats["delivered"], second_stats["delivered"])

    async def test_zero_length_packets_cannot_bypass_the_pending_packet_budget(self):
        relay = await self.relay(delay_ms=100, max_queued_packets=2)
        for _ in range(4):
            relay.receive("client_to_server", b"", ("127.0.0.1", 12345))
        self.assertEqual(relay.snapshot()["queued_packets"], 2)
        self.assertEqual(relay.snapshot()["client_to_server"]["capacity_dropped"], 2)
        self.assertEqual(relay.snapshot()["client_to_server"]["capacity_dropped_bytes"], 0)
        await relay.stop()
        self.assertEqual(relay.snapshot()["queued_packets"], 0)

    async def test_unknown_server_source_is_counted_with_packet_bytes(self):
        relay = await self.relay()
        relay.receive("server_to_client", b"orphan", ("127.0.0.1", 42424))
        stats = relay.snapshot()["server_to_client"]
        self.assertEqual(stats["endpoint_dropped"], 1)
        self.assertEqual(stats["endpoint_dropped_bytes"], 6)

    async def test_socket_backpressure_drops_without_an_unbounded_transport_buffer(self):
        relay = await self.relay()
        client = await self.client(relay.address)
        client.transport.sendto(b"prime")
        await asyncio.wait_for(client.queue.get(), 1)
        flow = next(iter(relay.flows.values()))
        original = flow.sender
        class Blocked:
            closed = False
            def send(self, data):
                raise BlockingIOError()
            def sendto(self, data, address):
                raise BlockingIOError()
            def close(self):
                self.closed = True
        blocked = Blocked()
        flow.sender = blocked
        original.close()
        client.transport.sendto(b"drop")
        await asyncio.sleep(.02)
        self.assertEqual(relay.snapshot()["client_to_server"]["send_pressure_dropped"], 1)
        self.assertEqual(relay.snapshot()["client_to_server"]["send_pressure_dropped_bytes"], 4)
        self.assertEqual(flow.transport.get_write_buffer_size(), 0)
        await relay.stop()
        self.assertTrue(blocked.closed)

    async def test_a_transient_upstream_socket_failure_does_not_poison_the_client_flow(self):
        relay = await self.relay()
        client = await self.client(relay.address)
        original = self.loop.create_datagram_endpoint
        failed = False
        async def flaky(*args, **kwargs):
            nonlocal failed
            if kwargs.get("remote_addr") == self.upstream and not failed:
                failed = True
                raise OSError("fixture temporary socket creation failure")
            return await original(*args, **kwargs)
        with patch.object(self.loop, "create_datagram_endpoint", side_effect=flaky):
            client.transport.sendto(b"first")
            await asyncio.sleep(.02)
            self.assertEqual(relay.snapshot()["client_to_server"]["endpoint_dropped"], 1)
            client.transport.sendto(b"retry")
            self.assertEqual(await asyncio.wait_for(client.queue.get(), 1), b"retry")

    async def test_cancelled_listener_acquisition_closes_duplicate_and_rebinds_owned_port(self):
        relay = UdpImpairment(self.upstream)
        self.relays.append(relay)
        original = self.loop.create_datagram_endpoint
        entered = asyncio.Event()
        created = []

        async def wrapped(factory, *args, **kwargs):
            owner = asyncio.current_task()
            def factory_with_cancel():
                protocol = factory()
                made = protocol.connection_made
                def connection_made(transport):
                    made(transport)
                    created.append(transport)
                    entered.set()
                    owner.cancel()
                protocol.connection_made = connection_made
                return protocol
            return await original(factory_with_cancel, *args, **kwargs)

        with patch.object(self.loop, "create_datagram_endpoint", side_effect=wrapped):
            start = asyncio.create_task(relay.start(("127.0.0.1", 0)))
            await asyncio.wait_for(entered.wait(), 1)
            address = relay.sender.getsockname()
            result = (await asyncio.gather(start, return_exceptions=True))[0]

        self.assertIsInstance(result, asyncio.CancelledError)
        await asyncio.sleep(0)
        self.assertTrue(created[0].is_closing())
        self.assertEqual(relay.sender.fileno(), -1)
        owned = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            owned.bind(address)
        finally:
            owned.close()

    async def test_cancelled_upstream_acquisition_releases_pending_once_and_retry_succeeds(self):
        relay = await self.relay(delay_ms=0)
        client = await self.client(relay.address)
        original = self.loop.create_datagram_endpoint
        endpoint_created = asyncio.Event()
        client_address = client.transport.get_extra_info("sockname")

        async def wrapped(factory, *args, **kwargs):
            if kwargs.get("remote_addr") != self.upstream:
                return await original(factory, *args, **kwargs)
            owner = asyncio.current_task()
            def factory_with_cancel():
                protocol = factory()
                made = protocol.connection_made
                def connection_made(transport):
                    made(transport)
                    endpoint_created.set()
                    owner.cancel()
                protocol.connection_made = connection_made
                return protocol
            return await original(factory_with_cancel, *args, **kwargs)

        with patch.object(self.loop, "create_datagram_endpoint", side_effect=wrapped):
            relay.receive("client_to_server", b"cancelled", client_address)
            relay.receive("client_to_server", b"second-waiter", client_address)
            await asyncio.wait_for(endpoint_created.wait(), 1)
            ready = relay.flows[client_address].ready
            await asyncio.gather(ready, return_exceptions=True)

        dropped = relay.snapshot()["client_to_server"]
        self.assertEqual(dropped["endpoint_dropped"], 2)
        self.assertEqual(
            dropped["endpoint_dropped_bytes"],
            len(b"cancelled") + len(b"second-waiter"),
        )
        self.assertEqual(relay.snapshot()["queued_packets"], 0)
        self.assertEqual(relay.snapshot()["queued_bytes"], 0)
        self.assertEqual(relay.flows, {})

        client.transport.sendto(b"retry")
        self.assertEqual(await asyncio.wait_for(client.queue.get(), 1), b"retry")

        sink_transport, _ = await self.loop.create_datagram_endpoint(
            asyncio.DatagramProtocol, local_addr=("127.0.0.1", 0)
        )
        boundary_receiver, _ = await self.loop.create_datagram_endpoint(
            asyncio.DatagramProtocol, local_addr=("127.0.0.1", 0)
        )
        self.clients.extend((sink_transport, boundary_receiver))
        pre_entry_relay = UdpImpairment(
            sink_transport.get_extra_info("sockname"),
            delay_ms=10,
            idle_seconds=.01,
        )
        await pre_entry_relay.start(("127.0.0.1", 0))
        self.relays.append(pre_entry_relay)
        pre_entry_address = boundary_receiver.get_extra_info("sockname")
        pre_entry_relay.receive(
            "client_to_server", b"cancel-before-entry", pre_entry_address
        )
        pre_entry_ready = pre_entry_relay.flows[pre_entry_address].ready
        pre_entry_ready.cancel()
        await asyncio.sleep(.06)

        pre_entry_stats = pre_entry_relay.snapshot()
        self.assertTrue(pre_entry_ready.cancelled())
        self.assertEqual(pre_entry_stats["queued_packets"], 0)
        self.assertEqual(pre_entry_stats["queued_bytes"], 0)
        self.assertEqual(pre_entry_stats["flows"], 0)
        self.assertEqual(
            pre_entry_stats["client_to_server"]["endpoint_dropped"], 1
        )
        self.assertEqual(
            pre_entry_stats["client_to_server"]["endpoint_dropped_bytes"],
            len(b"cancel-before-entry"),
        )

        pre_entry_relay.receive(
            "client_to_server", b"pre-entry-retry", pre_entry_address
        )
        await asyncio.sleep(.06)
        retried_stats = pre_entry_relay.snapshot()["client_to_server"]
        self.assertEqual(retried_stats["received"], 2)
        self.assertEqual(retried_stats["endpoint_dropped"], 1)
        self.assertEqual(retried_stats["delivered"], 1)
        self.assertEqual(pre_entry_relay.snapshot()["queued_packets"], 0)
        self.assertEqual(pre_entry_relay.snapshot()["queued_bytes"], 0)

        stale_receiver, _ = await self.loop.create_datagram_endpoint(
            asyncio.DatagramProtocol, local_addr=("127.0.0.1", 0)
        )
        self.clients.append(stale_receiver)
        stale_relay = await self.relay()
        stale_address = stale_receiver.get_extra_info("sockname")
        stale_relay.receive("client_to_server", b"stale-owner", stale_address)
        stale_flow = stale_relay.flows[stale_address]
        stale_flow.ready.cancel()
        stale_relay.drop_flow(stale_flow, outcome="endpoint_dropped")
        stale_relay.receive("client_to_server", b"fresh-owner", stale_address)
        fresh_flow = stale_relay.flows[stale_address]
        self.assertIsNot(fresh_flow, stale_flow)
        await asyncio.sleep(.02)
        stale_stats = stale_relay.snapshot()["client_to_server"]
        self.assertEqual(stale_stats["endpoint_dropped"], 1)
        self.assertEqual(stale_stats["delivered"], 1)
        self.assertEqual(stale_relay.snapshot()["queued_packets"], 0)
        self.assertEqual(stale_relay.snapshot()["queued_bytes"], 0)
        self.assertIs(stale_relay.flows[stale_address], fresh_flow)

    async def test_stop_cancellation_is_classified_as_shutdown_without_double_release(self):
        relay = await self.relay(delay_ms=0)
        client = await self.client(relay.address)
        original = self.loop.create_datagram_endpoint
        entered = asyncio.Event()
        release = asyncio.Event()

        async def held(factory, *args, **kwargs):
            if kwargs.get("remote_addr") == self.upstream:
                entered.set()
                await release.wait()
            return await original(factory, *args, **kwargs)

        with patch.object(self.loop, "create_datagram_endpoint", side_effect=held):
            client.transport.sendto(b"stopping")
            await asyncio.wait_for(entered.wait(), 1)
            await relay.stop()
            release.set()
            await asyncio.sleep(0)

        stats = relay.snapshot()["client_to_server"]
        self.assertEqual(stats["shutdown_dropped"], 1)
        self.assertEqual(stats["shutdown_dropped_bytes"], len(b"stopping"))
        self.assertEqual(stats["endpoint_dropped"], 0)
        self.assertEqual(relay.snapshot()["queued_packets"], 0)
        self.assertEqual(relay.snapshot()["queued_bytes"], 0)
        self.assertEqual(relay.flows, {})

        pre_entry_relay = await self.relay(delay_ms=10)
        pre_entry_client = await self.client(pre_entry_relay.address)
        pre_entry_address = pre_entry_client.transport.get_extra_info("sockname")
        pre_entry_relay.receive(
            "client_to_server", b"cancel-stop-before-entry", pre_entry_address
        )
        pre_entry_relay.flows[pre_entry_address].ready.cancel()
        await pre_entry_relay.stop()

        pre_entry_stats = pre_entry_relay.snapshot()["client_to_server"]
        self.assertEqual(pre_entry_stats["shutdown_dropped"], 1)
        self.assertEqual(
            pre_entry_stats["shutdown_dropped_bytes"],
            len(b"cancel-stop-before-entry"),
        )
        self.assertEqual(pre_entry_stats["endpoint_dropped"], 0)
        self.assertEqual(pre_entry_relay.snapshot()["queued_packets"], 0)
        self.assertEqual(pre_entry_relay.snapshot()["queued_bytes"], 0)
        self.assertEqual(pre_entry_relay.flows, {})

if __name__ == "__main__":
    unittest.main()
