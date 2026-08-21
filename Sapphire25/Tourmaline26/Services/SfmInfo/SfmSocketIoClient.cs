using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tourmaline26.Services.SfmInfo
{
    /// <summary>
    /// Cliente mínimo Engine.IO v4 / Socket.IO (solo long-polling HTTP)
    /// para el panel de salidas de info.trensfm.com.
    /// </summary>
    internal sealed class SfmSocketIoClient : IAsyncDisposable
    {
        private static readonly Regex OpenPacketRegex = new(
            @"0(\{.*?\})(?=40|42|2|3|$)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly HttpClient mvarHttp;
        private readonly Uri mvarBaseUri;
        private readonly ILogger mvarLogger;
        private string? mvarSid;
        private bool mvarDisposed;

        public bool IsConnected => !string.IsNullOrEmpty(mvarSid);

        public SfmSocketIoClient(HttpClient httpClient, Uri baseUri, ILogger logger)
        {
            mvarHttp = httpClient;
            mvarBaseUri = baseUri;
            mvarLogger = logger;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            mvarSid = null;

            string openBody = await GetAsync(sid: null, cancellationToken).ConfigureAwait(false);
            Match openMatch = Regex.Match(openBody, @"^0(\{.*\})");
            if (!openMatch.Success)
                openMatch = OpenPacketRegex.Match(openBody);
            if (!openMatch.Success)
                throw new InvalidOperationException($"Socket.IO handshake inválido: {Truncate(openBody)}");

            using JsonDocument doc = JsonDocument.Parse(openMatch.Groups[1].Value);
            mvarSid = doc.RootElement.GetProperty("sid").GetString()
                ?? throw new InvalidOperationException("Socket.IO sin sid.");

            // Namespace por defecto.
            await PostAsync("40", cancellationToken).ConfigureAwait(false);

            // Connect + eventos iniciales (base_linea, etc.) en el siguiente long-poll.
            string first = await GetAsync(sid: mvarSid, cancellationToken).ConfigureAwait(false);
            ProcessIncoming(first, out _, out bool needPong);
            if (needPong)
                await PostAsync("3", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Emite un evento Socket.IO con argumentos JSON serializados.
        /// </summary>
        public Task EmitAsync(string eventName, CancellationToken cancellationToken, params object?[] args)
        {
            var payload = new List<object?> { eventName };
            if (args is { Length: > 0 })
                payload.AddRange(args);

            string packet = "42" + JsonSerializer.Serialize(payload);
            return PostAsync(packet, cancellationToken);
        }

        /// <summary>
        /// Long-poll: recibe paquetes; responde pings; devuelve eventos Socket.IO parseados.
        /// </summary>
        public async Task<IReadOnlyList<SfmSocketEvent>> PollAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mvarSid))
                throw new InvalidOperationException("Socket.IO no conectado.");

            string body = await GetAsync(sid: mvarSid, cancellationToken).ConfigureAwait(false);
            ProcessIncoming(body, out List<SfmSocketEvent> events, out bool needPong);
            if (needPong)
                await PostAsync("3", cancellationToken).ConfigureAwait(false);

            return events;
        }

        public async ValueTask DisposeAsync()
        {
            if (mvarDisposed)
                return;
            mvarDisposed = true;

            try
            {
                if (!string.IsNullOrEmpty(mvarSid))
                    await PostAsync("41", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // ignore on dispose
            }

            mvarSid = null;
        }

        private async Task<string> GetAsync(string? sid, CancellationToken cancellationToken)
        {
            string url = BuildUrl(sid);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Version = HttpVersion.Version11;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            req.Headers.TryAddWithoutValidation("Accept", "*/*");
            using HttpResponseMessage response = await mvarHttp.SendAsync(req, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Socket.IO GET {response.StatusCode}: {Truncate(body)}");
            return body;
        }

        private async Task PostAsync(string body, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mvarSid))
                throw new InvalidOperationException("Socket.IO no conectado.");

            string url = BuildUrl(mvarSid);
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "UTF-8" };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Version = HttpVersion.Version11;
            req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            req.Headers.TryAddWithoutValidation("Accept", "*/*");
            req.Content = content;

            using HttpResponseMessage response = await mvarHttp.SendAsync(req, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Socket.IO POST {response.StatusCode}: {Truncate(responseBody)}");
        }

        private string BuildUrl(string? sid)
        {
            // Base: https://info.trensfm.com/  →  https://info.trensfm.com/socket.io/?EIO=4&transport=polling
            string root = mvarBaseUri.AbsoluteUri.TrimEnd('/');
            var sb = new StringBuilder();
            sb.Append(root);
            sb.Append("/socket.io/?EIO=4&transport=polling&t=");
            sb.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (!string.IsNullOrEmpty(sid))
            {
                sb.Append("&sid=");
                sb.Append(Uri.EscapeDataString(sid));
            }
            return sb.ToString();
        }

        private void ProcessIncoming(string body, out List<SfmSocketEvent> events, out bool needPong)
        {
            events = new List<SfmSocketEvent>();
            needPong = false;
            if (string.IsNullOrEmpty(body))
                return;

            // Engine.IO v4 puede concatenar paquetes o separarlos con \x1e.
            string[] chunks = body.Contains('\u001e')
                ? body.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
                : SplitEnginePackets(body);

            foreach (string chunk in chunks)
            {
                if (chunk.Length == 0)
                    continue;

                char type = chunk[0];
                string rest = chunk.Length > 1 ? chunk[1..] : string.Empty;

                switch (type)
                {
                    case '2': // ping
                        needPong = true;
                        break;
                    case '3': // pong
                        break;
                    case '4': // message → Socket.IO
                        ParseSocketPacket(rest, events);
                        break;
                    case '0': // open (re-handshake, raro en poll)
                        mvarLogger.LogDebug("Socket.IO open packet en poll: {Body}", Truncate(chunk));
                        break;
                    case '1': // close
                        mvarSid = null;
                        mvarLogger.LogWarning("Socket.IO cerró la sesión.");
                        break;
                    case '6': // noop
                        break;
                    default:
                        // Fallback: paquete ya en forma Socket.IO (42[...]).
                        if (chunk.StartsWith("42", StringComparison.Ordinal))
                            ParseSocketPacket(chunk[1..], events);
                        break;
                }
            }
        }

        private static void ParseSocketPacket(string packet, List<SfmSocketEvent> events)
        {
            if (packet.Length == 0)
                return;

            // 0 connect, 1 disconnect, 2 event, 3 ack, 4 error, 5 binary event...
            if (packet[0] == '0')
                return;
            if (packet[0] == '1')
                return;

            if (packet[0] != '2')
                return;

            string json = packet[1..];
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 1)
                    return;

                string eventName = doc.RootElement[0].GetString() ?? string.Empty;
                JsonElement? data = doc.RootElement.GetArrayLength() > 1
                    ? doc.RootElement[1].Clone()
                    : null;

                events.Add(new SfmSocketEvent(eventName, data));
            }
            catch
            {
                // paquete no JSON-evento; se ignora
            }
        }

        /// <summary>
        /// Separa paquetes concatenados del estilo <c>40{...}42["a",...]2</c>.
        /// </summary>
        private static string[] SplitEnginePackets(string body)
        {
            var list = new List<string>();
            int i = 0;
            while (i < body.Length)
            {
                char t = body[i];
                if (t is '2' or '3' or '6')
                {
                    list.Add(body.Substring(i, 1));
                    i++;
                    continue;
                }

                if (t == '4' && i + 1 < body.Length)
                {
                    // 40..., 41..., 42[...]
                    int start = i;
                    i++; // skip '4'
                    char sio = body[i];
                    if (sio == '2' || sio == '3' || sio == '4' || sio == '5' || sio == '6')
                    {
                        i++; // socket type
                        if (i < body.Length && body[i] == '[')
                        {
                            int end = FindMatchingBracket(body, i);
                            if (end < 0)
                            {
                                list.Add(body[start..]);
                                break;
                            }
                            list.Add(body[start..(end + 1)]);
                            i = end + 1;
                            continue;
                        }
                        // 40{...} connect with optional JSON
                        if (i < body.Length && body[i] == '{')
                        {
                            int end = FindMatchingBrace(body, i);
                            if (end < 0)
                            {
                                list.Add(body[start..]);
                                break;
                            }
                            list.Add(body[start..(end + 1)]);
                            i = end + 1;
                            continue;
                        }
                        list.Add(body[start..i]);
                        continue;
                    }

                    if (sio == '0' || sio == '1')
                    {
                        i++;
                        if (i < body.Length && body[i] == '{')
                        {
                            int end = FindMatchingBrace(body, i);
                            if (end < 0)
                            {
                                list.Add(body[start..]);
                                break;
                            }
                            list.Add(body[start..(end + 1)]);
                            i = end + 1;
                        }
                        else
                        {
                            list.Add(body[start..i]);
                        }
                        continue;
                    }
                }

                // Paquete que empieza por 0{...} (open) o 42 sin prefijo engine (fallback)
                if (t == '0' && i + 1 < body.Length && body[i + 1] == '{')
                {
                    int end = FindMatchingBrace(body, i + 1);
                    if (end < 0)
                    {
                        list.Add(body[i..]);
                        break;
                    }
                    list.Add(body[i..(end + 1)]);
                    i = end + 1;
                    continue;
                }

                if (t == '4' && i + 1 < body.Length && body[i + 1] == '2')
                {
                    // ya cubierto arriba; si falló, tragar resto
                    list.Add(body[i..]);
                    break;
                }

                if ((t == '4' && i + 1 < body.Length && body[i + 1] == '2') ||
                    (body.AsSpan(i).StartsWith("42")))
                {
                    int jsonStart = body.IndexOf('[', i);
                    if (jsonStart < 0)
                    {
                        list.Add(body[i..]);
                        break;
                    }
                    int end = FindMatchingBracket(body, jsonStart);
                    if (end < 0)
                    {
                        list.Add(body[i..]);
                        break;
                    }
                    list.Add(body[i..(end + 1)]);
                    i = end + 1;
                    continue;
                }

                // carácter desconocido: avanzar
                i++;
            }

            return list.ToArray();
        }

        private static int FindMatchingBracket(string s, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static int FindMatchingBrace(string s, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string Truncate(string s, int max = 180) =>
            s.Length <= max ? s : s[..max] + "…";
    }

    internal readonly struct SfmSocketEvent
    {
        public string Name { get; }
        public JsonElement? Data { get; }

        public SfmSocketEvent(string name, JsonElement? data)
        {
            Name = name;
            Data = data;
        }
    }
}
