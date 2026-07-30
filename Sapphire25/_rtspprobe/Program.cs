using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtsp;

class Program {
  static async Task Main() {
    var uri = new Uri("rtsp://192.168.0.50:554/v2");
    var conn = new ConnectionParameters(uri) {
      RtpTransport = RtpTransportProtocol.TCP,
      RequiredTracks = RequiredTracks.Video,
      ConnectTimeout = TimeSpan.FromSeconds(5),
      ReceiveTimeout = TimeSpan.FromSeconds(8)
    };
    using var client = new RtspClient(conn);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
    var tcs = new TaskCompletionSource<byte[]>();
    client.FrameReceived += (s, f) => {
      if (f is RawJpegFrame j && !tcs.Task.IsCompleted) {
        var seg = j.FrameSegment;
        var buf = new byte[seg.Count];
        Buffer.BlockCopy(seg.Array!, seg.Offset, buf, 0, seg.Count);
        tcs.TrySetResult(buf);
      }
    };
    await client.ConnectAsync(cts.Token);
    var receive = client.ReceiveAsync(cts.Token);
    var jpeg = await tcs.Task.WaitAsync(cts.Token);
    var path = Path.Combine(Directory.GetCurrentDirectory(), "camera-test.jpg");
    await File.WriteAllBytesAsync(path, jpeg);
    Console.WriteLine($"OK saved {jpeg.Length} bytes -> {path}");
    cts.Cancel();
    try { await receive; } catch { }
  }
}
